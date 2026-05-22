using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Application.Fonts;

public sealed class LocalFontManagementService : ILocalFontManagementService
{
    private readonly ILocalFontSettingsService _settingsService;
    private readonly ILocalFontImportService _importService;
    private readonly ILocalFontOrganizationService _organizationService;
    private readonly ILocalFontActivationService _activationService;
    private readonly ILocalFontOperationLogService _operationLogService;

    public LocalFontManagementService(
        ILocalFontSettingsService settingsService,
        ILocalFontImportService importService,
        ILocalFontOrganizationService organizationService,
        ILocalFontActivationService activationService,
        ILocalFontOperationLogService operationLogService)
    {
        _settingsService = settingsService;
        _importService = importService;
        _organizationService = organizationService;
        _activationService = activationService;
        _operationLogService = operationLogService;
    }

    public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        _settingsService.GetSettingsAsync(cancellationToken);

    public Task SaveManagedDirectoryAsync(string directory, CancellationToken cancellationToken) =>
        _settingsService.SaveManagedDirectoryAsync(directory, cancellationToken);

    public Task SaveGoogleFontsApiKeyAsync(string apiKey, CancellationToken cancellationToken) =>
        _settingsService.SaveGoogleFontsApiKeyAsync(apiKey, cancellationToken);

    public Task SaveUiCultureAsync(string cultureCode, CancellationToken cancellationToken) =>
        _settingsService.SaveUiCultureAsync(cultureCode, cancellationToken);

    public Task<FontImportPreview> PreviewImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken) =>
        _importService.PreviewImportAsync(sourcePaths, cancellationToken);

    public Task<FontImportResult> ImportAsync(FontImportPreview preview, FontImportOptions options, CancellationToken cancellationToken) =>
        _importService.ImportAsync(preview, options, cancellationToken);

    public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) =>
        _organizationService.SetFavoriteAsync(familyName, isFavorite, cancellationToken);

    public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken) =>
        _organizationService.SetTagsAsync(familyName, tags, cancellationToken);

    public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
        _organizationService.GetTagsAsync(cancellationToken);

    public Task CreateTagAsync(string name, CancellationToken cancellationToken) =>
        _organizationService.CreateTagAsync(name, cancellationToken);

    public Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken) =>
        _organizationService.RenameTagAsync(oldName, newName, cancellationToken);

    public Task DeleteTagAsync(string name, CancellationToken cancellationToken) =>
        _organizationService.DeleteTagAsync(name, cancellationToken);

    public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken) =>
        _organizationService.SetCollectionsAsync(familyName, collections, cancellationToken);

    public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
        _organizationService.GetCollectionsAsync(cancellationToken);

    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken) =>
        _organizationService.CreateCollectionAsync(name, cancellationToken);

    public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) =>
        _organizationService.RenameCollectionAsync(oldName, newName, cancellationToken);

    public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken) =>
        _organizationService.DeleteCollectionAsync(name, cancellationToken);

    public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) =>
        _organizationService.AddFontToCollectionAsync(collectionName, familyName, cancellationToken);

    public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) =>
        _organizationService.RemoveFontFromCollectionAsync(collectionName, familyName, cancellationToken);

    public Task<ActivationResult> ActivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
        _activationService.ActivateFontAsync(ownerKey, font, cancellationToken);

    public Task<ActivationResult> DeactivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
        _activationService.DeactivateFontAsync(ownerKey, font, cancellationToken);

    public Task RecordTemporaryActivationSkippedAsync(string ownerKey, FontFamilyRecord font, string reason, CancellationToken cancellationToken) =>
        _activationService.RecordTemporaryActivationSkippedAsync(ownerKey, font, reason, cancellationToken);

    public Task<FontInstallResult> InstallFontAsync(FontFamilyRecord font, CancellationToken cancellationToken) =>
        _activationService.InstallFontAsync(font, cancellationToken);

    public Task<FontUninstallResult> UninstallManagedFontAsync(FontFamilyRecord font, CancellationToken cancellationToken) =>
        _activationService.UninstallManagedFontAsync(font, cancellationToken);

    public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
        _operationLogService.GetRecentOperationsAsync(limit, cancellationToken);
}

public sealed class LocalFontSettingsService : ILocalFontSettingsService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IOperationLogger _operationLogger;

    public LocalFontSettingsService(IAppSettingsStore settingsStore, IOperationLogger operationLogger)
    {
        _settingsStore = settingsStore;
        _operationLogger = operationLogger;
    }

    public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        _settingsStore.GetSettingsAsync(cancellationToken);

    public async Task SaveManagedDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(L("需要先选择 GlyphStash 管理目录。"));
        }

        var existing = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        await _settingsStore.SaveSettingsAsync(new UserFontSettings(directory, existing?.GoogleFontsApiKey ?? "", existing?.UiCultureCode ?? ""), cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("settings", "managed-directory", AppText.Format("Log.ManagedDirectoryUpdatedFormat", directory), directory, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveGoogleFontsApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        var existing = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        await _settingsStore.SaveSettingsAsync(new UserFontSettings(existing?.ManagedFontDirectory ?? "", apiKey.Trim(), existing?.UiCultureCode ?? ""), cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("settings", "google-fonts-api-key", AppText.Get("Log.GoogleFontsApiKeyUpdated"), "google-fonts", true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveUiCultureAsync(string cultureCode, CancellationToken cancellationToken)
    {
        var existing = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var resolved = AppText.ResolveCulture(cultureCode).Name;
        await _settingsStore.SaveSettingsAsync(new UserFontSettings(existing?.ManagedFontDirectory ?? "", existing?.GoogleFontsApiKey ?? "", resolved), cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("settings", "ui-culture", AppText.Format("Log.UiCultureUpdatedFormat", resolved), resolved, true, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LocalFontImportService : ILocalFontImportService
{
    private static readonly string[] InstallableExtensions = [".ttf", ".otf", ".ttc", ".otc"];
    private static readonly string[] RecognizedButUnsupportedExtensions = [".woff", ".woff2"];

    private readonly IFontMetadataReader _metadataReader;
    private readonly IManagedFontFileStore _fileStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IFontMetadataStore _metadataStore;
    private readonly IFontLibraryMutationStore _mutationStore;
    private readonly ICollectionStore _collectionStore;
    private readonly IFontInstallService _installService;
    private readonly FontActivationCoordinator _activationCoordinator;
    private readonly IOperationLogger _operationLogger;

    public LocalFontImportService(
        IFontMetadataReader metadataReader,
        IManagedFontFileStore fileStore,
        IAppSettingsStore settingsStore,
        IFontMetadataStore metadataStore,
        IFontLibraryMutationStore mutationStore,
        ICollectionStore collectionStore,
        IFontInstallService installService,
        FontActivationCoordinator activationCoordinator,
        IOperationLogger operationLogger)
    {
        _metadataReader = metadataReader;
        _fileStore = fileStore;
        _settingsStore = settingsStore;
        _metadataStore = metadataStore;
        _mutationStore = mutationStore;
        _collectionStore = collectionStore;
        _installService = installService;
        _activationCoordinator = activationCoordinator;
        _operationLogger = operationLogger;
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
                items.Add(Failed(sourcePath, fileName, format, L("文件不存在")));
                continue;
            }

            if (RecognizedButUnsupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(Failed(sourcePath, fileName, format, L("M2 仅识别 WOFF/WOFF2；Windows 本地安装和临时启用暂不支持该格式。")));
                continue;
            }

            if (!InstallableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(Failed(sourcePath, fileName, format, L("不支持的字体格式")));
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
                    duplicate ? L("重复字体，可导入但会复用相同文件 hash") : L("可导入"),
                    Weight: metadata.Weight,
                    Width: metadata.Width,
                    Slant: metadata.Slant));
            }
            catch (Exception ex)
            {
                items.Add(Failed(sourcePath, fileName, format, ex.ToUserMessage()));
            }
        }

        return new FontImportPreview(items);
    }

    public async Task<FontImportResult> ImportAsync(FontImportPreview preview, FontImportOptions options, CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(L("需要先选择 GlyphStash 管理目录。"));

        var result = new List<FontImportResultItem>();
        foreach (var item in preview.Items.Where(item => item.CanImport))
        {
            try
            {
                var copy = await _fileStore.CopyToManagedDirectoryAsync(item.SourcePath, settings, cancellationToken).ConfigureAwait(false);
                var installResult = options.InstallForCurrentUser
                    ? await _installService.InstallForCurrentUserAsync(new FontFileRef(copy.ManagedPath, item.Format, copy.Sha256), cancellationToken).ConfigureAwait(false)
                    : new FontInstallResult(true, copy.ManagedPath, null, L("未选择用户级安装。"));
                var visiblePath = installResult is { Succeeded: true, InstalledPath: not null } ? installResult.InstalledPath : copy.ManagedPath;
                var visibleSource = installResult is { Succeeded: true, InstalledPath: not null } ? FontSourceKind.UserInstalled : FontSourceKind.GlyphStashManaged;
                var file = new FontFileRecord(visiblePath, item.Format, copy.Sha256, visibleSource, DateTimeOffset.UtcNow);
                var face = new FontFaceRecord(
                    item.FamilyName,
                    string.IsNullOrWhiteSpace(item.SubfamilyName) ? "Regular" : item.SubfamilyName,
                    string.IsNullOrWhiteSpace(item.FullName) ? item.FamilyName : item.FullName,
                    string.IsNullOrWhiteSpace(item.PostScriptName) ? item.FamilyName.Replace(' ', '-') : item.PostScriptName,
                    item.Weight,
                    item.Width,
                    item.Slant,
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
                    string.IsNullOrWhiteSpace(item.LicenseText) ? L("未知授权") : item.LicenseText,
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
                await _operationLogger.LogAsync("import", "import-font", F("已导入字体：{0}", "Imported font: {0}", item.FamilyName), copy.ManagedPath, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var message = ex.ToUserMessage();
                result.Add(new FontImportResultItem(item, false, null, message));
                await _operationLogger.LogAsync("import", "import-font", message, item.SourcePath, false, cancellationToken).ConfigureAwait(false);
            }
        }

        return new FontImportResult(result);
    }

    private static FontImportPreviewItem Failed(string sourcePath, string fileName, string format, string message) =>
        new(sourcePath, fileName, format, "", "", "", "", null, null, null, null, false, false, false, L("不可导入"), message);
}

public sealed class LocalFontOrganizationService : ILocalFontOrganizationService
{
    private readonly IFontLibraryMutationStore _mutationStore;
    private readonly ITagStore _tagStore;
    private readonly ICollectionStore _collectionStore;
    private readonly IOperationLogger _operationLogger;

    public LocalFontOrganizationService(
        IFontLibraryMutationStore mutationStore,
        ITagStore tagStore,
        ICollectionStore collectionStore,
        IOperationLogger operationLogger)
    {
        _mutationStore = mutationStore;
        _tagStore = tagStore;
        _collectionStore = collectionStore;
        _operationLogger = operationLogger;
    }

    public async Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken)
    {
        await _mutationStore.SetFavoriteAsync(familyName, isFavorite, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("library", "favorite", isFavorite ? F("已收藏：{0}", "Favorited: {0}", familyName) : F("已取消收藏：{0}", "Unfavorited: {0}", familyName), familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        await _mutationStore.SetTagsAsync(familyName, tags, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("tags", "set-tags", F("已更新标签：{0}", "Updated tags: {0}", familyName), familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
        _tagStore.GetTagsAsync(cancellationToken);

    public async Task CreateTagAsync(string name, CancellationToken cancellationToken)
    {
        await _tagStore.CreateTagAsync(name, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("tags", "create", F("已创建标签：{0}", "Created tag: {0}", name), name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await _tagStore.RenameTagAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("tags", "rename", F("已重命名标签：{0} -> {1}", "Renamed tag: {0} -> {1}", oldName, newName), oldName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTagAsync(string name, CancellationToken cancellationToken)
    {
        await _tagStore.DeleteTagAsync(name, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("tags", "delete", F("已删除标签：{0}", "Deleted tag: {0}", name), name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken)
    {
        await _mutationStore.SetCollectionsAsync(familyName, collections, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "set-collections", F("已更新集合关联：{0}", "Updated collection links: {0}", familyName), familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
        _collectionStore.GetCollectionsAsync(cancellationToken);

    public async Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await _collectionStore.CreateCollectionAsync(name, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "create", F("已创建集合：{0}", "Created collection: {0}", name), name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await _collectionStore.RenameCollectionAsync(oldName, newName, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "rename", F("已重命名集合：{0} -> {1}", "Renamed collection: {0} -> {1}", oldName, newName), oldName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await _collectionStore.DeleteCollectionAsync(name, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "delete", F("已删除集合：{0}", "Deleted collection: {0}", name), name, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await _collectionStore.AddFontToCollectionAsync(collectionName, familyName, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "add-font", F("已加入集合：{0} -> {1}", "Added to collection: {0} -> {1}", familyName, collectionName), familyName, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await _collectionStore.RemoveFontFromCollectionAsync(collectionName, familyName, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("collection", "remove-font", F("已从集合移除：{0} -> {1}", "Removed from collection: {0} -> {1}", familyName, collectionName), familyName, true, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LocalFontActivationService : ILocalFontActivationService
{
    private readonly IFontInstallService _installService;
    private readonly FontActivationCoordinator _activationCoordinator;
    private readonly IOperationLogger _operationLogger;

    public LocalFontActivationService(
        IFontInstallService installService,
        FontActivationCoordinator activationCoordinator,
        IOperationLogger operationLogger)
    {
        _installService = installService;
        _activationCoordinator = activationCoordinator;
        _operationLogger = operationLogger;
    }

    public async Task<ActivationResult> ActivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.ActivationState == FontActivationState.Installed)
        {
            var result = new ActivationResult(false, [], L("已安装，无需临时启用。"));
            await _operationLogger.LogAsync("activation", "activate-blocked", result.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return result;
        }

        return await _activationCoordinator.ActivateAsync(ownerKey, ToPhysicalFiles(font), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ActivationResult> DeactivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
        await _activationCoordinator.DeactivateAsync(ownerKey, ToPhysicalFiles(font), cancellationToken).ConfigureAwait(false);

    public Task RecordTemporaryActivationSkippedAsync(string ownerKey, FontFamilyRecord font, string reason, CancellationToken cancellationToken) =>
        _operationLogger.LogAsync("activation", "activate-skipped", $"{font.FamilyName}：{reason}", ownerKey, true, cancellationToken);

    public async Task<FontInstallResult> InstallFontAsync(FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.SourceKind == FontSourceKind.System)
        {
            var blocked = new FontInstallResult(false, font.PrimaryFilePath, null, L("系统字体已安装，无需执行用户级安装。"));
            await _operationLogger.LogAsync("install", "install", blocked.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return blocked;
        }

        var file = ToPhysicalFiles(font).First();
        var result = await _installService.InstallForCurrentUserAsync(file, cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("install", "install", result.Message, font.FamilyName, result.Succeeded, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<FontUninstallResult> UninstallManagedFontAsync(FontFamilyRecord font, CancellationToken cancellationToken)
    {
        if (font.SourceKind == FontSourceKind.System)
        {
            var blocked = new FontUninstallResult(false, font.PrimaryFilePath, L("系统字体在 v1 中不支持卸载。"));
            await _operationLogger.LogAsync("install", "uninstall", blocked.Message, font.FamilyName, false, cancellationToken).ConfigureAwait(false);
            return blocked;
        }

        var file = font.Faces.Select(face => face.File).FirstOrDefault(file => file.Path.Contains("Microsoft\\Windows\\Fonts", StringComparison.OrdinalIgnoreCase))
            ?? font.Faces.Select(face => face.File).FirstOrDefault();
        if (file is null)
        {
            return new FontUninstallResult(false, "", L("未找到可卸载字体文件。"));
        }

        var result = await _installService.UninstallManagedFontAsync(
            new ManagedFontRecord(font.FamilyName, file.Path, file.Format, file.Sha256, file.Path, FontActivationState.NotEnabled, file.LastSeenAt, null),
            cancellationToken).ConfigureAwait(false);
        await _operationLogger.LogAsync("install", "uninstall", result.Message, font.FamilyName, result.Succeeded, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static IReadOnlyList<FontFileRef> ToPhysicalFiles(FontFamilyRecord font)
    {
        var refs = font.Faces
            .Select(face => new FontFileRef(face.File.Path, face.File.Format, face.File.Sha256))
            .Where(fontFile => fontFile.HasPhysicalPath)
            .DistinctBy(fontFile => fontFile.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (refs.Count == 0)
        {
            throw new InvalidOperationException(L("该字体没有可用于临时启用的本地文件路径。"));
        }

        return refs;
    }
}

public sealed class LocalFontOperationLogService : ILocalFontOperationLogService
{
    private readonly IOperationLogStore _operationLogStore;

    public LocalFontOperationLogService(IOperationLogStore operationLogStore)
    {
        _operationLogStore = operationLogStore;
    }

    public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
        _operationLogStore.GetRecentOperationsAsync(limit, cancellationToken);
}

public sealed class OperationLogger : IOperationLogger
{
    private readonly IOperationLogStore _operationLogStore;

    public OperationLogger(IOperationLogStore operationLogStore)
    {
        _operationLogStore = operationLogStore;
    }

    public Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken) =>
        _operationLogStore.AppendOperationAsync(new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded), cancellationToken);
}
