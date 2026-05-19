using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Fonts;

public interface IFontInventoryService
{
    Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken);
}
