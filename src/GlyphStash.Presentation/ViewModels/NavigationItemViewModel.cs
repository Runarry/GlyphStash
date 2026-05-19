namespace GlyphStash.Presentation.ViewModels;

public sealed record NavigationItemViewModel(
    string Key,
    string Label,
    string Title,
    string Description,
    string Milestone,
    bool IsImplemented)
{
    public bool IsFontLibrary => Key == "font-library";

    public bool IsCollections => Key == "collections";

    public bool IsSettings => Key == "settings";

    public bool IsComponentDemo => Key == "component-demo";
}
