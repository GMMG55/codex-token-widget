using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace CodexTokenWidget;

public partial class SettingsWindow : Window
{
    public WidgetSettings Settings { get; }

    public SettingsWindow(WidgetSettings settings)
    {
        InitializeComponent();
        VersionText.Text = $"版本 {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "未知"}";
        Settings = settings;
        EnableAutoRefreshCheckBox.IsChecked = settings.AutoRefreshEnabled;
        ShowQuotaCardCheckBox.IsChecked = settings.ShowQuotaCard;
        ShowTodayCardCheckBox.IsChecked = settings.ShowTodayCard;
        ShowChartCardCheckBox.IsChecked = settings.ShowChartCard;
        ShowLifetimeTokensCardCheckBox.IsChecked = settings.ShowLifetimeTokensCard;
        ShowPeakDailyTokensCardCheckBox.IsChecked = settings.ShowPeakDailyTokensCard;
        ShowLongestTurnCardCheckBox.IsChecked = settings.ShowLongestTurnCard;
        ShowCurrentStreakCardCheckBox.IsChecked = settings.ShowCurrentStreakCard;
        ShowLongestStreakCardCheckBox.IsChecked = settings.ShowLongestStreakCard;
        SelectInterval(settings.RefreshIntervalMinutes);
        SelectEdgeBarSize(settings.EdgeBarSizePercent);
        SelectThemeMode(settings.ThemeMode);
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        StartWithCodexCheckBox.IsChecked = settings.StartWithCodex;
        UpdateIntervalState();
    }

    private void SelectInterval(int minutes)
    {
        foreach (ComboBoxItem item in IntervalComboBox.Items)
        {
            if (item.Tag?.ToString() != minutes.ToString()) continue;
            IntervalComboBox.SelectedItem = item;
            return;
        }
        IntervalComboBox.SelectedIndex = 0;
    }

    private void SelectEdgeBarSize(int percent)
    {
        foreach (ComboBoxItem item in EdgeBarSizeComboBox.Items)
        {
            if (item.Tag?.ToString() != percent.ToString()) continue;
            EdgeBarSizeComboBox.SelectedItem = item;
            return;
        }
        EdgeBarSizeComboBox.SelectedIndex = 0;
    }
    private void SelectThemeMode(string themeMode)
    {
        foreach (ComboBoxItem item in ThemeModeComboBox.Items)
        {
            if (!string.Equals(item.Tag?.ToString(), themeMode, StringComparison.OrdinalIgnoreCase)) continue;
            ThemeModeComboBox.SelectedItem = item;
            return;
        }
        ThemeModeComboBox.SelectedIndex = 0;
    }
    private void EnableAutoRefreshCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateIntervalState();

    private void UpdateIntervalState()
    {
        if (IntervalComboBox is not null)
            IntervalComboBox.IsEnabled = EnableAutoRefreshCheckBox.IsChecked == true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.AutoRefreshEnabled = EnableAutoRefreshCheckBox.IsChecked == true;
        Settings.RefreshIntervalMinutes = int.TryParse((IntervalComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var minutes) ? minutes : 1;
        Settings.EdgeBarSizePercent = int.TryParse((EdgeBarSizeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var edgeBarSize) ? edgeBarSize : 67;
        Settings.ThemeMode = (ThemeModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        Settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        Settings.StartWithCodex = StartWithCodexCheckBox.IsChecked == true;
        Settings.ShowQuotaCard = ShowQuotaCardCheckBox.IsChecked == true;
        Settings.ShowTodayCard = ShowTodayCardCheckBox.IsChecked == true;
        Settings.ShowChartCard = ShowChartCardCheckBox.IsChecked == true;
        Settings.ShowLifetimeTokensCard = ShowLifetimeTokensCardCheckBox.IsChecked == true;
        Settings.ShowPeakDailyTokensCard = ShowPeakDailyTokensCardCheckBox.IsChecked == true;
        Settings.ShowLongestTurnCard = ShowLongestTurnCardCheckBox.IsChecked == true;
        Settings.ShowCurrentStreakCard = ShowCurrentStreakCardCheckBox.IsChecked == true;
        Settings.ShowLongestStreakCard = ShowLongestStreakCardCheckBox.IsChecked == true;
        Settings.ShowStatisticsCard = Settings.ShowLifetimeTokensCard || Settings.ShowPeakDailyTokensCard || Settings.ShowLongestTurnCard || Settings.ShowCurrentStreakCard || Settings.ShowLongestStreakCard;
        Settings.Save();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}



