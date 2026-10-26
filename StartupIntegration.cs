using System.IO;
using Microsoft.Win32;

namespace CodexTokenWidget;

public static class StartupIntegration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexTokenWidget";

    public static void Apply(WidgetSettings settings)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable)) return;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (settings.StartWithWindows) key.SetValue(ValueName, $"\"{executable}\""); else key.DeleteValue(ValueName, throwOnMissingValue: false);
            CreateDesktopShortcut(executable);
        }
        catch { }
    }

    private static void CreateDesktopShortcut(string executable)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Codex Token Widget.lnk");
            if (File.Exists(path)) return;
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = executable;
            shortcut.WorkingDirectory = Path.GetDirectoryName(executable);
            shortcut.IconLocation = executable + ",0";
            shortcut.Save();
        }
        catch { }
    }
}

