using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeIssueItemViewModel
{
    private readonly FontMergeIssue _issue;

    public MergeIssueItemViewModel(FontMergeIssue issue)
    {
        _issue = issue;
    }

    public string SeverityLabel => _issue.Severity switch
    {
        FontMergeIssueSeverity.Error => "阻止",
        FontMergeIssueSeverity.Warning => "提示",
        _ => "信息"
    };

    public string Message => _issue.Message;

    public string Target => _issue.Target;

    public bool HasTarget => !string.IsNullOrWhiteSpace(Target);

    public bool IsError => _issue.Severity == FontMergeIssueSeverity.Error;

    public bool IsWarning => _issue.Severity == FontMergeIssueSeverity.Warning;

    public bool IsInfo => _issue.Severity == FontMergeIssueSeverity.Info;
}
