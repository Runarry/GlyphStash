using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Storage;

public interface IFontMetadataStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken);

    Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken);
}
