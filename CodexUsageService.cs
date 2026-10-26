using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodexTokenWidget;

public sealed class CodexUsageService
{
    private UsageSnapshot? lastAppServerSnapshot;

    public bool LastReadUsedCachedAppServerData { get; private set; }
    public string? LastAppServerError { get; private set; }

    public async Task<UsageSnapshot> ReadAsync()
    {
        LastReadUsedCachedAppServerData = false;
        LastAppServerError = null;
        var localUsageTask = Task.Run(ReadLocalTodayUsage);
        try
        {
            var snapshot = await ReadAppServerAsync();
            lastAppServerSnapshot = snapshot;
            return ApplyLocalTodayUsage(snapshot, await localUsageTask);
        }
        catch (Exception exception)
        {
            LastAppServerError = exception.Message;
            var localUsage = await localUsageTask;
            if (lastAppServerSnapshot is not null)
            {
                LastReadUsedCachedAppServerData = true;
                return ApplyLocalTodayUsage(RefreshCalendar(lastAppServerSnapshot), localUsage);
            }
            if (localUsage.IsAvailable) return BuildLocalOnlySnapshot(localUsage);
            throw;
        }
    }

    private async Task<UsageSnapshot> ReadAppServerAsync()
    {
        using var process = StartAppServer();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        // Drain stderr while the server runs so a full pipe cannot stall JSON-RPC replies.
        var stderr = process.StandardError.ReadToEndAsync(cancellation.Token);

        try
        {
            var requestId = 1;
            await SendAsync(process, new
            {
                method = "initialize",
                id = requestId++,
                @params = new
                {
                    clientInfo = new { name = "codex-token-widget", title = "Codex Token Widget", version = typeof(CodexUsageService).Assembly.GetName().Version?.ToString(3) },
                    capabilities = new { experimentalApi = true, requestAttestation = false }
                }
            }, cancellation.Token);
            await ReadResponseAsync(process, 1, cancellation.Token, stderr);

            await SendAsync(process, new { method = "initialized" }, cancellation.Token);
            return await ReadRemoteSnapshotAsync(async (method, token) =>
            {
                var id = requestId++;
                await SendAsync(process, new { method, id, @params = new { } }, token);
                return await ReadResponseAsync(process, id, token, stderr);
            }, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(Localization.Text("Codex 后台查询超时，请稍后刷新。"));
        }
        finally
        {
            TryStop(process);
            try { await stderr; } catch (OperationCanceledException) { }
        }
    }

    private async Task<UsageSnapshot> ReadRemoteSnapshotAsync(
        Func<string, CancellationToken, Task<JsonElement>> query, CancellationToken token)
    {
        // These endpoints are independent: a failed history query must not erase fresh limits.
        var errors = new List<string>();
        async Task<JsonElement?> ReadWithRetry(string method)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    return await query(method, timeout.Token);
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    if (attempt == 1) errors.Add($"{method}: {exception.Message}");
                    else await Task.Delay(500, token);
                }
            }
            return null;
        }

        var limits = await ReadWithRetry("account/rateLimits/read");
        var usage = await ReadWithRetry("account/usage/read");
        LastAppServerError = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        var previous = lastAppServerSnapshot is null ? null : RefreshCalendar(lastAppServerSnapshot);
        LastReadUsedCachedAppServerData = previous is not null && (limits is null || usage is null);
        var snapshot = previous ?? BuildLocalOnlySnapshot(default);
        if (limits is { } limitsResponse)
        {
            var rateLimits = TryGetObject(limitsResponse, "rateLimits");
            var primary = TryGetObject(rateLimits, "primary");
            var secondary = TryGetObject(rateLimits, "secondary");
            snapshot = snapshot with
            {
                FiveHourUsedPercent = ReadPercent(primary), FiveHourResetsAt = ReadReset(primary),
                WeekUsedPercent = ReadPercent(secondary), WeekResetsAt = ReadReset(secondary),
                HasRateLimits = primary.ValueKind == JsonValueKind.Object || secondary.ValueKind == JsonValueKind.Object
            };
        }
        if (usage is { } usageResponse) snapshot = ApplyUsage(snapshot, usageResponse);
        return snapshot;
    }

    private static UsageSnapshot RefreshCalendar(UsageSnapshot snapshot)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = Enumerable.Range(-6, 7).Select(offset =>
        {
            var date = today.AddDays(offset);
            return snapshot.LastSevenDays.FirstOrDefault(day => day.Date == date) ?? new DailyUsage(date, 0, false);
        }).ToList();
        return snapshot with { LastSevenDays = days, TodayTokens = days[^1].Tokens, IsTodayReported = days[^1].IsReported };
    }

    private static LocalTodayUsage ReadLocalTodayUsage()
    {
        try
        {
            var sessionsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(sessionsRoot)) return default;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var responseIds = new HashSet<string>(StringComparer.Ordinal);
            long totalTokens = 0;
            var recordCount = 0;

            foreach (var file in Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                         .Where(path => File.GetLastWriteTime(path).Date >= DateTime.Today.AddDays(-1)))
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } line)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        var root = document.RootElement;
                        if (!root.TryGetProperty("type", out var type) || type.GetString() != "token_usage_record") continue;
                        if (!root.TryGetProperty("timestamp", out var timestamp) || !DateTimeOffset.TryParse(timestamp.GetString(), out var recordedAt) || DateOnly.FromDateTime(recordedAt.LocalDateTime) != today) continue;
                        if (!root.TryGetProperty("payload", out var payload) || !payload.TryGetProperty("response_id", out var responseId) || !payload.TryGetProperty("usage", out var usage) || !usage.TryGetProperty("total_tokens", out var total)) continue;

                        var id = responseId.GetString();
                        if (string.IsNullOrWhiteSpace(id) || !responseIds.Add(id)) continue;
                        totalTokens += total.GetInt64();
                        recordCount++;
                    }
                    catch (JsonException)
                    {
                        // Codex may still be writing the final JSONL line; the next refresh will include it.
                    }
                }
            }
            return new LocalTodayUsage(totalTokens, recordCount);
        }
        catch
        {
            return default;
        }
    }

    private static UsageSnapshot ApplyLocalTodayUsage(UsageSnapshot snapshot, LocalTodayUsage localUsage)
    {
        if (!localUsage.IsAvailable) return snapshot;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = snapshot.LastSevenDays.Select(day => day.Date == today ? new DailyUsage(today, localUsage.TotalTokens, true) : day).ToList();
        return snapshot with
        {
            TodayTokens = localUsage.TotalTokens,
            IsTodayReported = true,
            IsTodayFromLocalSession = true,
            LastSevenDays = days
        };
    }

    private static UsageSnapshot BuildLocalOnlySnapshot(LocalTodayUsage localUsage)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = Enumerable.Range(-6, 7).Select(offset =>
        {
            var date = today.AddDays(offset);
            return new DailyUsage(date, date == today ? localUsage.TotalTokens : 0, date == today && localUsage.IsAvailable);
        }).ToList();
        return new UsageSnapshot(0, null, 0, null, localUsage.TotalTokens, localUsage.IsAvailable, localUsage.IsAvailable, days, null, null, null, null, null) { HasRateLimits = false };
    }

    private static Process StartAppServer()
    {
        var startInfo = new ProcessStartInfo(ResolveCodexExecutable())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--stdio");
        return Process.Start(startInfo) ?? throw new InvalidOperationException(Localization.Text("无法启动 Codex app-server。"));
    }

    private static string ResolveCodexExecutable()
    {
        var localCodexBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        try
        {
            var installed = Directory.Exists(localCodexBin)
                ? Directory.EnumerateFiles(localCodexBin, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            return installed ?? "codex";
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(Localization.Text("找不到本机 Codex CLI。"), exception);
        }
    }

    private static async Task SendAsync(Process process, object message, CancellationToken token)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await process.StandardInput.FlushAsync(token);
    }

    private static async Task<JsonElement> ReadResponseAsync(Process process, int id, CancellationToken token, Task<string> stderr)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(token);
            if (line is null)
            {
                var details = await stderr.WaitAsync(token);
                throw new InvalidOperationException(Localization.Text("Codex app-server 意外结束。{0}", details));
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id) continue;
            if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(Localization.Text("Codex app-server 返回错误：{0}", error.GetRawText()));
            if (!root.TryGetProperty("result", out var result)) throw new InvalidOperationException(Localization.Text("Codex app-server 响应缺少 result 字段。"));
            return result.Clone();
        }
    }

    private static UsageSnapshot ApplyUsage(UsageSnapshot snapshot, JsonElement usageResponse)
    {
        var buckets = new Dictionary<DateOnly, long>();

        if (usageResponse.TryGetProperty("dailyUsageBuckets", out var sourceBuckets) && sourceBuckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in sourceBuckets.EnumerateArray())
            {
                if (DateOnly.TryParse(bucket.GetProperty("startDate").GetString(), out var date)) buckets[date] = bucket.GetProperty("tokens").GetInt64();
            }
        }

        var summary = TryGetObject(usageResponse, "summary");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = Enumerable.Range(-6, 7).Select(offset =>
        {
            var date = today.AddDays(offset);
            return new DailyUsage(date, buckets.GetValueOrDefault(date), buckets.ContainsKey(date));
        }).ToList();

        return snapshot with
        {
            TodayTokens = days[^1].Tokens, IsTodayReported = days[^1].IsReported,
            IsTodayFromLocalSession = false, LastSevenDays = days,
            LifetimeTokens = ReadNullableLong(summary, "lifetimeTokens"),
            PeakDailyTokens = ReadNullableLong(summary, "peakDailyTokens"),
            LongestRunningTurnSec = ReadNullableLong(summary, "longestRunningTurnSec"),
            CurrentStreakDays = ReadNullableLong(summary, "currentStreakDays"),
            LongestStreakDays = ReadNullableLong(summary, "longestStreakDays")
        };
    }

    private static JsonElement TryGetObject(JsonElement owner, string name) => owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;
    private static long? ReadNullableLong(JsonElement owner, string name) => owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
    private static int ReadPercent(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? 0 : value.TryGetProperty("usedPercent", out var percent) ? percent.GetInt32() : 0;
    private static DateTimeOffset? ReadReset(JsonElement value) => value.ValueKind == JsonValueKind.Undefined || !value.TryGetProperty("resetsAt", out var reset) || reset.ValueKind == JsonValueKind.Null ? null : DateTimeOffset.FromUnixTimeSeconds(reset.GetInt64());

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch
        {
            // The process may already have exited between the state check and Kill.
        }
    }
}

public sealed record UsageSnapshot(
    int FiveHourUsedPercent,
    DateTimeOffset? FiveHourResetsAt,
    int WeekUsedPercent,
    DateTimeOffset? WeekResetsAt,
    long TodayTokens,
    bool IsTodayReported,
    bool IsTodayFromLocalSession,
    IReadOnlyList<DailyUsage> LastSevenDays,
    long? LifetimeTokens,
    long? PeakDailyTokens,
    long? LongestRunningTurnSec,
    long? CurrentStreakDays,
    long? LongestStreakDays)
{
    public bool HasRateLimits { get; init; } = true;
}

public sealed record DailyUsage(DateOnly Date, long Tokens, bool IsReported);

public readonly record struct LocalTodayUsage(long TotalTokens, int RecordCount)
{
    public bool IsAvailable => RecordCount > 0;
}
