using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GlyphStash.Presentation.Services;

namespace GlyphStash.Desktop;

public sealed class DesktopStorageDialogService : IUserFileDialogService, IUserClipboardService
{
    public TopLevel? TopLevel { get; set; }

    public async Task<IReadOnlyList<string>> PickFontFilesAsync(CancellationToken cancellationToken)
    {
        if (TopLevel?.StorageProvider is not { CanOpen: true } storageProvider)
        {
            return [];
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择字体文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("字体文件")
                {
                    Patterns = ["*.ttf", "*.otf", "*.ttc", "*.otc", "*.woff", "*.woff2"]
                }
            ]
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
    }

    public async Task<string?> PickManagedDirectoryAsync(CancellationToken cancellationToken)
    {
        if (TopLevel?.StorageProvider is not { CanPickFolder: true } storageProvider)
        {
            return null;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 GlyphStash 管理目录",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        if (TopLevel?.Clipboard is null)
        {
            return;
        }

        await TopLevel.Clipboard.SetTextAsync(text);
    }
}
