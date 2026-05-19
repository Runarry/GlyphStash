using System.Security.Cryptography;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Fonts;

public sealed class ManagedFontFileStore : IManagedFontFileStore
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf", ".ttc", ".otc"];

    public async Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ManagedFontDirectory))
        {
            throw new InvalidOperationException("需要先选择 GlyphStash 管理目录。");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("字体文件不存在。", sourcePath);
        }

        if (!IsSupportedFontPath(sourcePath))
        {
            throw new InvalidOperationException("当前字体库不支持该格式导入，仅支持 TTF、OTF、TTC、OTC。");
        }

        Directory.CreateDirectory(settings.ManagedFontDirectory);
        var sha256 = await ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false);
        var fileName = $"{sha256[..12]}-{SanitizeFileName(Path.GetFileName(sourcePath))}";
        var destination = Path.Combine(settings.ManagedFontDirectory, fileName);
        var alreadyExists = File.Exists(destination);
        if (!alreadyExists)
        {
            await using var source = File.OpenRead(sourcePath);
            await using var target = File.Create(destination);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }

        return new ManagedFontCopyResult(destination, sha256, alreadyExists);
    }

    public async Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ManagedFontDirectory) || !Directory.Exists(settings.ManagedFontDirectory))
        {
            return [];
        }

        var files = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(settings.ManagedFontDirectory, "*.*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsSupportedFontPath(path))
                {
                    files.Add(path);
                }
            }

            await RecoverLegacyDownloadedFontsAsync(settings.ManagedFontDirectory, files, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task RecoverLegacyDownloadedFontsAsync(string managedDirectory, List<string> files, CancellationToken cancellationToken)
    {
        var legacyDirectory = Path.Combine(managedDirectory, ".downloads");
        if (!Directory.Exists(legacyDirectory))
        {
            return;
        }

        foreach (var legacyPath in Directory.EnumerateFiles(legacyDirectory, "*.*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSupportedFontPath(legacyPath))
            {
                continue;
            }

            try
            {
                var sha256 = await ComputeSha256Async(legacyPath, cancellationToken).ConfigureAwait(false);
                var destination = Path.Combine(managedDirectory, $"{sha256[..12]}-{SanitizeFileName(Path.GetFileName(legacyPath))}");
                if (!File.Exists(destination))
                {
                    File.Copy(legacyPath, destination);
                }

                files.Add(destination);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsSupportedFontPath(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
