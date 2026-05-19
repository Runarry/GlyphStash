using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed class FontLibraryService
{
    private readonly IFontInventoryService _inventoryService;
    private readonly IFontMetadataStore _metadataStore;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly IManagedFontFileStore? _managedFontFileStore;
    private readonly IFontMetadataReader? _metadataReader;
    private readonly IActivationStore? _activationStore;

    public FontLibraryService(IFontInventoryService inventoryService, IFontMetadataStore metadataStore)
        : this(inventoryService, metadataStore, null, null, null, null)
    {
    }

    public FontLibraryService(
        IFontInventoryService inventoryService,
        IFontMetadataStore metadataStore,
        IAppSettingsStore? settingsStore,
        IManagedFontFileStore? managedFontFileStore,
        IFontMetadataReader? metadataReader,
        IActivationStore? activationStore)
    {
        _inventoryService = inventoryService;
        _metadataStore = metadataStore;
        _settingsStore = settingsStore;
        _managedFontFileStore = managedFontFileStore;
        _metadataReader = metadataReader;
        _activationStore = activationStore;
    }

    public async Task<IReadOnlyList<FontFamilyRecord>> LoadCachedFontsAsync(FontSearchQuery query, CancellationToken cancellationToken)
    {
        await _metadataStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await _metadataStore.SearchAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FontFamilyRecord>> RescanAsync(CancellationToken cancellationToken)
    {
        await _metadataStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var active = await GetActiveTemporaryActivationsAsync(cancellationToken).ConfigureAwait(false);
        var installedFonts = await _inventoryService.ScanInstalledFontsAsync(cancellationToken).ConfigureAwait(false);
        var managedFonts = await ScanManagedFontsAsync(cancellationToken).ConfigureAwait(false);
        installedFonts = FilterTemporaryEnumerationInstalledFonts(installedFonts, managedFonts, active);
        var fonts = MergeFonts(installedFonts, managedFonts);
        fonts = ApplyCurrentTemporaryActivations(fonts, active);
        fonts = ApplyAutomaticTags(fonts);
        await _metadataStore.SaveFontIndexAsync(fonts, cancellationToken).ConfigureAwait(false);
        return await _metadataStore.SearchAsync(new FontSearchQuery(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<FontFamilyRecord>> ScanManagedFontsAsync(CancellationToken cancellationToken)
    {
        if (_settingsStore is null || _managedFontFileStore is null || _metadataReader is null)
        {
            return [];
        }

        var settings = await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || string.IsNullOrWhiteSpace(settings.ManagedFontDirectory))
        {
            return [];
        }

        var paths = await _managedFontFileStore.EnumerateManagedFontFilesAsync(settings, cancellationToken).ConfigureAwait(false);
        var faces = new List<FontFaceRecord>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = await _metadataReader.ReadMetadataAsync(path, cancellationToken).ConfigureAwait(false);
                var file = new FontFileRecord(
                    path,
                    metadata.Format,
                    metadata.Sha256,
                    FontSourceKind.GlyphStashManaged,
                    DateTimeOffset.UtcNow);

                faces.Add(new FontFaceRecord(
                    metadata.FamilyName,
                    string.IsNullOrWhiteSpace(metadata.SubfamilyName) ? "Regular" : metadata.SubfamilyName,
                    string.IsNullOrWhiteSpace(metadata.FullName) ? metadata.FamilyName : metadata.FullName,
                    string.IsNullOrWhiteSpace(metadata.PostScriptName) ? metadata.FamilyName.Replace(' ', '-') : metadata.PostScriptName,
                    metadata.Weight,
                    metadata.Width,
                    metadata.Slant,
                    file));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A damaged or unsupported managed font should not block refreshing the rest of the index.
            }
        }

        return faces
            .GroupBy(face => face.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                var groupFaces = group
                    .OrderBy(face => face.Weight)
                    .ThenBy(face => face.SubfamilyName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                return new FontFamilyRecord(
                    group.Key,
                    groupFaces,
                    FontSourceKind.GlyphStashManaged,
                    FontActivationState.NotEnabled,
                    LicenseStatus.Unknown,
                    "未知授权",
                    [],
                    [],
                    false);
            })
            .OrderBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<ActivationRecord>> GetActiveTemporaryActivationsAsync(CancellationToken cancellationToken)
    {
        if (_activationStore is null)
        {
            return [];
        }

        return await _activationStore.GetOwnedActivationsAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<FontFamilyRecord> FilterTemporaryEnumerationInstalledFonts(
        IReadOnlyList<FontFamilyRecord> installedFonts,
        IReadOnlyList<FontFamilyRecord> managedFonts,
        IReadOnlyList<ActivationRecord> active)
    {
        if (installedFonts.Count == 0 || managedFonts.Count == 0 || active.Count == 0)
        {
            return installedFonts;
        }

        var activePaths = BuildActivePathSet(active);
        var activeHashes = BuildActiveHashSet(active);
        if (activePaths.Count == 0 && activeHashes.Count == 0)
        {
            return installedFonts;
        }

        var activeManagedFamilyNames = managedFonts
            .Where(font => FontMatchesActiveIdentity(font, activePaths, activeHashes))
            .Select(font => font.FamilyName)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (activeManagedFamilyNames.Count == 0)
        {
            return installedFonts;
        }

        return installedFonts
            .Where(font => !activeManagedFamilyNames.Contains(font.FamilyName) || !IsEnumerationOnlyInstalledFont(font))
            .ToList();
    }

    private static IReadOnlyList<FontFamilyRecord> MergeFonts(
        IReadOnlyList<FontFamilyRecord> installedFonts,
        IReadOnlyList<FontFamilyRecord> managedFonts)
    {
        var merged = installedFonts
            .GroupBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First() with
                {
                    Faces = group.SelectMany(font => font.Faces)
                        .DistinctBy(face => face.File.Path, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                },
                StringComparer.CurrentCultureIgnoreCase);

        foreach (var managed in managedFonts)
        {
            if (!merged.TryGetValue(managed.FamilyName, out var existing))
            {
                merged[managed.FamilyName] = managed;
                continue;
            }

            var faces = existing.Faces
                .Concat(managed.Faces)
                .DistinctBy(face => face.File.Path, StringComparer.OrdinalIgnoreCase)
                .OrderBy(face => face.Weight)
                .ThenBy(face => face.SubfamilyName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            merged[managed.FamilyName] = existing with { Faces = faces };
        }

        return merged.Values
            .OrderBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<FontFamilyRecord> ApplyCurrentTemporaryActivations(
        IReadOnlyList<FontFamilyRecord> fonts,
        IReadOnlyList<ActivationRecord> active)
    {
        if (active.Count == 0)
        {
            return fonts;
        }

        var activePaths = BuildActivePathSet(active);
        var activeHashes = BuildActiveHashSet(active);

        return fonts.Select(font =>
        {
            if (font.ActivationState == FontActivationState.Installed)
            {
                return font;
            }

            var isTemporarilyEnabled = font.Faces.Any(face =>
                activePaths.Contains(face.File.Path)
                || (!string.IsNullOrWhiteSpace(face.File.Sha256) && activeHashes.Contains(face.File.Sha256)));

            return isTemporarilyEnabled
                ? font with { ActivationState = FontActivationState.TemporarilyEnabled }
                : font;
        }).ToList();
    }

    private static IReadOnlyList<FontFamilyRecord> ApplyAutomaticTags(IReadOnlyList<FontFamilyRecord> fonts) =>
        fonts.Select(font =>
        {
            if (!LooksLikeCjkFont(font))
            {
                return font;
            }

            var tags = font.Tags
                .Concat(["中文"])
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return font with { Tags = tags };
        }).ToList();

    private static bool LooksLikeCjkFont(FontFamilyRecord font)
    {
        var searchableText = string.Join(
            ' ',
            font.FamilyName,
            string.Join(' ', font.Faces.Select(face => $"{face.SubfamilyName} {face.FullName} {face.PostScriptName}")));

        return searchableText.Contains("CJK", StringComparison.OrdinalIgnoreCase)
            || ContainsSeparatedToken(searchableText, "SC")
            || ContainsSeparatedToken(searchableText, "TC")
            || ContainsSeparatedToken(searchableText, "CN")
            || ContainsSeparatedToken(searchableText, "GB")
            || ContainsSeparatedToken(searchableText, "GBK")
            || searchableText.Contains("Hans", StringComparison.OrdinalIgnoreCase)
            || searchableText.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || searchableText.Contains("Chinese", StringComparison.OrdinalIgnoreCase)
            || searchableText.Contains("中文", StringComparison.Ordinal)
            || searchableText.Contains("宋体", StringComparison.Ordinal)
            || searchableText.Contains("黑体", StringComparison.Ordinal)
            || searchableText.Contains("楷体", StringComparison.Ordinal)
            || searchableText.Contains("仿宋", StringComparison.Ordinal)
            || searchableText.Contains("雅黑", StringComparison.Ordinal)
            || searchableText.Contains("思源", StringComparison.Ordinal)
            || searchableText.Contains("方正", StringComparison.Ordinal)
            || searchableText.Contains("华文", StringComparison.Ordinal);
    }

    private static bool ContainsSeparatedToken(string value, string token) =>
        value.Split([' ', '-', '_', '.', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, token, StringComparison.OrdinalIgnoreCase));

    private static bool FontMatchesActiveIdentity(
        FontFamilyRecord font,
        IReadOnlySet<string> activePaths,
        IReadOnlySet<string> activeHashes)
    {
        return font.Faces.Any(face =>
            activePaths.Contains(face.File.Path)
            || (!string.IsNullOrWhiteSpace(face.File.Sha256) && activeHashes.Contains(face.File.Sha256)));
    }

    private static bool IsEnumerationOnlyInstalledFont(FontFamilyRecord font)
    {
        return font.ActivationState == FontActivationState.Installed
            && font.Faces.Count > 0
            && font.Faces.All(face => IsEnumerationOnlyInstalledPath(face.File.Path));
    }

    private static bool IsEnumerationOnlyInstalledPath(string path) =>
        path.StartsWith("installed://", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> BuildActivePathSet(IReadOnlyList<ActivationRecord> active) =>
        active
            .Select(record => record.FontPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> BuildActiveHashSet(IReadOnlyList<ActivationRecord> active) =>
        active
            .Select(record => record.Sha256)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Select(hash => hash!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
