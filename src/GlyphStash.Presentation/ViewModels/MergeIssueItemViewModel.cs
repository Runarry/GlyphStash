using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeIssueItemViewModel : ObservableObject
{
    private readonly FontMergeIssue _issue;

    public MergeIssueItemViewModel(FontMergeIssue issue)
    {
        _issue = issue;
    }

    public string SeverityLabel => _issue.Severity switch
    {
        FontMergeIssueSeverity.Error => L("阻止"),
        FontMergeIssueSeverity.Warning => L("提示"),
        _ => L("信息")
    };

    public string Message => _issue.Message;

    public string Target => _issue.Target;

    public bool HasTarget => !string.IsNullOrWhiteSpace(Target);

    public bool IsError => _issue.Severity == FontMergeIssueSeverity.Error;

    public bool IsWarning => _issue.Severity == FontMergeIssueSeverity.Warning;

    public bool IsInfo => _issue.Severity == FontMergeIssueSeverity.Info;

    public void RefreshLocalizedState() => OnPropertyChanged(nameof(SeverityLabel));

}
