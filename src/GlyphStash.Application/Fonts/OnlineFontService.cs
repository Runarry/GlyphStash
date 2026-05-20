using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Application.Fonts;

public sealed class OnlineFontService
{
    private readonly IFontSourceProvider _provider;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IManagedFontFileStore _managedFontFileStore;
    private readonly IFontLibraryMutationStore _mutationStore;
    private readonly IFontInstallService _installService;
    private readonly FontActivationCoordinator _activationCoordinator;
    private readonly IOperationLogStore _operationLogStore;
    private readonly IDownloadRecordStore _downloadRecordStore;

    public OnlineFontService(
        IFontSourceProvider provider,
        IAppSettingsStore settingsStore,
        IManagedFontFileStore managedFontFileStore,
        IFontLibraryMutationStore mutationStore,
        IFontInstallService installService,
        FontActivationCoordinator activationCoordinator,
        IOperationLogStore operationLogStore,
        IDownloadRecordStore downloadRecordStore)
    {
        _provider = provider;
        _settingsStore = settingsStore;
        _managedFontFileStore = managedFontFileStore;
        _mutationStore = mutationStore;
        _installService = installService;
        _activationCoordinator = activationCoordinator;
        _operationLogStore = operationLogStore;
        _downloadRecordStore = downloadRecordStore;
    }

    public async Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(
        string searchText,
        string subset,
        string category,
        IReadOnlyList<string> capabilities,
        string sort,
        CancellationToken cancellationToken)
    {
        var settings = await RequireSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(settings.GoogleFontsApiKey))
        {
            throw new InvalidOperationException(L("需要先在设置页配置 Google Fonts API key。"));
        }

        return await _provider.SearchAsync(new RemoteFontSearchQuery(searchText, settings.GoogleFontsApiKey, subset, category, capabilities, sort), cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(string searchText, CancellationToken cancellationToken) =>
        SearchAsync(searchText, "", "", [], "alpha", cancellationToken);

    public async Task<RemoteFontDownloadResult> DownloadAsync(
        RemoteFontFamily family,
        IReadOnlyList<RemoteFontStyle> styles,
        OnlineFontImportOptions options,
        CancellationToken cancellationToken)
    {
        var settings = await RequireSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(settings.GoogleFontsApiKey))
        {
            throw new InvalidOperationException(L("需要先在设置页配置 Google Fonts API key。"));
        }

        if (string.IsNullOrWhiteSpace(settings.ManagedFontDirectory))
        {
            throw new InvalidOperationException(L("需要先选择 GlyphStash 管理目录。"));
        }

        var selectedStyles = styles.Where(style => !string.IsNullOrWhiteSpace(style.DownloadUrl)).ToList();
        if (selectedStyles.Count == 0)
        {
            throw new InvalidOperationException(L("请选择至少一个可下载样式。"));
        }

        var result = await _provider.DownloadAsync(
            new RemoteFontDownloadRequest(family, selectedStyles, settings.ManagedFontDirectory, settings.GoogleFontsApiKey),
            cancellationToken).ConfigureAwait(false);

        var finalFiles = new List<RemoteFontDownloadedFile>();
        foreach (var file in result.Files)
        {
            var copy = await _managedFontFileStore.CopyToManagedDirectoryAsync(file.LocalPath, settings, cancellationToken).ConfigureAwait(false);
            var finalFile = file with { LocalPath = copy.ManagedPath, Sha256 = copy.Sha256 };
            finalFiles.Add(finalFile);

            var fontFile = new FontFileRecord(finalFile.LocalPath, finalFile.Format, finalFile.Sha256, FontSourceKind.GlyphStashManaged, DateTimeOffset.UtcNow);
            var styleLabel = FontStyleVariantFormatter.FormatGoogleFontsVariant(file.Style.Variant);
            var weight = FontStyleVariantFormatter.WeightFromGoogleFontsVariant(file.Style.Variant);
            var slant = FontStyleVariantFormatter.SlantFromGoogleFontsVariant(file.Style.Variant);
            var face = new FontFaceRecord(
                family.FamilyName,
                styleLabel,
                $"{family.FamilyName} {styleLabel}".Trim(),
                $"{family.FamilyName.Replace(' ', '-')}-{file.Style.Variant}",
                weight,
                "Normal",
                slant,
                fontFile);
            var installResult = options.InstallForCurrentUser
                ? await _installService.InstallForCurrentUserAsync(new FontFileRef(finalFile.LocalPath, finalFile.Format, finalFile.Sha256), cancellationToken).ConfigureAwait(false)
                : new FontInstallResult(true, finalFile.LocalPath, null, L("未选择用户级安装。"));
            var shouldActivate = !options.InstallForCurrentUser && options.TemporarilyActivate;
            if (shouldActivate)
            {
                await _activationCoordinator.ActivateAsync($"font:{family.FamilyName}", [new FontFileRef(finalFile.LocalPath, finalFile.Format, finalFile.Sha256)], cancellationToken)
                    .ConfigureAwait(false);
            }

            var visiblePath = installResult.InstalledPath ?? finalFile.LocalPath;
            var visibleFile = fontFile with
            {
                Path = visiblePath,
                SourceKind = installResult.InstalledPath is null ? FontSourceKind.GlyphStashManaged : FontSourceKind.UserInstalled
            };
            var visibleFace = face with { File = visibleFile };
            var state = shouldActivate
                ? FontActivationState.TemporarilyEnabled
                : installResult.InstalledPath is null ? FontActivationState.NotEnabled : FontActivationState.Installed;
            var record = new FontFamilyRecord(
                family.FamilyName,
                [visibleFace],
                visibleFile.SourceKind,
                state,
                LicenseStatus.ExternalLink,
                family.LicenseText,
                options.Tags,
                options.Collections,
                options.Favorite);

            await _mutationStore.UpsertManagedFontAsync(
                new ManagedFontRecord(
                    family.FamilyName,
                    finalFile.LocalPath,
                    finalFile.Format,
                    finalFile.Sha256,
                    installResult.InstalledPath,
                    state,
                    DateTimeOffset.UtcNow,
                    installResult.InstalledPath is null ? null : DateTimeOffset.UtcNow),
                record,
                cancellationToken).ConfigureAwait(false);

            await _downloadRecordStore.AddDownloadRecordAsync(
                new DownloadRecord(
                    family.ProviderId,
                    family.FamilyName,
                    family.FamilyName,
                    file.Style.Variant,
                    file.Style.DownloadUrl,
                    family.SourceUrl,
                    family.LicenseText,
                    finalFile.LocalPath,
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }

        await LogAsync("online-fonts", "download", AppText.FormatLiteral("已下载在线字体：{0} ({1} 个样式)", "Downloaded online font: {0} ({1} styles)", family.FamilyName, finalFiles.Count), family.FamilyName, true, cancellationToken)
            .ConfigureAwait(false);
        return result with { Files = finalFiles, Message = AppText.FormatLiteral("已下载 {0} 个样式。", "Downloaded {0} styles.", finalFiles.Count) };
    }

    private async Task<UserFontSettings> RequireSettingsAsync(CancellationToken cancellationToken) =>
        await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false) ?? new UserFontSettings("");

    private Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken) =>
        _operationLogStore.AppendOperationAsync(new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded), cancellationToken);

    private static string L(string text) => AppText.TranslateLiteral(text);
}

public sealed record OnlineFontImportOptions(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Collections,
    bool Favorite,
    bool InstallForCurrentUser,
    bool TemporarilyActivate);
