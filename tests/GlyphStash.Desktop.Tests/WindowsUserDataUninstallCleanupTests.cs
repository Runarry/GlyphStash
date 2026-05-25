using System.Runtime.Versioning;

namespace GlyphStash.Desktop.Tests;

public sealed class WindowsUserDataUninstallCleanupTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void CleanupUserDataIfRequested_DefaultsToPreserveDataWithoutMarker()
    {
        var root = CreateTempDirectory();
        var marker = Path.Combine(root, "uninstall-clear-user-data.marker");
        File.WriteAllText(Path.Combine(root, "glyphstash.db"), "data");

        var result = WindowsUserDataUninstallCleanup.CleanupUserDataIfRequested(root, marker);

        Assert.False(result.Requested);
        Assert.True(result.Succeeded);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void CleanupUserDataIfRequested_DeletesDataWhenMarkerExists()
    {
        var root = CreateTempDirectory();
        var marker = Path.Combine(root, "uninstall-clear-user-data.marker");
        File.WriteAllText(Path.Combine(root, "glyphstash.db"), "data");
        File.WriteAllText(marker, "clear");

        var result = WindowsUserDataUninstallCleanup.CleanupUserDataIfRequested(root, marker);

        Assert.True(result.Requested);
        Assert.True(result.Succeeded);
        Assert.False(Directory.Exists(root));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
