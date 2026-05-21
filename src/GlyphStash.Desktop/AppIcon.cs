using Avalonia.Controls;
using Avalonia.Platform;

namespace GlyphStash.Desktop;

internal static class AppIcon
{
    private static readonly Uri IconUri = new("avares://GlyphStash.Desktop/Assets/glyphstash.ico");

    public static WindowIcon Load()
    {
        using var stream = AssetLoader.Open(IconUri);
        return new WindowIcon(stream);
    }
}
