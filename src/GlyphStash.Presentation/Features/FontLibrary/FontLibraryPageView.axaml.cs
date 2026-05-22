using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class FontLibraryPageView : UserControl
{
    private const double WideDetailsBreakpoint = 1180;
    private const double TightFilterBreakpoint = 760;

    public FontLibraryPageView()
    {
        InitializeComponent();
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
        if (width <= 0 || LibraryContentGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var isTight = width < TightFilterBreakpoint;
        var showDetails = width >= WideDetailsBreakpoint;

        Classes.Set("narrow", !showDetails);
        Classes.Set("tight", isTight);

        LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(isTight ? 190 : 250);
        LibraryContentGrid.ColumnDefinitions[2].Width = new GridLength(showDetails ? 360 : 0);
        LibraryContentGrid.ColumnSpacing = isTight ? 12 : 16;
        LibraryDetailsPanel.IsVisible = showDetails;
    }
}
