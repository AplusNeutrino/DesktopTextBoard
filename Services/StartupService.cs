using Microsoft.Win32;

namespace DesktopTextBoard.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.StartupRegistryValueName;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return HasStartupValue(key, ValueName)
            || HasStartupValue(key, AppInfo.LegacyStartupRegistryValueName);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            key.DeleteValue(AppInfo.LegacyStartupRegistryValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            key.DeleteValue(AppInfo.LegacyStartupRegistryValueName, throwOnMissingValue: false);
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
    }

    private static bool HasStartupValue(RegistryKey? key, string valueName)
    {
        return key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value);
    }
}
