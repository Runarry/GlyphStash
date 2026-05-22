using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class OnlineFontsPageView : UserControl
{
    private const double StackedBreakpoint = 960;
    private const double TightBreakpoint = 720;

    public OnlineFontsPageView()
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

        var isStacked = width < StackedBreakpoint;
        var isTight = width < TightBreakpoint;

        Classes.Set("stacked", isStacked);
        Classes.Set("tight", isTight);

        OnlineContentGrid.RowDefinitions = isStacked
            ? RowDefinitions.Parse(isTight ? "280,*" : "320,*")
            : RowDefinitions.Parse("*");
        OnlineContentGrid.ColumnDefinitions = isStacked
            ? ColumnDefinitions.Parse("*")
            : ColumnDefinitions.Parse("420,*");
        OnlineContentGrid.RowSpacing = isStacked ? 16 : 0;
        OnlineContentGrid.ColumnSpacing = isStacked ? 0 : 16;

        Grid.SetRow(OnlineSearchPanel, 0);
        Grid.SetColumn(OnlineSearchPanel, 0);
        Grid.SetRow(OnlineDetailPanel, isStacked ? 1 : 0);
        Grid.SetColumn(OnlineDetailPanel, isStacked ? 0 : 1);

        OnlineMetadataGrid.Columns = isStacked ? 2 : 4;
        OnlineDownloadOptionsGrid.Columns = isTight ? 1 : 2;
    }
}
