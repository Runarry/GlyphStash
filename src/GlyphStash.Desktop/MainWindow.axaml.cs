using Avalonia.Controls;

namespace GlyphStash.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Load();
    }
}
