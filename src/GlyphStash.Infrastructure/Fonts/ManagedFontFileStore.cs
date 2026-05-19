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

    public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ManagedFontDirectory) || !Directory.Exists(settings.ManagedFontDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(settings.ManagedFontDirectory, "*.*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
