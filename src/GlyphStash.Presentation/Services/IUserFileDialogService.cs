namespace GlyphStash.Presentation.Services;

public interface IUserFileDialogService
{
    Task<IReadOnlyList<string>> PickFontFilesAsync(CancellationToken cancellationToken);

    Task<string?> PickManagedDirectoryAsync(CancellationToken cancellationToken);
}

public sealed class NullUserFileDialogService : IUserFileDialogService
{
    public static NullUserFileDialogService Instance { get; } = new();

    private NullUserFileDialogService()
    {
    }

    public Task<IReadOnlyList<string>> PickFontFilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string?> PickManagedDirectoryAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
