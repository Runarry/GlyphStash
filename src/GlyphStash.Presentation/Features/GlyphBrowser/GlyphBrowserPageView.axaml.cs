using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class GlyphBrowserPageView : UserControl
{
    private const double StackedDetailBreakpoint = 980;
    private const double TightBreakpoint = 720;

    public GlyphBrowserPageView()
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
        if (width <= 0)
        {
            return;
        }

        var isStacked = width < StackedDetailBreakpoint;
        var isTight = width < TightBreakpoint;

        Classes.Set("stacked", isStacked);
        Classes.Set("tight", isTight);

        GlyphContentGrid.RowDefinitions = isStacked
            ? RowDefinitions.Parse("*,220")
            : RowDefinitions.Parse("*");
        GlyphContentGrid.ColumnDefinitions = isStacked
            ? ColumnDefinitions.Parse(isTight ? "190,*" : "230,*")
            : ColumnDefinitions.Parse("260,*,340");
        GlyphContentGrid.RowSpacing = isStacked ? 16 : 0;
        GlyphContentGrid.ColumnSpacing = isTight ? 12 : 16;

        Grid.SetRow(GlyphFiltersPanel, 0);
        Grid.SetColumn(GlyphFiltersPanel, 0);
        Grid.SetRowSpan(GlyphFiltersPanel, isStacked ? 2 : 1);
        Grid.SetRow(GlyphGridPanel, 0);
        Grid.SetColumn(GlyphGridPanel, 1);
        Grid.SetRow(GlyphDetailsPanel, isStacked ? 1 : 0);
        Grid.SetColumn(GlyphDetailsPanel, isStacked ? 1 : 2);

        GlyphMetadataGrid.Columns = isTight ? 1 : 2;
    }
}
