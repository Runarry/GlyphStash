using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using Microsoft.Win32;

namespace GlyphStash.Platform.Windows.Fonts;

[SupportedOSPlatform("windows")]
public sealed class WindowsFontInstallService : IFontInstallService
{
    private const int PublicSessionFlags = 0;
    private const string FontsRegistryPath = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";

    private readonly IWindowsFontApi _fontApi;

    public WindowsFontInstallService(IWindowsFontApi fontApi)
    {
        _fontApi = fontApi;
    }

    public Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!fontFile.HasPhysicalPath || !File.Exists(fontFile.Path))
        {
            return Task.FromResult(new FontInstallResult(false, fontFile.Path, null, "字体文件不存在或不是本地物理路径。"));
        }

        var userFontsDirectory = GetUserFontsDirectory();
        Directory.CreateDirectory(userFontsDirectory);
        var destination = BuildInstalledFontPath(userFontsDirectory, fontFile);
        if (!File.Exists(destination))
        {
            File.Copy(fontFile.Path, destination, overwrite: false);
        }

        using var key = Registry.CurrentUser.CreateSubKey(FontsRegistryPath, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户字体注册表。");
        var valueName = BuildRegistryValueName(destination, fontFile.Format);
        key.SetValue(valueName, destination, RegistryValueKind.String);

        var loaded = _fontApi.AddFontResourceEx(destination, PublicSessionFlags);
        _fontApi.BroadcastFontChange();
        return Task.FromResult(loaded > 0
            ? new FontInstallResult(true, fontFile.Path, destination, "字体已安装到当前用户。")
            : new FontInstallResult(false, fontFile.Path, destination, "字体文件已复制并写入注册表，但 Windows 未立即加载该字体。"));
    }

    public Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var installedPath = managedFont.InstalledFilePath ?? managedFont.ManagedFilePath;
        if (string.IsNullOrWhiteSpace(installedPath))
        {
            return Task.FromResult(new FontUninstallResult(false, managedFont.ManagedFilePath, "未找到已安装字体路径。"));
        }

        RemoveRegistryValues(installedPath);
        _ = _fontApi.RemoveFontResourceEx(installedPath, PublicSessionFlags);
        _fontApi.BroadcastFontChange();

        var deleted = false;
        if (IsGlyphStashUserFontCopy(installedPath) && File.Exists(installedPath))
        {
            File.Delete(installedPath);
            deleted = true;
        }

        return Task.FromResult(new FontUninstallResult(true, installedPath, deleted ? "字体已从当前用户安装位置卸载并删除 GlyphStash 副本。" : "字体已从当前用户字体注册表卸载。"));
    }

    private static string GetUserFontsDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");

    private static string BuildInstalledFontPath(string userFontsDirectory, FontFileRef fontFile)
    {
        var prefix = string.IsNullOrWhiteSpace(fontFile.Sha256) ? Guid.NewGuid().ToString("N")[..12] : fontFile.Sha256[..Math.Min(12, fontFile.Sha256.Length)];
        return Path.Combine(userFontsDirectory, $"GlyphStash-{prefix}-{Path.GetFileName(fontFile.Path)}");
    }

    private static string BuildRegistryValueName(string installedPath, string format)
    {
        var kind = string.Equals(format, "OTF", StringComparison.OrdinalIgnoreCase) ? "OpenType" : "TrueType";
        return $"{Path.GetFileNameWithoutExtension(installedPath)} ({kind})";
    }

    private static void RemoveRegistryValues(string installedPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(FontsRegistryPath, writable: true);
        if (key is null)
        {
            return;
        }

        foreach (var valueName in key.GetValueNames())
        {
            var value = Convert.ToString(key.GetValue(valueName)) ?? string.Empty;
            if (string.Equals(value, installedPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, Path.GetFileName(installedPath), StringComparison.OrdinalIgnoreCase)
                || valueName.StartsWith(Path.GetFileNameWithoutExtension(installedPath), StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
    }

    private static bool IsGlyphStashUserFontCopy(string installedPath)
    {
        var userFonts = GetUserFontsDirectory();
        return installedPath.StartsWith(userFonts, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(installedPath).StartsWith("GlyphStash-", StringComparison.OrdinalIgnoreCase);
    }
}
