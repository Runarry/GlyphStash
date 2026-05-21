namespace GlyphStash.Presentation.ViewModels;

public abstract class ShellPageViewModel
{
    protected ShellPageViewModel(ShellViewModel shell)
    {
        Shell = shell;
    }

    public ShellViewModel Shell { get; }
}

public sealed class FontLibraryViewModel : ShellPageViewModel
{
    public FontLibraryViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class CollectionsViewModel : ShellPageViewModel
{
    public CollectionsViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class OnlineFontsViewModel : ShellPageViewModel
{
    public OnlineFontsViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class MergeToolViewModel : ShellPageViewModel
{
    public MergeToolViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class GlyphBrowserViewModel : ShellPageViewModel
{
    public GlyphBrowserViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class SettingsViewModel : ShellPageViewModel
{
    public SettingsViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}

public sealed class PlaceholderPageViewModel : ShellPageViewModel
{
    public PlaceholderPageViewModel(ShellViewModel shell)
        : base(shell)
    {
    }
}
