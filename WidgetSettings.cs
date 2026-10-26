using System.IO;
using System.Text.Json;

namespace CodexTokenWidget;

public sealed class WidgetSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexTokenWidget");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool AutoRefreshEnabled { get; set; } = true;
    public int RefreshIntervalMinutes { get; set; } = 1;
    public int EdgeBarSizePercent { get; set; } = 67;
    public string LanguageMode { get; set; } = "System";
    public string ThemeMode { get; set; } = "System";
    public bool StartWithWindows { get; set; } = true;
    public bool StartWithCodex { get; set; } = true;
    public bool ShowQuotaCard { get; set; } = true;
    public bool ShowTodayCard { get; set; } = true;
    public bool ShowChartCard { get; set; } = true;
    public bool ShowStatisticsCard { get; set; } = true;
    public bool ShowLifetimeTokensCard { get; set; } = true;
    public bool ShowPeakDailyTokensCard { get; set; } = true;
    public bool ShowLongestTurnCard { get; set; } = true;
    public bool ShowCurrentStreakCard { get; set; } = true;
    public bool ShowLongestStreakCard { get; set; } = true;

    public static WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new WidgetSettings();
            return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(SettingsPath)) ?? new WidgetSettings();
        }
        catch
        {
            return new WidgetSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
