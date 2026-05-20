using CommunityToolkit.Mvvm.ComponentModel;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class MergeStepItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public MergeStepItemViewModel(int index, string label)
    {
        Index = index;
        Label = label;
    }

    public int Index { get; }

    public string Label { get; }
}
