using System.Reflection;
using System.Text.Json;
using CodexTokenWidget;

internal static class UsageChecks
{
    public static async Task RunAsync(bool live)
    {
        var checks = 0;
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
            checks++;
        }
        var limits = JsonSerializer.SerializeToElement(new
        {
            rateLimits = new
            {
                primary = new { usedPercent = 26, resetsAt = 1793013355 },
                secondary = new { usedPercent = 4, resetsAt = 1793600155 }
            }
        });
        var usage = JsonSerializer.SerializeToElement(new
        {
            summary = new { lifetimeTokens = 1806778657L, peakDailyTokens = 138519152L, currentStreakDays = 7 },
            dailyUsageBuckets = Enumerable.Range(-6, 7).Select(offset => new
            {
                startDate = DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd"), tokens = 10000 + offset * 100
            })
        });
        var method = typeof(CodexUsageService).GetMethod("ReadRemoteSnapshotAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task<UsageSnapshot> Read(CodexUsageService service, Func<string, CancellationToken, Task<JsonElement>> query) =>
            (Task<UsageSnapshot>)method.Invoke(service, new object[] { query, CancellationToken.None })!;
        void SetCache(CodexUsageService service, UsageSnapshot snapshot) =>
            typeof(CodexUsageService).GetField("lastAppServerSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, snapshot);

        var service = new CodexUsageService();
        var historyCalls = 0;
        var partial = await Read(service, (name, _) =>
        {
            if (name.Contains("rateLimits")) return Task.FromResult(limits);
            historyCalls++;
            throw new TimeoutException("history timed out");
        });
        Check(historyCalls == 2, "History timeout retries once");
        Check(partial.HasRateLimits && partial.FiveHourUsedPercent == 26 && partial.WeekUsedPercent == 4, "History failure keeps successful limits");
        Check(service.LastAppServerError?.Contains("history timed out") == true, "Partial failure exposes diagnostics");

        var limitsCalls = 0;
        partial = await Read(new CodexUsageService(), (name, _) =>
        {
            if (name.Contains("usage")) return Task.FromResult(usage);
            limitsCalls++;
            throw new TimeoutException("limits timed out");
        });
        Check(limitsCalls == 2, "Limits timeout retries once");
        Check(!partial.HasRateLimits && partial.LifetimeTokens == 1806778657L, "Limits failure preserves history without fake zero limits");
        Check(partial.LastSevenDays.All(day => day.IsReported), "Limits failure preserves seven-day chart");

        var transientCalls = 0;
        service = new CodexUsageService();
        var complete = await Read(service, (name, _) =>
        {
            if (name.Contains("rateLimits")) return Task.FromResult(limits);
            if (++transientCalls == 1) throw new TimeoutException("transient failure");
            return Task.FromResult(usage);
        });
        Check(transientCalls == 2 && complete.LifetimeTokens == 1806778657L, "Transient history error recovers automatically");
        Check(service.LastAppServerError is null, "Recovered retry clears warning");
        SetCache(service, complete);
        var cached = await Read(service, (name, _) => name.Contains("rateLimits") ? Task.FromResult(limits) : throw new TimeoutException());
        Check(cached.LifetimeTokens == complete.LifetimeTokens && cached.HasRateLimits, "History outage preserves cached summary");
        Check(service.LastReadUsedCachedAppServerData, "Cached result is labeled");
        var roundTrip = JsonSerializer.Deserialize<UsageSnapshot>(JsonSerializer.Serialize(complete))!;
        Check(roundTrip.HasRateLimits && roundTrip.LifetimeTokens == complete.LifetimeTokens && roundTrip.LastSevenDays.Count == 7, "Persisted snapshot retains limits, history and availability");
        SetCache(service, complete with { AccountId = "previous-account" });
        var newAccountLimits = JsonSerializer.SerializeToElement(new { accountId = "new-account", rateLimits = new { primary = new { usedPercent = 10 } } });
        var switched = await Read(service, (name, _) => name.Contains("rateLimits") ? Task.FromResult(newAccountLimits) : throw new TimeoutException());
        Check(switched.AccountId == "new-account" && switched.LifetimeTokens is null, "Account switch does not mix old cached history with new limits");
        var missing = await Read(new CodexUsageService(), (_, _) => throw new TimeoutException());
        Check(!missing.HasRateLimits && !missing.IsTodayReported && missing.LastSevenDays.All(day => !day.IsReported), "Cold-start outage marks unknown data unavailable");
        Console.WriteLine($"Passed {checks} usage recovery checks.");

        if (live)
        {
            var liveService = new CodexUsageService();
            var snapshot = await liveService.ReadAsync();
            Console.WriteLine($"Live query result: limits={snapshot.HasRateLimits}, lifetimeAvailable={snapshot.LifetimeTokens is not null}, reportedDays={snapshot.LastSevenDays.Count(day => day.IsReported)}/7, warning={liveService.LastAppServerError ?? "none"}.");
            if (!snapshot.HasRateLimits || snapshot.LifetimeTokens is null || liveService.LastAppServerError is not null)
                throw new InvalidOperationException("Live backend validation failed: " + liveService.LastAppServerError);
            Console.WriteLine($"Live backend verified: 5h={snapshot.FiveHourUsedPercent}%, week={snapshot.WeekUsedPercent}%, lifetime={snapshot.LifetimeTokens}, reportedDays={snapshot.LastSevenDays.Count(day => day.IsReported)}/7.");
            var restoredService = new CodexUsageService();
            typeof(CodexUsageService).GetMethod("LoadCache", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(restoredService, null);
            var restored = (UsageSnapshot?)typeof(CodexUsageService).GetField("lastAppServerSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(restoredService);
            if (restored?.LifetimeTokens != snapshot.LifetimeTokens || restoredService.LastSuccessfulReadAt is null)
                throw new InvalidOperationException("Successful live data did not survive cache reload.");
            Console.WriteLine("Persisted real backend data reload verified.");
        }
    }
}
