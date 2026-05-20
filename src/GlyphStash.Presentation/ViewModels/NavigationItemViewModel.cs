using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    private readonly string _label;
    private readonly string _title;
    private readonly string _description;

    public NavigationItemViewModel(
        string key,
        string label,
        string title,
        string description,
        string milestone,
        bool isImplemented)
    {
        Key = key;
        _label = label;
        _title = title;
        _description = description;
        Milestone = milestone;
        IsImplemented = isImplemented;
    }

    public string Key { get; }

    public string Label => AppText.TranslateLiteral(_label);

    public string Title => AppText.TranslateLiteral(_title);

    public string Description => AppText.TranslateLiteral(_description);

    public string Milestone { get; }

    public bool IsImplemented { get; }

    public bool IsFontLibrary => Key == "font-library";

    public bool IsCollections => Key == "collections";

    public bool IsSettings => Key == "settings";

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }
}
