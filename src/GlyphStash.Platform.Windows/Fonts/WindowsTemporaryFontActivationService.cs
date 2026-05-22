using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Platform.Windows.Fonts;

[SupportedOSPlatform("windows")]
public sealed class WindowsTemporaryFontActivationService : ITemporaryFontActivationService
{
    public const int PublicSessionFlags = 0;

    private readonly IWindowsFontApi _fontApi;
    private readonly Dictionary<string, int> _loadedCounts = new(StringComparer.OrdinalIgnoreCase);

    public WindowsTemporaryFontActivationService(IWindowsFontApi fontApi)
    {
        _fontApi = fontApi;
    }

    public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
    {
        var results = new List<FontActivationFileResult>();
        foreach (var font in fonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!font.HasPhysicalPath || !File.Exists(font.Path))
            {
                results.Add(new FontActivationFileResult(font, false, 0, PublicSessionFlags, L("字体文件不存在或不是本地物理路径。")));
                continue;
            }

            var count = _fontApi.AddFontResourceEx(font.Path, PublicSessionFlags);
            if (count <= 0)
            {
                results.Add(new FontActivationFileResult(font, false, 0, PublicSessionFlags, L("Windows 未能加载该字体资源。")));
                continue;
            }

            _loadedCounts[font.Path] = _loadedCounts.GetValueOrDefault(font.Path) + count;
            results.Add(new FontActivationFileResult(font, true, count, PublicSessionFlags));
        }

        if (results.Any(result => result.Succeeded))
        {
            _fontApi.BroadcastFontChange();
        }

        var failed = results.Count(result => !result.Succeeded);
        return Task.FromResult(new ActivationResult(
            failed == 0,
            results,
            failed == 0
                ? L("字体已临时启用，并已广播 WM_FONTCHANGE。")
                : AppText.FormatLiteral("部分字体临时启用失败：{0} 个。", "Some fonts failed temporary activation: {0}.", failed)));
    }

    public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
    {
        var results = new List<FontActivationFileResult>();
        foreach (var font in fonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Max(1, _loadedCounts.GetValueOrDefault(font.Path));
            var removed = 0;
            for (var i = 0; i < count; i++)
            {
                if (_fontApi.RemoveFontResourceEx(font.Path, PublicSessionFlags))
                {
                    removed++;
                }
            }

            _loadedCounts.Remove(font.Path);
            results.Add(new FontActivationFileResult(
                font,
                removed > 0,
                removed,
                PublicSessionFlags,
                removed > 0 ? null : L("Windows 未确认释放该字体资源。")));
        }

        if (results.Any(result => result.Succeeded))
        {
            _fontApi.BroadcastFontChange();
        }

        var failed = results.Count(result => !result.Succeeded);
        return Task.FromResult(new ActivationResult(
            failed == 0,
            results,
            failed == 0
                ? L("字体临时启用已关闭，并已广播 WM_FONTCHANGE。")
                : AppText.FormatLiteral("部分字体关闭失败：{0} 个。", "Some fonts failed to deactivate: {0}.", failed)));
    }

    public async Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken)
    {
        var fonts = _loadedCounts.Keys.Select(path => new FontFileRef(path)).ToList();
        if (fonts.Count > 0)
        {
            await DeactivateForCurrentUserSessionAsync(fonts, cancellationToken).ConfigureAwait(false);
        }
    }

}
