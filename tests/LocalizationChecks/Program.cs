using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexTokenWidget;
using Localization = CodexTokenWidget.Localization;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static void Main(string[] args)
    {
        Check(new WidgetSettings().LanguageMode == "System", "New settings follow system");
        Check(JsonSerializer.Deserialize<WidgetSettings>("{\"ThemeMode\":\"Dark\"}")!.LanguageMode == "System", "Old settings migrate without losing defaults");
        foreach (var (mode, system, expected) in new[]
        {
            ("System", "en-GB", "en-US"), ("System", "zh-TW", "zh-CN"),
            ("System", "ja-JP", "zh-CN"), ("en", "zh-CN", "en-US"),
            ("zh-CN", "en-US", "zh-CN"), ("invalid", "en-US", "zh-CN")
        }) Check(Localization.ResolveCulture(mode, CultureInfo.GetCultureInfo(system)).Name == expected, $"Resolve {mode}/{system}");

        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        Localization.Apply("en");
        Check(Localization.Text("设置") == "Settings", "English translation");
        Check(Localization.FormatNumber(4855000) == "4.9M", "English millions");
        Check(Localization.FormatNumber(1810000000) == "1.8B", "English billions");
        Check(Localization.FormatNumber(999) == "999", "Small number");
        Check(Localization.FormatNumber(null) == "--", "Unavailable number");
        Check(Localization.FormatDuration(7440) == "2h 4m", "English duration");

        var settings = new WidgetSettings { LanguageMode = "en", AutoRefreshEnabled = false, ThemeMode = "Light" };
        var settingsWindow = new SettingsWindow(settings);
        Check(settingsWindow.Title.EndsWith("Settings"), "English settings title");
        Check(((ComboBox)settingsWindow.FindName("LanguageModeComboBox")).SelectedValue?.ToString() == "en", "Saved language selection");
        Check(((CheckBox)settingsWindow.FindName("ShowQuotaCardCheckBox")).Content?.ToString() == "5-hour and weekly limits", "English settings labels");

        var window = new MainWindow();
        typeof(MainWindow).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, settings);
        Invoke(window, "ApplySettings");
        var days = Enumerable.Range(-6, 7).Select(offset => new DailyUsage(DateOnly.FromDateTime(DateTime.Today).AddDays(offset), 1000000 + offset * 100000, true)).ToList();
        var snapshot = new UsageSnapshot(78, DateTimeOffset.Now, 62, DateTimeOffset.Now, 4855000, true, true, days, 1810000000, 140000000, 7440, 7, 7);
        typeof(MainWindow).GetField("latestSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, snapshot);
        Invoke(window, "RenderSnapshot", snapshot);
        Check(window.Title == "Codex token usage", "English widget title");
        Check(((TextBlock)window.FindName("TodayTokensText")).Text == "4.9M", "English widget number");
        Check(((TextBlock)window.FindName("LongestTurnText")).Text == "2h 4m", "English widget duration");
        var tray = (System.Windows.Forms.NotifyIcon)typeof(MainWindow).GetField("trayIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Check(tray.ContextMenuStrip!.Items[0].Text == "Show", "English tray menu");
        if (args.Length > 0)
        {
            SavePreview(window, Path.Combine(args[0], "widget-en.png"));
            SavePreview(settingsWindow, Path.Combine(args[0], "settings-en.png"));
        }

        settings.LanguageMode = "zh-CN";
        Invoke(window, "ApplySettings");
        Check(window.Title == "Codex token 使用量", "Live Chinese widget title");
        Check(settingsWindow.Title == "Codex Token Widget 设置", "Live settings resources");
        Check(((TextBlock)window.FindName("TodayTokensText")).Text == "485.5万", "Live Chinese number");
        Check(tray.ContextMenuStrip!.Items[0].Text == "显示", "Live Chinese tray menu");
        Check(Localization.FormatDuration(7440) == "2小时4分", "Chinese duration");
        Check(Localization.FormatNumber(1810000000) == "18.1亿", "Chinese hundred millions");
        Check(JsonSerializer.Deserialize<WidgetSettings>(JsonSerializer.Serialize(settings))!.LanguageMode == "zh-CN", "Language survives serialization");
        settings.LanguageMode = "en";
        Invoke(window, "ApplySettings");
        Check(window.Title == "Codex token usage", "Switch back to English");
        window.Close();
        settingsWindow.Close();
        app.Shutdown();
        Console.WriteLine($"Passed {checks} localization checks.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++;
    }

    private static void Invoke(MainWindow window, string method, params object[] args) =>
        typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void SavePreview(Window window, string path)
    {
        // Render detached content: no app startup integration, window display, or network read.
        var content = (FrameworkElement)window.Content;
        var size = new System.Windows.Size(window.Width, window.Height);
        content.Measure(size);
        content.Arrange(new Rect(size));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }
}
