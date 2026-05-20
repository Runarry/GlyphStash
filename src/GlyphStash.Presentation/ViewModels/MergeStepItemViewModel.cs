using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class MergeStepItemViewModel : ObservableObject
{
    private readonly string _label;

    [ObservableProperty]
    private bool _isActive;

    public MergeStepItemViewModel(int index, string label)
    {
        Index = index;
        _label = label;
    }

    public int Index { get; }

    public string Label => AppText.TranslateLiteral(_label);

    public void RefreshLocalizedState() => OnPropertyChanged(nameof(Label));
}
