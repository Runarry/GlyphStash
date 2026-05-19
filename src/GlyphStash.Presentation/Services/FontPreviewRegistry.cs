using Avalonia.Media;
using Avalonia.Media.Fonts;
using GlyphStash.Presentation.ViewModels;

namespace GlyphStash.Presentation.Services;

public interface IFontPreviewRegistry
{
    FontFamily Resolve(FontFaceItemViewModel? face, string fallbackFamilyName);
}

public sealed class AvaloniaFontPreviewRegistry : IFontPreviewRegistry
{
    private static readonly Uri PreviewCollectionKey = new("fonts:GlyphStashPreview", UriKind.Absolute);
    private readonly PreviewFontCollection _collection = new(PreviewCollectionKey);
    private readonly HashSet<string> _registeredPaths = new(StringComparer.OrdinalIgnoreCase);

    public AvaloniaFontPreviewRegistry()
    {
        FontManager.Current.AddFontCollection(_collection);
    }

    public FontFamily Resolve(FontFaceItemViewModel? face, string fallbackFamilyName)
    {
        var familyName = string.IsNullOrWhiteSpace(face?.FamilyName) ? fallbackFamilyName : face.FamilyName;
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return FontFamily.Default;
        }

        if (face is null || string.IsNullOrWhiteSpace(face.FilePath) || !File.Exists(face.FilePath))
        {
            return new FontFamily(familyName);
        }

        var fullPath = Path.GetFullPath(face.FilePath);
        if (_registeredPaths.Contains(fullPath) || TryRegister(fullPath))
        {
            return new FontFamily(PreviewCollectionKey, familyName);
        }

        return new FontFamily(familyName);
    }

    private bool TryRegister(string fullPath)
    {
        try
        {
            if (!_collection.TryAddFontSource(new Uri(fullPath, UriKind.Absolute)))
            {
                return false;
            }

            _registeredPaths.Add(fullPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class PreviewFontCollection(Uri key) : FontCollectionBase
    {
        public override Uri Key { get; } = key;
    }
}

public sealed class NullFontPreviewRegistry : IFontPreviewRegistry
{
    public static NullFontPreviewRegistry Instance { get; } = new();

    private NullFontPreviewRegistry()
    {
    }

    public FontFamily Resolve(FontFaceItemViewModel? face, string fallbackFamilyName)
    {
        var familyName = string.IsNullOrWhiteSpace(face?.FamilyName) ? fallbackFamilyName : face.FamilyName;
        return string.IsNullOrWhiteSpace(familyName) ? FontFamily.Default : new FontFamily(familyName);
    }
}
