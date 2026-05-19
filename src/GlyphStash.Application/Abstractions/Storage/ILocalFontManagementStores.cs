using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Storage;

public interface IAppSettingsStore
{
    Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken);

    Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken);
}

public interface IFontLibraryMutationStore
{
    Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken);

    Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken);

    Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken);

    Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken);
}

public interface ITagStore
{
    Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken);

    Task CreateTagAsync(string name, CancellationToken cancellationToken);

    Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task DeleteTagAsync(string name, CancellationToken cancellationToken);
}

public interface ICollectionStore
{
    Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken);

    Task CreateCollectionAsync(string name, CancellationToken cancellationToken);

    Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken);

    Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken);

    Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken);
}

public interface IActivationStore
{
    Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken);

    Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken);

    Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken);

    Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken);
}

public interface IOperationLogStore
{
    Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken);
}
