using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
public static class WindowsUserDataUninstallCleanup
{
    private const string ClearMarkerFileName = "uninstall-clear-user-data.marker";
    private const string OriginalUninstallStringValue = "GlyphStashOriginalUninstallString";
    private const string OriginalQuietUninstallStringValue = "GlyphStashOriginalQuietUninstallString";
    private const string DisplayNameValue = "DisplayName";
    private const string UninstallStringValue = "UninstallString";
    private const string QuietUninstallStringValue = "QuietUninstallString";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    private const int IdYes = 6;
    private const int IdNo = 7;
    private const int MbYesNoCancel = 0x00000003;
    private const int MbIconQuestion = 0x00000020;
    private const int MbSetForeground = 0x00010000;

    public static string UserDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphStash");

    private static string ClearMarkerPath => Path.Combine(UserDataDirectory, ClearMarkerFileName);

    public static bool TryHandleUninstallPromptCommand(string[] args)
    {
        if (!args.Any(arg => string.Equals(arg, "--glyphstash-uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var clearData = args.Any(arg => string.Equals(arg, "--clear-user-data", StringComparison.OrdinalIgnoreCase));
        var quiet = args.Any(arg => string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(arg, "/quiet", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(arg, "/qn", StringComparison.OrdinalIgnoreCase));

        if (!quiet)
        {
            var answer = MessageBoxW(
                IntPtr.Zero,
                "是否同时清除 GlyphStash 用户数据？\n\n选择“是”会删除字体数据库、日志、缓存和用户配置。\n选择“否”会卸载应用但保留用户数据。\n选择“取消”会取消卸载。",
                "卸载 GlyphStash",
                MbYesNoCancel | MbIconQuestion | MbSetForeground);
            if (answer == IdYes)
            {
                clearData = true;
            }
            else if (answer != IdNo)
            {
                return true;
            }
        }

        if (clearData)
        {
            WriteClearMarker();
        }
        else
        {
            DeleteClearMarker();
        }

        var originalCommand = FindOriginalUninstallCommand(quiet);
        if (string.IsNullOrWhiteSpace(originalCommand))
        {
            MessageBoxW(
                IntPtr.Zero,
                "未找到 GlyphStash 原始卸载命令。请从 Windows 设置或安装包重新尝试卸载。",
                "卸载 GlyphStash",
                MbIconQuestion | MbSetForeground);
            return true;
        }

        StartCommand(originalCommand);
        return true;
    }

    public static void TryInstallInteractiveUninstallPrompt()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        {
            return;
        }

        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var uninstallRoot = root.OpenSubKey(UninstallRegistryPath, writable: true);
                if (uninstallRoot is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    using var key = uninstallRoot.OpenSubKey(subKeyName, writable: true);
                    if (key is null || !IsGlyphStashEntry(key))
                    {
                        continue;
                    }

                    var original = Convert.ToString(key.GetValue(UninstallStringValue)) ?? "";
                    if (string.IsNullOrWhiteSpace(original)
                        || original.Contains("--glyphstash-uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var originalQuiet = Convert.ToString(key.GetValue(QuietUninstallStringValue)) ?? original;
                    key.SetValue(OriginalUninstallStringValue, original, RegistryValueKind.String);
                    key.SetValue(OriginalQuietUninstallStringValue, originalQuiet, RegistryValueKind.String);
                    key.SetValue(UninstallStringValue, $"\"{currentExe}\" --glyphstash-uninstall", RegistryValueKind.String);
                    key.SetValue(QuietUninstallStringValue, $"\"{currentExe}\" --glyphstash-uninstall --quiet", RegistryValueKind.String);
                }
            }
            catch
            {
                // Registry prompt installation is best-effort; default Velopack uninstall still works.
            }
        }
    }

    public static UserDataCleanupResult CleanupUserDataIfRequested() =>
        CleanupUserDataIfRequested(UserDataDirectory, ClearMarkerPath);

    public static UserDataCleanupResult CleanupUserDataIfRequested(string userDataDirectory, string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return new UserDataCleanupResult(false, true, null);
        }

        try
        {
            if (Directory.Exists(userDataDirectory))
            {
                Directory.Delete(userDataDirectory, recursive: true);
            }

            return new UserDataCleanupResult(true, true, null);
        }
        catch (Exception ex)
        {
            return new UserDataCleanupResult(true, false, ex.Message);
        }
        finally
        {
            TryDeleteFile(markerPath);
        }
    }

    private static bool IsGlyphStashEntry(RegistryKey key)
    {
        var displayName = Convert.ToString(key.GetValue(DisplayNameValue)) ?? "";
        return displayName.Contains("GlyphStash", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindOriginalUninstallCommand(bool quiet)
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var uninstallRoot = root.OpenSubKey(UninstallRegistryPath, writable: false);
                if (uninstallRoot is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    using var key = uninstallRoot.OpenSubKey(subKeyName, writable: false);
                    if (key is null || !IsGlyphStashEntry(key))
                    {
                        continue;
                    }

                    var valueName = quiet ? OriginalQuietUninstallStringValue : OriginalUninstallStringValue;
                    var command = Convert.ToString(key.GetValue(valueName));
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        return command;
                    }
                }
            }
            catch
            {
                // Try the next registry root.
            }
        }

        return null;
    }

    private static void WriteClearMarker()
    {
        Directory.CreateDirectory(UserDataDirectory);
        File.WriteAllText(ClearMarkerPath, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static void DeleteClearMarker() => TryDeleteFile(ClearMarkerPath);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Marker cleanup must not block uninstall.
        }
    }

    private static void StartCommand(string command)
    {
        var (fileName, arguments) = SplitCommand(command);
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return (command[1..endQuote], command[(endQuote + 1)..].Trim());
            }
        }

        var firstSpace = command.IndexOf(' ', StringComparison.Ordinal);
        return firstSpace < 0
            ? (command, "")
            : (command[..firstSpace], command[(firstSpace + 1)..].Trim());
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, int type);
}

public sealed record UserDataCleanupResult(
    bool Requested,
    bool Succeeded,
    string? ErrorMessage);
