using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Fonts;

public interface IFontMergeWorker
{
    Task<FontMergeWorkerPreviewResult> PreviewAsync(
        FontMergeWorkerRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken);

    Task<FontMergeWorkerMergeResult> MergeAsync(
        FontMergeWorkerRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken);
}
