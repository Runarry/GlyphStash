using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed class FontActivationCoordinator
{
    private const string ActiveState = "Active";
    private const string ReleasedState = "Released";
    private const string StaleState = "StaleAfterRestart";

    private readonly ITemporaryFontActivationService _platformActivationService;
    private readonly IActivationStore _activationStore;
    private readonly IOperationLogStore _operationLogStore;

    public FontActivationCoordinator(
        ITemporaryFontActivationService platformActivationService,
        IActivationStore activationStore,
        IOperationLogStore operationLogStore)
    {
        _platformActivationService = platformActivationService;
        _activationStore = activationStore;
        _operationLogStore = operationLogStore;
    }

    public async Task<ActivationResult> ActivateAsync(string ownerKey, IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
    {
        var existing = await _activationStore.GetOwnedActivationsAsync(cancellationToken).ConfigureAwait(false);
        var newPlatformLoads = fonts
            .Where(font => existing.All(record => !SamePath(record.FontPath, font.Path)))
            .ToList();

        var platformResult = newPlatformLoads.Count == 0
            ? new ActivationResult(true, [], "字体已由当前 GlyphStash 会话持有，已增加引用。")
            : await _platformActivationService.ActivateForCurrentUserSessionAsync(newPlatformLoads, cancellationToken).ConfigureAwait(false);

        var succeededPaths = platformResult.Files
            .Where(result => result.Succeeded)
            .Select(result => result.FontFile.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var font in fonts)
        {
            var current = existing.FirstOrDefault(record => SamePath(record.FontPath, font.Path) && record.OwnerKey == ownerKey);
            var platformSucceeded = newPlatformLoads.All(loaded => !SamePath(loaded.Path, font.Path)) || succeededPaths.Contains(font.Path);
            if (!platformSucceeded)
            {
                continue;
            }

            await _activationStore.UpsertActivationAsync(
                new ActivationRecord(
                    font.Path,
                    ownerKey,
                    (current?.ReferenceCount ?? 0) + 1,
                    platformResult.Files.FirstOrDefault(file => SamePath(file.FontFile.Path, font.Path))?.PlatformFlags ?? current?.PlatformFlags ?? 0,
                    current?.ActivatedAt ?? DateTimeOffset.UtcNow,
                    ActiveState,
                    "",
                    font.Sha256),
                cancellationToken).ConfigureAwait(false);
        }

        await LogAsync("activation", "activate", platformResult.Message, string.Join(", ", fonts.Select(font => font.Path)), platformResult.Succeeded, cancellationToken)
            .ConfigureAwait(false);

        return platformResult;
    }

    public async Task<ActivationResult> DeactivateAsync(string ownerKey, IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
    {
        var existing = await _activationStore.GetOwnedActivationsAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<FontActivationFileResult>();
        var platformFiles = new List<FontFileRef>();

        foreach (var font in fonts)
        {
            var current = existing.FirstOrDefault(record => SamePath(record.FontPath, font.Path) && record.OwnerKey == ownerKey);
            if (current is null)
            {
                results.Add(new FontActivationFileResult(font, true, 0, 0, "当前来源没有持有该字体的临时启用引用。"));
                continue;
            }

            if (current.ReferenceCount > 1)
            {
                await _activationStore.UpsertActivationAsync(current with
                {
                    ReferenceCount = current.ReferenceCount - 1,
                    LastKnownState = ActiveState
                }, cancellationToken).ConfigureAwait(false);
                results.Add(new FontActivationFileResult(font, true, 0, current.PlatformFlags));
                continue;
            }

            await _activationStore.RemoveActivationAsync(font.Path, ownerKey, cancellationToken).ConfigureAwait(false);
            var remaining = (await _activationStore.GetOwnedActivationsAsync(cancellationToken).ConfigureAwait(false))
                .Any(record => SamePath(record.FontPath, font.Path));
            if (!remaining)
            {
                platformFiles.Add(font);
            }

            results.Add(new FontActivationFileResult(font, true, 0, current.PlatformFlags));
        }

        var platformResult = platformFiles.Count == 0
            ? new ActivationResult(true, [], "已释放 GlyphStash 持有的临时启用引用。")
            : await _platformActivationService.DeactivateForCurrentUserSessionAsync(platformFiles, cancellationToken).ConfigureAwait(false);

        await LogAsync("activation", "deactivate", platformResult.Message, string.Join(", ", fonts.Select(font => font.Path)), platformResult.Succeeded, cancellationToken)
            .ConfigureAwait(false);

        return new ActivationResult(platformResult.Succeeded, results.Concat(platformResult.Files).ToList(), platformResult.Message);
    }

    public async Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken)
    {
        var existing = await _activationStore.GetOwnedActivationsAsync(cancellationToken).ConfigureAwait(false);
        var fonts = existing
            .GroupBy(record => record.FontPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FontFileRef(group.Key, Sha256: group.FirstOrDefault(record => !string.IsNullOrWhiteSpace(record.Sha256))?.Sha256))
            .ToList();

        if (fonts.Count > 0)
        {
            await _platformActivationService.DeactivateForCurrentUserSessionAsync(fonts, cancellationToken).ConfigureAwait(false);
        }

        await _activationStore.MarkAllOwnedStaleAsync(cancellationToken).ConfigureAwait(false);
        await LogAsync("activation", "cleanup", $"退出清理临时启用字体：{fonts.Count} 个文件", null, true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkRestartedActivationsStaleAsync(CancellationToken cancellationToken)
    {
        await _activationStore.MarkAllOwnedStaleAsync(cancellationToken).ConfigureAwait(false);
        await LogAsync("activation", "recover", "应用启动时将上次会话的临时启用记录标记为已过期。", null, true, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken) =>
        _operationLogStore.AppendOperationAsync(new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded), cancellationToken);

    private static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
