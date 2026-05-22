using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class CollectionsPageView : UserControl
{
    private const double StackedDetailBreakpoint = 980;
    private const double TightBreakpoint = 720;

    public CollectionsPageView()
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

        CollectionsContentGrid.RowDefinitions = isStacked
            ? RowDefinitions.Parse("*,220")
            : RowDefinitions.Parse("*");
        CollectionsContentGrid.ColumnDefinitions = isStacked
            ? ColumnDefinitions.Parse(isTight ? "190,*" : "240,*")
            : ColumnDefinitions.Parse("280,*,320");
        CollectionsContentGrid.RowSpacing = isStacked ? 16 : 0;
        CollectionsContentGrid.ColumnSpacing = isTight ? 12 : 16;

        Grid.SetRow(CollectionsListPanel, 0);
        Grid.SetColumn(CollectionsListPanel, 0);
        Grid.SetRowSpan(CollectionsListPanel, isStacked ? 2 : 1);
        Grid.SetRow(CollectionFontsPanel, 0);
        Grid.SetColumn(CollectionFontsPanel, 1);
        Grid.SetRow(CollectionDetailPanel, isStacked ? 1 : 0);
        Grid.SetColumn(CollectionDetailPanel, isStacked ? 1 : 2);

        CollectionMetadataGrid.Columns = isTight ? 1 : 2;
    }
}
