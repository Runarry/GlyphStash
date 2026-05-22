using Avalonia;
using Avalonia.Controls;

namespace GlyphStash.Presentation.Views;

public partial class ShellView : UserControl
{
    private const double WideBreakpoint = 1180;
    private const double CompactBreakpoint = 960;

    public ShellView()
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
        if (width <= 0 || ShellRoot.ColumnDefinitions.Count < 2)
        {
            return;
        }

        var isWide = width >= WideBreakpoint;
        var isCompact = width < CompactBreakpoint;

        Classes.Set("wide", isWide);
        Classes.Set("medium", !isWide && !isCompact);
        Classes.Set("compact", isCompact);

        ShellRoot.ColumnDefinitions[0].Width = new GridLength(isCompact ? 188 : 220);
    }
}
