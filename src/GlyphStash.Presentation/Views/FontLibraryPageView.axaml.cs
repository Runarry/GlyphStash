using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class FontLibraryPageView : UserControl
{
    private const double TightFilterBreakpoint = 760;
    private bool _showInlineDetails;

    public FontLibraryPageView()
    {
        InitializeComponent();
        UpdateResponsiveLayout(Bounds.Width);
    }

    public void SetInlineDetailsMode(bool showInlineDetails)
    {
        _showInlineDetails = showInlineDetails;
        UpdateResponsiveLayout(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            UpdateResponsiveLayout(Bounds.Width);
        }
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (width <= 0 || LibraryContentGrid.ColumnDefinitions.Count < 2)
        {
            return;
        }

        var isTight = width < TightFilterBreakpoint;

        Classes.Set("narrow", _showInlineDetails);
        Classes.Set("tight", isTight);

        LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(isTight ? 190 : 250);
        LibraryContentGrid.ColumnSpacing = isTight ? 12 : 16;
        InlineDetailsPanel.IsVisible = _showInlineDetails;
        InlineDetailsPanel.MaxHeight = isTight ? 260 : 300;
    }
}
