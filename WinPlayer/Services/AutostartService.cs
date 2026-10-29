using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace WinPlayer.Services;

public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinPlayer";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            key.SetValue(ValueName, Quote(GetExecutablePath()));
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool Toggle()
    {
        var next = !IsEnabled();
        SetEnabled(next);
        return next;
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath
                   ?? Process.GetCurrentProcess().MainModule?.FileName
                   ?? Path.Combine(AppContext.BaseDirectory, "WinPlayer.exe");
        return Path.GetFullPath(path);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
