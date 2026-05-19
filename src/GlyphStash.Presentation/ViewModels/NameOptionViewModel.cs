using CommunityToolkit.Mvvm.ComponentModel;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class NameOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public NameOptionViewModel(string name, int count = 0, bool isSelected = false)
    {
        Name = name;
        Count = count;
        IsSelected = isSelected;
    }

    public string Name { get; }

    public int Count { get; }

    public string CountLabel => Count == 0 ? "未绑定字体" : $"{Count:N0} 个字体";
}
