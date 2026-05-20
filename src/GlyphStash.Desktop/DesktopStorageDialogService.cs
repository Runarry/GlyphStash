using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GlyphStash.Localization;
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
            Title = AppText.Get("Dialog.PickFontFilesTitle"),
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(AppText.Get("Common.FontFiles"))
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
            Title = AppText.Get("Dialog.PickManagedDirectoryTitle"),
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickMergeOutputFileAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
        if (TopLevel?.StorageProvider is not { CanSave: true } storageProvider)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppText.Get("Dialog.PickMergeOutputTitle"),
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "GlyphStash-Merged.ttf" : suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType(AppText.Get("Common.FontFiles"))
                {
                    Patterns = ["*.ttf", "*.otf"]
                }
            ]
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickMergeReportFileAsync(CancellationToken cancellationToken)
    {
        if (TopLevel?.StorageProvider is not { CanOpen: true } storageProvider)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppText.Get("Dialog.PickMergeReportTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(AppText.Get("Dialog.MergeReportFileType"))
                {
                    Patterns = ["*.glyphstash-merge-report.json", "*.json"]
                }
            ]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
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
