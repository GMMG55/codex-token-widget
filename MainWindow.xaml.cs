using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexTokenWidget;

public partial class MainWindow : Window
{
    private double edgeWindowWidth = 10;
    private double edgeWindowHeight = 62;
    private double edgeTrackHeight = 54;
    private const double SnapDistance = 42;
    private const double EdgeInset = 3;
    private const int WmNcHitTest = 0x0084;

    private readonly CodexUsageService usageService = new();
    private readonly Border[] fiveHourDots;
    private readonly Border[] weekDots;
    private readonly System.Windows.Forms.NotifyIcon trayIcon;
    private readonly DispatcherTimer autoRefreshTimer = new();
    private readonly EdgePreviewWindow edgePreview = new();
    private WidgetSettings settings = WidgetSettings.Load();
    private UsageSnapshot? latestSnapshot;
    private bool isRefreshing;
    private bool isDragging;
    private System.Windows.Point dragStartScreen;
    private double dragStartLeft;
    private double dragStartTop;
    private bool isDocked;
    private bool isEdgeCollapsed;
    private Dock dockSide;
    private double expandedWidth = 300;
    private double expandedHeight = 640;

    public MainWindow()
    {
        Localization.Apply(settings.LanguageMode);
        InitializeComponent();
        fiveHourDots = [FiveDot1, FiveDot2, FiveDot3, FiveDot4, FiveDot5, FiveDot6];
        weekDots = [WeekDot1, WeekDot2, WeekDot3, WeekDot4, WeekDot5, WeekDot6];
        trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "Codex Token Widget",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowFromTray);
        autoRefreshTimer.Tick += async (_, _) => await RefreshAsync();
        SourceInitialized += (_, _) => AddResizeHitTestHook();
        ApplySettings();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => { edgePreview.Close(); trayIcon.Dispose(); };
        edgePreview.PointerEntered += () => Dispatcher.BeginInvoke(() => ExpandFromEdge(false));
        MouseLeave += (_, _) => ScheduleEdgeCollapse();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemPreferencesChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemPreferencesChanged;
    }

    private void SystemPreferencesChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => { if (settings.LanguageMode == "System" || settings.ThemeMode == "System") ApplySettings(); });
    }

    private static System.Drawing.Icon LoadApplicationIcon()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CodexTokenWidget.Assets.codex-token-widget.ico");
        return stream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(stream);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (isRefreshing) return;
        isRefreshing = true;
        RefreshButton.IsEnabled = false;
        UpdatedText.Text = Localization.Text("读取中…");
        try
        {
            var snapshot = await usageService.ReadAsync();
            latestSnapshot = snapshot;
            RenderSnapshot(snapshot);
            ToolTip = null;
        }
        catch (Exception exception)
        {
            UpdatedText.Text = latestSnapshot is null ? Localization.Text("读取失败") : Localization.Text("读取失败，保留上次数据");
            ToolTip = $"{exception.GetType().Name}: {exception.Message}";
            WriteErrorLog(exception);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            isRefreshing = false;
        }
    }

    private void RenderSnapshot(UsageSnapshot snapshot)
    {
        FiveHourPercentText.Text = $"{snapshot.FiveHourUsedPercent}%";
        WeekPercentText.Text = $"{snapshot.WeekUsedPercent}%";
        FiveHourResetText.Text = FormatReset(snapshot.FiveHourResetsAt);
        WeekResetText.Text = FormatReset(snapshot.WeekResetsAt);
        TodayTokensText.Text = snapshot.IsTodayReported ? FormatCompactNumber(snapshot.TodayTokens) : "--";
        TodayCard.ToolTip = Localization.Text("今天 Token 总量：{0:N0}\n来源：{1}", snapshot.TodayTokens, snapshot.IsTodayFromLocalSession ? Localization.Text("本机会话实时统计（估算）") : Localization.Text("app-server 后台统计"));
        TodaySourceText.Text = snapshot.IsTodayFromLocalSession ? Localization.Text("本机会话实时统计（估算）") : snapshot.IsTodayReported ? Localization.Text("app-server 后台统计（可能延迟）") : Localization.Text("等待 app-server 同步当天数据");
        LifetimeTokensText.Text = FormatChineseNumber(snapshot.LifetimeTokens);
        PeakDailyTokensText.Text = FormatChineseNumber(snapshot.PeakDailyTokens);
        LongestTurnText.Text = FormatDuration(snapshot.LongestRunningTurnSec);
        CurrentStreakText.Text = FormatDays(snapshot.CurrentStreakDays);
        LongestStreakText.Text = FormatDays(snapshot.LongestStreakDays);
        PaintDots(fiveHourDots, snapshot.FiveHourUsedPercent);
        PaintDots(weekDots, snapshot.WeekUsedPercent);
        FiveHourPercentText.Foreground = CreateUsageBrush(snapshot.FiveHourUsedPercent);
        WeekPercentText.Foreground = CreateUsageBrush(snapshot.WeekUsedPercent);
        UpdateEdgeProgress(snapshot.FiveHourUsedPercent);
        edgePreview.UpdateProgress(snapshot.FiveHourUsedPercent);
        DrawChart(snapshot.LastSevenDays);
        UpdatedText.Text = usageService.LastReadUsedCachedAppServerData
            ? Localization.Text("刷新 {0:HH:mm} · 限额为缓存数据", DateTime.Now)
            : Localization.Text("刷新 {0:HH:mm}", DateTime.Now);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        settings = dialog.Settings;
        ApplySettings();
        UpdatedText.Text = settings.AutoRefreshEnabled ? Localization.Text("自动刷新 {0} 分钟", settings.RefreshIntervalMinutes) : Localization.Text("自动刷新已关闭");
    }

    private void ApplySettings()
    {
        Localization.Apply(settings.LanguageMode);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Localization.Culture.IetfLanguageTag);
        var previousMenu = trayIcon.ContextMenuStrip;
        trayIcon.ContextMenuStrip = BuildTrayMenu();
        previousMenu?.Dispose();
        if (latestSnapshot is not null) RenderSnapshot(latestSnapshot);
        ConfigureAutoRefresh();
        ApplyTheme();
        QuotaCard.Visibility = ToVisibility(settings.ShowQuotaCard);
        TodayCard.Visibility = ToVisibility(settings.ShowTodayCard);
        ChartCard.Visibility = ToVisibility(settings.ShowChartCard);
        LifetimeTokensCard.Visibility = ToVisibility(settings.ShowLifetimeTokensCard);
        PeakDailyTokensCard.Visibility = ToVisibility(settings.ShowPeakDailyTokensCard);
        LongestTurnCard.Visibility = ToVisibility(settings.ShowLongestTurnCard);
        CurrentStreakCard.Visibility = ToVisibility(settings.ShowCurrentStreakCard);
        LongestStreakCard.Visibility = ToVisibility(settings.ShowLongestStreakCard);
        StatsRow1.Visibility = ToVisibility(settings.ShowLifetimeTokensCard || settings.ShowPeakDailyTokensCard);
        StatsRow2.Visibility = ToVisibility(settings.ShowLongestTurnCard || settings.ShowCurrentStreakCard);
        StatsRow3.Visibility = ToVisibility(settings.ShowLongestStreakCard);
        expandedHeight = CalculateExpandedHeight();
        if (!isEdgeCollapsed) Height = expandedHeight;
    }

    private void ApplyTheme()
    {
        var dark = settings.ThemeMode.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                   (settings.ThemeMode.Equals("System", StringComparison.OrdinalIgnoreCase) && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is 0);
        var panel = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#25272C" : "#FDFDFE"));
        var card = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#303238" : "#F8F8F9"));
        var text = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#F2F3F5" : "#282B30"));
        var border = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#4C5058" : "#E8E9EC"));
        var buttonBackground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#3B3E45" : "#F2F3F5"));
        var buttonHoverBackground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#484C55" : "#E8EAEE"));
        var buttonPressedBackground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#31343A" : "#DDE0E5"));
        var buttonBorder = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#575B64" : "#E6E7EA"));
        var buttonHoverBorder = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#686D78" : "#D5D7DB"));
        WidgetPanel.Background = panel;
        WidgetPanel.BorderBrush = border;
        foreach (var cardBorder in new[] { QuotaCard, TodayCard, ChartCard, LifetimeTokensCard, PeakDailyTokensCard, LongestTurnCard, CurrentStreakCard, LongestStreakCard }) cardBorder.Background = card;
        Resources["HeaderButtonBackground"] = buttonBackground;
        Resources["HeaderButtonHoverBackground"] = buttonHoverBackground;
        Resources["HeaderButtonPressedBackground"] = buttonPressedBackground;
        Resources["HeaderButtonBorder"] = buttonBorder;
        Resources["HeaderButtonHoverBorder"] = buttonHoverBorder;
        Resources["HeaderButtonForeground"] = text;
        ApplyTextColor(WidgetPanel, text);
    }

    private static void ApplyTextColor(DependencyObject root, System.Windows.Media.Brush color)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock textBlock) textBlock.Foreground = color;
            ApplyTextColor(child, color);
        }
    }
    private void ApplyEdgeBarSize()
    {
        var scale = GetEdgeBarScale();
        var visualWidth = Math.Max(6, Math.Round(8 * scale));
        var visualHeight = Math.Max(58, Math.Round(86 * scale));
        EdgeBar.Width = visualWidth;
        EdgeBar.Height = visualHeight;
        edgeWindowWidth = Math.Max(10, visualWidth + 4);
        edgeWindowHeight = visualHeight + 4;
        edgeTrackHeight = Math.Max(0, visualHeight - 4);
        if (latestSnapshot is not null) UpdateEdgeProgress(latestSnapshot.FiveHourUsedPercent);
    }

    private double GetEdgeBarScale() => Math.Clamp(settings.EdgeBarSizePercent, 67, 133) / 100d;
    private static Visibility ToVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private double CalculateExpandedHeight()
    {
        var height = 16d + 28d + 30d;
        if (settings.ShowQuotaCard) height += 102;
        if (settings.ShowTodayCard) height += 73;
        if (settings.ShowChartCard) height += 159;
        if (settings.ShowLifetimeTokensCard || settings.ShowPeakDailyTokensCard) height += 73;
        if (settings.ShowLongestTurnCard || settings.ShowCurrentStreakCard) height += 73;
        if (settings.ShowLongestStreakCard) height += 73;
        return Math.Clamp(height, MinHeight, MaxHeight);
    }

    private void ConfigureAutoRefresh()
    {
        autoRefreshTimer.Stop();
        if (!settings.AutoRefreshEnabled) return;
        autoRefreshTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(settings.RefreshIntervalMinutes, 1, 60));
        autoRefreshTimer.Start();
    }

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Localization.Text("显示"), null, (_, _) => Dispatcher.BeginInvoke(ShowFromTray));
        menu.Items.Add(Localization.Text("刷新"), null, (_, _) => Dispatcher.BeginInvoke(async () => await RefreshAsync()));
        menu.Items.Add(Localization.Text("设置"), null, (_, _) => Dispatcher.BeginInvoke(() => SettingsButton_Click(this, new RoutedEventArgs())));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Localization.Text("退出"), null, (_, _) => Dispatcher.BeginInvoke(Close));
        return menu;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        trayIcon.ShowBalloonTip(1200, "Codex Token Widget", Localization.Text("已最小化到系统托盘，双击图标可恢复。"), System.Windows.Forms.ToolTipIcon.Info);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowFromTray()
    {
        ExpandFromEdge(true);
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || e.Source is System.Windows.Controls.Button) return;
        if (isEdgeCollapsed) ExpandFromEdge();
        isDragging = true;
        dragStartScreen = PointToScreen(e.GetPosition(this));
        dragStartLeft = Left;
        dragStartTop = Top;
        CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isDragging || e.LeftButton != MouseButtonState.Pressed) return;
        var currentScreen = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = dragStartLeft + (currentScreen.X - dragStartScreen.X) / dpi.DpiScaleX;
        Top = dragStartTop + (currentScreen.Y - dragStartScreen.Y) / dpi.DpiScaleY;
        UpdateEdgePreview();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging) return;
        isDragging = false;
        ReleaseMouseCapture();
        edgePreview.Hide();
        UpdateDockState();
        e.Handled = true;
    }

    private void UpdateEdgePreview()
    {
        if (!isDragging || isEdgeCollapsed) return;
        var workArea = GetCurrentMonitorWorkArea();
        if (!TryGetDockSide(workArea, out var side))
        {
            edgePreview.Hide();
            return;
        }
        edgePreview.ShowAt(workArea, side, Top + ActualHeight / 2, latestSnapshot?.FiveHourUsedPercent ?? 0, GetEdgeBarScale());
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isEdgeCollapsed) return;
        expandedWidth = ActualWidth;
        expandedHeight = ActualHeight;
        if (latestSnapshot is not null) DrawChart(latestSnapshot.LastSevenDays);
    }

    private void AddResizeHitTestHook()
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmNcHitTest || isEdgeCollapsed) return IntPtr.Zero;
        var raw = lParam.ToInt64();
        var screenPoint = new System.Windows.Point((short)(raw & 0xffff), (short)((raw >> 16) & 0xffff));
        var point = PointFromScreen(screenPoint);
        const double grip = 7;
        var left = point.X <= grip;
        var right = point.X >= ActualWidth - grip;
        var top = point.Y <= grip;
        var bottom = point.Y >= ActualHeight - grip;
        var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1;
        if (hit == 1) return IntPtr.Zero;
        handled = true;
        return new IntPtr(hit);
    }
    private void UpdateDockState()
    {
        var workArea = GetCurrentMonitorWorkArea();
        if (!TryGetDockSide(workArea, out dockSide))
        {
            isDocked = false;
            return;
        }
        isDocked = true;
        CollapseToEdge();
    }

    private bool TryGetDockSide(System.Windows.Rect workArea, out Dock side)
    {
        var nearLeft = Left <= workArea.Left + SnapDistance;
        var nearRight = Left + ActualWidth >= workArea.Right - SnapDistance;
        if (!nearLeft && !nearRight)
        {
            side = Dock.Left;
            return false;
        }
        if (nearLeft && nearRight)
        {
            side = Left + ActualWidth / 2 <= workArea.Left + workArea.Width / 2 ? Dock.Left : Dock.Right;
            return true;
        }
        side = nearLeft ? Dock.Left : Dock.Right;
        return true;
    }

    private System.Windows.Rect GetCurrentMonitorWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var pixels = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        return new System.Windows.Rect(pixels.Left / dpi.DpiScaleX, pixels.Top / dpi.DpiScaleY, pixels.Width / dpi.DpiScaleX, pixels.Height / dpi.DpiScaleY);
    }

    private void CollapseToEdge()
    {
        if (!isDocked) return;
        var workArea = GetCurrentMonitorWorkArea();
        var centerY = Top + ActualHeight / 2;
        isEdgeCollapsed = true;
        MinWidth = 0;
        MinHeight = 0;
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WidgetPanel.Visibility = Visibility.Collapsed;
        EdgeBar.Visibility = Visibility.Collapsed;
        Width = edgeWindowWidth;
        Height = edgeWindowHeight;
        Left = dockSide == Dock.Left ? workArea.Left + EdgeInset : workArea.Right - edgeWindowWidth - EdgeInset;
        Top = Math.Clamp(centerY - edgeWindowHeight / 2, workArea.Top + EdgeInset, workArea.Bottom - edgeWindowHeight - EdgeInset);
        Opacity = 0;
        edgePreview.ShowAt(workArea, dockSide, Top + edgeWindowHeight / 2, latestSnapshot?.FiveHourUsedPercent ?? 0, GetEdgeBarScale(), true);
        Dispatcher.BeginInvoke(() =>
        {
            if (!isEdgeCollapsed) return;
            EdgeProgressCanvas.UpdateLayout();
            UpdateEdgeProgress(latestSnapshot?.FiveHourUsedPercent ?? 0);
        }, DispatcherPriority.Render);
    }

    private void ExpandFromEdge(bool showInTaskbar = false)
    {
        if (!isEdgeCollapsed) return;
        var workArea = GetCurrentMonitorWorkArea();
        var centerY = Top + edgeWindowHeight / 2;
        MinWidth = 270;
        MinHeight = 130;
        MaxWidth = 620;
        MaxHeight = 760;
        ResizeMode = ResizeMode.CanResize;
        Width = expandedWidth;
        Height = expandedHeight;
        Left = dockSide == Dock.Left ? workArea.Left + EdgeInset : workArea.Right - expandedWidth - EdgeInset;
        Top = Math.Clamp(centerY - expandedHeight / 2, workArea.Top + EdgeInset, workArea.Bottom - expandedHeight - EdgeInset);
        edgePreview.Hide();
        IsHitTestVisible = true;
        Opacity = 1;
        EdgeBar.Visibility = Visibility.Collapsed;
        WidgetPanel.Visibility = Visibility.Visible;
        ShowInTaskbar = showInTaskbar;
        isEdgeCollapsed = false;
    }

    private void ScheduleEdgeCollapse()
    {
        if (!isDocked || isEdgeCollapsed) return;
        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            if (!IsMouseOver && isDocked) CollapseToEdge();
        };
        delay.Start();
    }

    private void EdgeHitArea_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => ExpandFromEdge();

    private void UpdateEdgeProgress(int percent)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        var trackWidth = EdgeProgressCanvas.ActualWidth > 0 ? EdgeProgressCanvas.ActualWidth : Math.Max(2, EdgeBar.Width - 4);
        var trackHeight = EdgeProgressCanvas.ActualHeight > 0 ? EdgeProgressCanvas.ActualHeight : edgeTrackHeight;
        EdgeProgressFill.Width = trackWidth;
        EdgeProgressFill.Height = normalized == 0 ? 0 : Math.Max(3, trackHeight * normalized / 100d);
        Canvas.SetBottom(EdgeProgressFill, 0);
        Canvas.SetLeft(EdgeProgressFill, 0);
        EdgeProgressFill.Background = CreateUsageBrush(normalized);
        EdgeBar.ToolTip = Localization.Text("5 小时用量：{0}%", normalized);
    }

    private static string FormatCompactNumber(long value) => Localization.FormatNumber(value);
    private static string FormatChineseNumber(long? value) => Localization.FormatNumber(value);
    private static string FormatDuration(long? seconds) => Localization.FormatDuration(seconds);
    private static string FormatDays(long? days) => days is null ? "--" : days == 1 && Localization.IsEnglish ? Localization.Text("一天") : Localization.Text("{0}天", days);
    private static string FormatReset(DateTimeOffset? reset) => reset is null ? "" : reset.Value.LocalDateTime.ToString("M/d HH:mm");

    private static void PaintDots(IEnumerable<Border> dots, int percent)
    {
        var activeCount = (int)Math.Ceiling(Math.Clamp(percent, 0, 100) / 100d * 6);
        var index = 0;
        var activeBrush = CreateUsageBrush(percent);
        foreach (var dot in dots) dot.Background = index++ < activeCount ? activeBrush : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E7EA"));
    }

    private static SolidColorBrush CreateUsageBrush(int percent) => new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(percent > 80 ? "#EF5B5B" : percent > 50 ? "#F4B63F" : "#43C979"));

    private void DrawChart(IReadOnlyList<DailyUsage> days)
    {
        ChartCanvas.Children.Clear();
        const double chartHeight = 78;
        const double gap = 10;
        var availableWidth = Math.Max(240, ChartCanvas.ActualWidth);
        var barWidth = Math.Max(18, (availableWidth - gap * 6) / 7);
        var max = Math.Max(days.Max(day => day.Tokens), 1);
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var height = day.IsReported ? Math.Max(4, chartHeight * day.Tokens / max) : 4;
            var color = !day.IsReported ? "#D8DADD" : index == days.Count - 1 ? "#43C979" : "#9BE2B7";
            var bar = new Border { Width = barWidth, Height = height, CornerRadius = new CornerRadius(4, 4, 1, 1), Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)), ToolTip = day.IsReported ? $"{day.Date:M/d}: {day.Tokens:N0} tokens" : Localization.Text("{0}: 等待后台同步", day.Date.ToString("M/d")) };
            Canvas.SetLeft(bar, index * (barWidth + gap)); Canvas.SetTop(bar, chartHeight - height); ChartCanvas.Children.Add(bar);
            var label = new TextBlock { Text = day.Date.ToString("M/d"), FontSize = 9, Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8F9299")), Width = barWidth, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(label, index * (barWidth + gap)); Canvas.SetTop(label, 86); ChartCanvas.Children.Add(label);
        }
    }

    private static void WriteErrorLog(Exception exception)
    {
        try { var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTokenWidget"); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "error.log"), $"{DateTime.Now:O}{Environment.NewLine}{exception}"); } catch { }
    }
}
