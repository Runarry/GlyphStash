using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed class OnlineFontService
{
    private readonly IFontSourceProvider _provider;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IFontLibraryMutationStore _mutationStore;
    private readonly IFontInstallService _installService;
    private readonly FontActivationCoordinator _activationCoordinator;
    private readonly IOperationLogStore _operationLogStore;
    private readonly IDownloadRecordStore _downloadRecordStore;

    public OnlineFontService(
        IFontSourceProvider provider,
        IAppSettingsStore settingsStore,
        IFontLibraryMutationStore mutationStore,
        IFontInstallService installService,
        FontActivationCoordinator activationCoordinator,
        IOperationLogStore operationLogStore,
        IDownloadRecordStore downloadRecordStore)
    {
        _provider = provider;
        _settingsStore = settingsStore;
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
            throw new InvalidOperationException("需要先在设置页配置 Google Fonts API key。");
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
            throw new InvalidOperationException("需要先在设置页配置 Google Fonts API key。");
        }

        if (string.IsNullOrWhiteSpace(settings.ManagedFontDirectory))
        {
            throw new InvalidOperationException("需要先选择 GlyphStash 管理目录。");
        }

        var selectedStyles = styles.Where(style => !string.IsNullOrWhiteSpace(style.DownloadUrl)).ToList();
        if (selectedStyles.Count == 0)
        {
            throw new InvalidOperationException("请选择至少一个可下载样式。");
        }

        var result = await _provider.DownloadAsync(
            new RemoteFontDownloadRequest(family, selectedStyles, settings.ManagedFontDirectory, settings.GoogleFontsApiKey),
            cancellationToken).ConfigureAwait(false);

        foreach (var file in result.Files)
        {
            var fontFile = new FontFileRecord(file.LocalPath, file.Format, file.Sha256, FontSourceKind.GlyphStashManaged, DateTimeOffset.UtcNow);
            var face = new FontFaceRecord(
                family.FamilyName,
                NormalizeSubfamily(file.Style.Variant),
                $"{family.FamilyName} {NormalizeSubfamily(file.Style.Variant)}".Trim(),
                $"{family.FamilyName.Replace(' ', '-')}-{file.Style.Variant}",
                WeightFromVariant(file.Style.Variant),
                "Normal",
                file.Style.Variant.Contains("italic", StringComparison.OrdinalIgnoreCase) ? "Italic" : "Normal",
                fontFile);
            var installResult = options.InstallForCurrentUser
                ? await _installService.InstallForCurrentUserAsync(new FontFileRef(file.LocalPath, file.Format, file.Sha256), cancellationToken).ConfigureAwait(false)
                : new FontInstallResult(true, file.LocalPath, null, "未选择用户级安装。");
            var shouldActivate = !options.InstallForCurrentUser && options.TemporarilyActivate;
            if (shouldActivate)
            {
                await _activationCoordinator.ActivateAsync($"font:{family.FamilyName}", [new FontFileRef(file.LocalPath, file.Format, file.Sha256)], cancellationToken)
                    .ConfigureAwait(false);
            }

            var visiblePath = installResult.InstalledPath ?? file.LocalPath;
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
                    file.LocalPath,
                    file.Format,
                    file.Sha256,
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
                    file.LocalPath,
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }

        await LogAsync("online-fonts", "download", $"已下载在线字体：{family.FamilyName} ({result.Files.Count} 个样式)", family.FamilyName, true, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private async Task<UserFontSettings> RequireSettingsAsync(CancellationToken cancellationToken) =>
        await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false) ?? new UserFontSettings("");

    private Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken) =>
        _operationLogStore.AppendOperationAsync(new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded), cancellationToken);

    private static string NormalizeSubfamily(string variant)
    {
        if (string.Equals(variant, "regular", StringComparison.OrdinalIgnoreCase))
        {
            return "Regular";
        }

        if (string.Equals(variant, "italic", StringComparison.OrdinalIgnoreCase))
        {
            return "Italic";
        }

        return variant;
    }

    private static int WeightFromVariant(string variant)
    {
        var digits = new string(variant.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var weight) ? weight : 400;
    }
}

public sealed record OnlineFontImportOptions(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Collections,
    bool Favorite,
    bool InstallForCurrentUser,
    bool TemporarilyActivate);
