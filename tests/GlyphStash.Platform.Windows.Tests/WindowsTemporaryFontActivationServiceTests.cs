using GlyphStash.Domain.Fonts;
using GlyphStash.Platform.Windows.Fonts;
using System.Runtime.Versioning;

namespace GlyphStash.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsTemporaryFontActivationServiceTests
{
    [Fact]
    public async Task ActivateAndDeactivate_UsePublicSessionFlagsAndRemoveLoadedCount()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.otf");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
        await File.WriteAllTextAsync(tempFile, "fixture");

        var api = new FakeWindowsFontApi();
        var service = new WindowsTemporaryFontActivationService(api);
        var font = new FontFileRef(tempFile, "OTF");

        var activate = await service.ActivateForCurrentUserSessionAsync([font], CancellationToken.None);
        var deactivate = await service.DeactivateForCurrentUserSessionAsync([font], CancellationToken.None);

        Assert.True(activate.Succeeded);
        Assert.True(deactivate.Succeeded);
        Assert.Equal([0], api.AddFlags);
        Assert.Equal([0, 0], api.RemoveFlags);
        Assert.Equal(2, api.BroadcastCount);
    }

    private sealed class FakeWindowsFontApi : IWindowsFontApi
    {
        public List<int> AddFlags { get; } = [];

        public List<int> RemoveFlags { get; } = [];

        public int BroadcastCount { get; private set; }

        public int AddFontResourceEx(string fileName, int flags)
        {
            AddFlags.Add(flags);
            return 2;
        }

        public bool RemoveFontResourceEx(string fileName, int flags)
        {
            RemoveFlags.Add(flags);
            return true;
        }

        public void BroadcastFontChange() => BroadcastCount++;
    }
}
