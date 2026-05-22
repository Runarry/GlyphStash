using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace GlyphStash.Presentation.Views;

public partial class FontDetailsPanelView : UserControl
{
    private const double SingleColumnBreakpoint = 520;

    public FontDetailsPanelView()
    {
        InitializeComponent();
        Loaded += (_, _) => ResetInitialScrollPosition();
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

        var isNarrow = width < SingleColumnBreakpoint;
        Classes.Set("narrow", isNarrow);
        FontMetadataGrid.Columns = isNarrow ? 1 : 2;
    }

    private void ResetInitialScrollPosition()
    {
        Dispatcher.UIThread.Post(
            () => SelectedFontScrollViewer.Offset = default,
            DispatcherPriority.Background);
    }
}
