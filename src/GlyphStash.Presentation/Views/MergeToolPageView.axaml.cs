using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class MergeToolPageView : UserControl
{
    private const double StackedBreakpoint = 820;
    private const double TightBreakpoint = 700;

    public MergeToolPageView()
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

        MergeSelectFontsGrid.RowDefinitions = isStacked ? RowDefinitions.Parse("Auto,Auto") : RowDefinitions.Parse("*");
        MergeSelectFontsGrid.ColumnDefinitions = isStacked ? ColumnDefinitions.Parse("*") : ColumnDefinitions.Parse("*,*");
        MergeSelectFontsGrid.RowSpacing = isStacked ? 16 : 0;
        MergeSelectFontsGrid.ColumnSpacing = isStacked ? 0 : 16;
        Grid.SetRow(MergeSupplementalPanel, isStacked ? 1 : 0);
        Grid.SetColumn(MergeSupplementalPanel, isStacked ? 0 : 1);

        MergeExportGrid.RowDefinitions = isStacked ? RowDefinitions.Parse("Auto,Auto") : RowDefinitions.Parse("*");
        MergeExportGrid.ColumnDefinitions = isStacked ? ColumnDefinitions.Parse("*") : ColumnDefinitions.Parse("*,*");
        MergeExportGrid.RowSpacing = isStacked ? 16 : 0;
        MergeExportGrid.ColumnSpacing = isStacked ? 0 : 16;
        Grid.SetRow(MergeOutputPanel, isStacked ? 1 : 0);
        Grid.SetColumn(MergeOutputPanel, isStacked ? 0 : 1);

        MergePreviewMetricsGrid.Columns = isTight ? 2 : 4;
        MergeReportGrid.Columns = isTight ? 1 : 2;
    }
}
