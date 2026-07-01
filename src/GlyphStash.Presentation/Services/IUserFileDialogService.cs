namespace GlyphStash.Presentation.Services;

public interface IUserFileDialogService
{
    Task<IReadOnlyList<string>> PickFontFilesAsync(CancellationToken cancellationToken);

    Task<string?> PickMergeInputFontFileAsync(string title, CancellationToken cancellationToken);

    Task<string?> PickManagedDirectoryAsync(CancellationToken cancellationToken);

    Task<string?> PickMergeOutputFileAsync(string suggestedFileName, CancellationToken cancellationToken);

    Task<string?> PickMergeReportFileAsync(CancellationToken cancellationToken);
}

public interface IUserClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}

public sealed class NullUserFileDialogService : IUserFileDialogService, IUserClipboardService
{
    public static NullUserFileDialogService Instance { get; } = new();

    private NullUserFileDialogService()
    {
    }

    public Task<IReadOnlyList<string>> PickFontFilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string?> PickMergeInputFontFileAsync(string title, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickManagedDirectoryAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickMergeOutputFileAsync(string suggestedFileName, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickMergeReportFileAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task SetTextAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;
}
