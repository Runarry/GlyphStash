using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed class FontLibraryService
{
    private readonly IFontInventoryService _inventoryService;
    private readonly IFontMetadataStore _metadataStore;

    public FontLibraryService(IFontInventoryService inventoryService, IFontMetadataStore metadataStore)
    {
        _inventoryService = inventoryService;
        _metadataStore = metadataStore;
    }

    public async Task<IReadOnlyList<FontFamilyRecord>> LoadCachedFontsAsync(FontSearchQuery query, CancellationToken cancellationToken)
    {
        await _metadataStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await _metadataStore.SearchAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FontFamilyRecord>> RescanAsync(CancellationToken cancellationToken)
    {
        await _metadataStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var fonts = await _inventoryService.ScanInstalledFontsAsync(cancellationToken).ConfigureAwait(false);
        await _metadataStore.SaveFontIndexAsync(fonts, cancellationToken).ConfigureAwait(false);
        return fonts;
    }
}
