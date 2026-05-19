using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed class LocalFontManagementService
{
    private static readonly string[] InstallableExtensions = [".ttf", ".otf", ".ttc", ".otc"];
    private static readonly string[] RecognizedButUnsupportedExtensions = [".woff", ".woff2"];

    private readonly IFontMetadataReader _metadataReader;
    private readonly IManagedFontFileStore _fileStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IFontMetadataStore _metadataStore;
    private readonly IFontLibraryMutationStore _mutationStore;
    private readonly ITagStore _tagStore;
    private readonly ICollectionStore _collectionStore;
    private readonly IFontInstallService _installService;
    private readonly FontActivationCoordinator _activationCoordinator;
    private readonly IOperationLogStore _operationLogStore;

    public LocalFontManagementService(
        IFontMetadataReader metadataReader,
        IManagedFontFileStore fileStore,
        IAppSettingsStore settingsStore,
        IFontMetadataStore metadataStore,
        IFontLibraryMutationStore mutationStore,
        ITagStore tagStore,
        ICollectionStore collectionStore,
        IFontInstallService installService,
        FontActivationCoordinator activationCoordinator,
        IOperationLogStore operationLogStore)
    {
        _metadataReader = metadataReader;
        _fileStore = fileStore;
        _settingsStore = settingsStore;
        _metadataStore = metadataStore;
        _mutationStore = mutationStore;
        _tagStore = tagStore;
        _collectionStore = collectionStore;
        _installService = installService;
        _activationCoordinator = activationCoordinator;
        _operationLogStore = operationLogStore;
    }

    public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        _settingsStore.GetSettingsAsync(cancellationToken);

    public async Task SaveManagedDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("需要先选择 GlyphStash 管理目录。");
        }

        await _settingsStore.SaveSettingsAsync(new UserFontSettings(directory), cancellationToken).ConfigureAwait(false);
        await LogAsync("settings", "managed-directory", $"管理目录已更新：{directory}", directory, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FontImportPreview> PreviewImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken)
    {
        var existing = await _metadataStore.SearchAsync(new FontSearchQuery(), cancellationToken).ConfigureAwait(false);
        var existingHashes = existing
            .SelectMany(font => font.Faces)
            .Select(face => face.File.Sha256)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<FontImportPreviewItem>();
        foreach (var sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(sourcePath);
            var format = extension.TrimStart('.').ToUpperInvariant();
            var fileName = Path.GetFileName(sourcePath);

            if (!File.Exists(sourcePath))
            {
                items.Add(Failed(sourcePath, fileName, format, "文件不存在"));
                continue;
            }

            if (RecognizedButUnsupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(Failed(sourcePath, fileName, format, "M2 仅识别 WOFF/WOFF2；Windows 本地安装和临时启用暂不支持该格式。"));
                continue;
            }

            if (!InstallableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(Failed(sourcePath, fileName, format, "不支持的字体格式"));
                continue;
            }

            try
            {
                var metadata = await _metadataReader.ReadMetadataAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                var duplicate = existingHashes.Contains(metadata.Sha256);
                items.Add(new FontImportPreviewItem(
                    sourcePath,
                    fileName,
                    metadata.Format,
                    metadata.FamilyName,
                    metadata.SubfamilyName,
                    metadata.FullName,
                    metadata.PostScriptName,
                    metadata.Version,
                    metadata.Manufacturer,
                    metadata.LicenseText,
                    metadata.Sha256,
                    true,
                    true,
                    true,
                    duplicate ? "重复字体，可导入但会复用相同文件 hash" : "可导入"));
            }
            catch (Exception ex)
            {
                items.Add(Failed(sourcePath, fileName, format, BuildErrorMessage(ex)));
            }
        }

        return new FontImportPreview(items);
    }

    public async Task<FontImportResult> ImportAsync(FontImportPreview preview, FontImportOptions options, CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("需要先选择 GlyphStash 管理目录。");

        var result = new List<FontImportResultItem>();
        foreach (var item in preview.Items.Where(item => item.CanImport))
        {
            try
            {
                var copy = await _fileStore.CopyToManagedDirectoryAsync(item.SourcePath, settings, cancellationToken).ConfigureAwait(false);
                var installResult = options.InstallForCurrentUser
                    ? await _installService.InstallForCurrentUserAsync(new FontFileRef(copy.ManagedPath, item.Format, copy.Sha256), cancellationToken).ConfigureAwait(false)
                    : new FontInstallResult(true, copy.ManagedPath, null, "未选择用户级安装。");
                var visiblePath = installResult is { Succeeded: true, InstalledPath: not null } ? installResult.InstalledPath : copy.ManagedPath;
                var visibleSource = installResult is { Succeeded: true, InstalledPath: not null } ? FontSourceKind.UserInstalled : FontSourceKind.GlyphStashManaged;
                var file = new FontFileRecord(visiblePath, item.Format, copy.Sha256, visibleSource, DateTimeOffset.UtcNow);
                var face = new FontFaceRecord(
                    item.FamilyName,
                    string.IsNullOrWhiteSpace(item.SubfamilyName) ? "Regular" : item.SubfamilyName,
                    string.IsNullOrWhiteSpace(item.FullName) ? item.FamilyName : item.FullName,
                    string.IsNullOrWhiteSpace(item.PostScriptName) ? item.FamilyName.Replace(' ', '-') : item.PostScriptName,
                    400,
                    "Normal",
                    item.SubfamilyName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ? "Italic" : "Normal",
                    file);
                var shouldTemporarilyActivate = !options.InstallForCurrentUser && options.TemporarilyActivate;
                if (shouldTemporarilyActivate)
                {
                    await _activationCoordinator.ActivateAsync($"font:{item.FamilyName}", [new FontFileRef(copy.ManagedPath, item.Format, copy.Sha256)], cancellationToken)
                        .ConfigureAwait(false);
                }

                var state = shouldTemporarilyActivate
                    ? FontActivationState.TemporarilyEnabled
                    : options.InstallForCurrentUser && installResult.Succeeded
                        ? FontActivationState.Installed
                        : FontActivationState.NotEnabled;
                var family = new FontFamilyRecord(
                    item.FamilyName,
                    [face],
                    visibleSource,
                    state,
                    string.IsNullOrWhiteSpace(item.LicenseText) ? LicenseStatus.Unknown : LicenseStatus.Known,
                    string.IsNullOrWhiteSpace(item.LicenseText) ? "未知授权" : item.LicenseText,
                    options.Tags,
                    options.Collections,
                    false);
                await _mutationStore.UpsertManagedFontAsync(
                    new ManagedFontRecord(
                        item.FamilyName,
                        copy.ManagedPath,
                        item.Format,
                        copy.Sha256,
                        installResult.Succeeded ? installResult.InstalledPath : null,
                        state,
                        DateTimeOffset.UtcNow,
                        installResult is { Succeeded: true, InstalledPath: not null } ? DateTimeOffset.UtcNow : null),
                    family with { ActivationState = state },
                    cancellationToken).ConfigureAwait(false);

                foreach (var collection in options.Collections.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.CurrentCultureIgnoreCase))
                {
                    await _collectionStore.CreateCollectionAsync(collection, cancellationToken).ConfigureAwait(false);
                    await _collectionStore.AddFontToCollectionAsync(collection, item.FamilyName, cancellationToken).ConfigureAwait(false);
                }

                result.Add(new FontImportResultItem(item, true, copy.ManagedPath, installResult.Message));
                await LogAsync("import", "import-font", $"已导入字体：{item.FamilyName}", copy.ManagedPath, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var message = BuildErrorMessage(ex);
                result.Add(new FontImportResultItem(item, false, null, message));
                await LogAsync("import", "import-font", message, item.SourcePath, false, cancellationToken).ConfigureAwait(false);
            }
        }

        return new FontImportResult(result);
    }

    public async Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken)
    {
        await _mutationStore.SetFavoriteAsync(familyName, isFavorite, cancellationToken).ConfigureAwait(false);
        await LogAsync("library", "favorite", isFavorite ? $"已收藏：{familyName}" : $"已取消收藏：{familyName}", familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        await _mutationStore.SetTagsAsync(familyName, tags, cancellationToken).ConfigureAwait(false);
        await LogAsync("tags", "set-tags", $"已更新标签：{familyName}", familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
        _tagStore.GetTagsAsync(cancellationToken);

    public async Task CreateTagAsync(string name, CancellationToken cancellationToken)
    {
        await _tagStore.CreateTagAsync(name, cancellationToken).ConfigureAwait(false);
        await LogAsync("tags", "create", $"已创建标签：{name}", name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await _tagStore.RenameTagAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        await LogAsync("tags", "rename", $"已重命名标签：{oldName} -> {newName}", oldName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTagAsync(string name, CancellationToken cancellationToken)
    {
        await _tagStore.DeleteTagAsync(name, cancellationToken).ConfigureAwait(false);
        await LogAsync("tags", "delete", $"已删除标签：{name}", name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken)
    {
        await _mutationStore.SetCollectionsAsync(familyName, collections, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "set-collections", $"已更新集合关联：{familyName}", familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
        _collectionStore.GetCollectionsAsync(cancellationToken);

    public async Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await _collectionStore.CreateCollectionAsync(name, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "create", $"已创建集合：{name}", name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await _collectionStore.RenameCollectionAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "rename", $"已重命名集合：{oldName} -> {newName}", oldName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await _collectionStore.DeleteCollectionAsync(name, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "delete", $"已删除集合：{name}", name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await _collectionStore.AddFontToCollectionAsync(collectionName, familyName, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "add-font", $"已加入集合：{familyName} -> {collectionName}", familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await _collectionStore.RemoveFontFromCollectionAsync(collectionName, familyName, cancellationToken).ConfigureAwait(false);
        await LogAsync("collection", "remove-font", $"已从集合移除：{familyName} -> {collectionName}", familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ActivationResult> ActivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.ActivationState == FontActivationState.Installed)
        {
            var result = new ActivationResult(false, [], "已安装，无需临时启用。");
            await LogAsync("activation", "activate-blocked", result.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return result;
        }

        return await _activationCoordinator.ActivateAsync(ownerKey, ToPhysicalFiles(font), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ActivationResult> DeactivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
        await _activationCoordinator.DeactivateAsync(ownerKey, ToPhysicalFiles(font), cancellationToken).ConfigureAwait(false);

    public Task RecordTemporaryActivationSkippedAsync(string ownerKey, FontFamilyRecord font, string reason, CancellationToken cancellationToken) =>
        LogAsync("activation", "activate-skipped", $"{font.FamilyName}：{reason}", ownerKey, true, cancellationToken);

    public async Task<FontInstallResult> InstallFontAsync(FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.SourceKind == FontSourceKind.System)
        {
            var blocked = new FontInstallResult(false, font.PrimaryFilePath, null, "系统字体已安装，无需执行用户级安装。");
            await LogAsync("install", "install", blocked.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return blocked;
        }

        var file = ToPhysicalFiles(font).First();
        var result = await _installService.InstallForCurrentUserAsync(file, cancellationToken).ConfigureAwait(false);
        await LogAsync("install", "install", result.Message, font.FamilyName, result.Succeeded, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<FontUninstallResult> UninstallManagedFontAsync(FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.SourceKind == FontSourceKind.System)
        {
            var blocked = new FontUninstallResult(false, font.PrimaryFilePath, "系统字体在 v1 中不支持卸载。");
            await LogAsync("install", "uninstall", blocked.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return blocked;
        }

        var file = font.Faces.Select(face => face.File).FirstOrDefault(file => file.Path.Contains("Microsoft\\Windows\\Fonts", StringComparison.OrdinalIgnoreCase))
            ?? font.Faces.Select(face => face.File).FirstOrDefault();
        if (file is null)
        {
            return new FontUninstallResult(false, "", "未找到可卸载字体文件。");
        }

        var result = await _installService.UninstallManagedFontAsync(
            new ManagedFontRecord(font.FamilyName, file.Path, file.Format, file.Sha256, file.Path, FontActivationState.NotEnabled, file.LastSeenAt, null),
            cancellationToken).ConfigureAwait(false);
        await LogAsync("install", "uninstall", result.Message, font.FamilyName, result.Succeeded, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
        _operationLogStore.GetRecentOperationsAsync(limit, cancellationToken);

    private static IReadOnlyList<FontFileRef> ToPhysicalFiles(FontFamilyRecord font)
    {
        var refs = font.Faces
            .Select(face => new FontFileRef(face.File.Path, face.File.Format, face.File.Sha256))
            .Where(fontFile => fontFile.HasPhysicalPath)
            .DistinctBy(fontFile => fontFile.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (refs.Count == 0)
        {
            throw new InvalidOperationException("该字体没有可用于临时启用的本地文件路径。");
        }

        return refs;
    }

    private static FontImportPreviewItem Failed(string sourcePath, string fileName, string format, string message) =>
        new(sourcePath, fileName, format, "", "", "", "", null, null, null, null, false, false, false, "不可导入", message);

    private Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken) =>
        _operationLogStore.AppendOperationAsync(new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded), cancellationToken);

    private static string BuildErrorMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
    }
}
