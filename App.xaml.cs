using System.IO;
using System.Diagnostics;
using System.Windows.Threading;

namespace CodexTokenWidget;

public partial class App : System.Windows.Application
{
    private DispatcherTimer? codexWatcher;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => { try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTokenWidget", "crash.log"), $"{DateTime.Now:O}{Environment.NewLine}{args.Exception}{Environment.NewLine}"); } catch { } args.Handled = true; };
        base.OnStartup(e);
        try
        {
            var settings = WidgetSettings.Load();
            Localization.Apply(settings.LanguageMode);
            StartupIntegration.Apply(settings);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (settings.StartWithCodex && !IsCodexRunning())
            {
                window.Hide();
                codexWatcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                codexWatcher.Tick += (_, _) => { if (!IsCodexRunning()) return; codexWatcher.Stop(); window.Show(); };
                codexWatcher.Start();
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(exception.ToString(), Localization.Text("Codex Token Widget 启动失败"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static bool IsCodexRunning() => Process.GetProcesses().Any(process => process.ProcessName.Contains("Codex", StringComparison.OrdinalIgnoreCase));
}
