using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class FontActivationCoordinatorTests
{
    [Fact]
    public async Task Deactivate_ReleasesPlatformOnlyAfterAllOwnersRelease()
    {
        var platform = new FakePlatformActivation();
        var store = new FakeActivationStore();
        var coordinator = new FontActivationCoordinator(platform, store, new FakeOperationLogStore());
        var font = new FontFileRef("C:/Project/BrandSans.otf", "OTF", "hash");

        await coordinator.ActivateAsync("font:BrandSans", [font], CancellationToken.None);
        await coordinator.ActivateAsync("collection:Website", [font], CancellationToken.None);
        await coordinator.DeactivateAsync("collection:Website", [font], CancellationToken.None);

        Assert.Equal(1, platform.ActivateCalls);
        Assert.Equal(0, platform.DeactivateCalls);

        await coordinator.DeactivateAsync("font:BrandSans", [font], CancellationToken.None);

        Assert.Equal(1, platform.DeactivateCalls);
    }

    [Fact]
    public async Task Activate_DoesNotReleasePlatformUntilExplicitDeactivateOrCleanup()
    {
        var platform = new FakePlatformActivation();
        var store = new FakeActivationStore();
        var coordinator = new FontActivationCoordinator(platform, store, new FakeOperationLogStore());
        var font = new FontFileRef("C:/Project/BrandSans.otf", "OTF", "hash");

        await coordinator.ActivateAsync("font:BrandSans", [font], CancellationToken.None);

        Assert.Equal(1, platform.ActivateCalls);
        Assert.Equal(0, platform.DeactivateCalls);
    }

    private sealed class FakePlatformActivation : ITemporaryFontActivationService
    {
        public int ActivateCalls { get; private set; }

        public int DeactivateCalls { get; private set; }

        public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
        {
            ActivateCalls += fonts.Count;
            return Task.FromResult(new ActivationResult(true, fonts.Select(font => new FontActivationFileResult(font, true, 1, 0)).ToList(), "activated"));
        }

        public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
        {
            DeactivateCalls += fonts.Count;
            return Task.FromResult(new ActivationResult(true, fonts.Select(font => new FontActivationFileResult(font, true, 1, 0)).ToList(), "deactivated"));
        }

        public Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        private readonly Dictionary<(string Path, string Owner), ActivationRecord> _records = new();

        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>(_records.Values.Where(record => record.LastKnownState == "Active").ToList());

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken)
        {
            _records[(record.FontPath, record.OwnerKey)] = record;
            return Task.CompletedTask;
        }

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken)
        {
            _records.Remove((fontPath, ownerKey));
            return Task.CompletedTask;
        }

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken)
        {
            foreach (var key in _records.Keys.ToList())
            {
                _records[key] = _records[key] with { LastKnownState = "StaleAfterRestart" };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeOperationLogStore : IOperationLogStore
    {
        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }
}
