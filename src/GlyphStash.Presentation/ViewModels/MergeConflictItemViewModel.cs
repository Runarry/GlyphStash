using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeConflictItemViewModel
{
    private readonly FontMergeConflictItem _item;

    public MergeConflictItemViewModel(FontMergeConflictItem item)
    {
        _item = item;
    }

    public string UnicodeLabel => _item.UnicodeLabel;

    public string Character => _item.Character;

    public string BaseStateLabel => FormatState(_item.BaseState);

    public string SupplementalStateLabel => FormatState(_item.SupplementalState);

    public string DecisionLabel => _item.DefaultDecision switch
    {
        FontMergeDecision.Merge => "合并",
        FontMergeDecision.SkipDuplicate => "跳过",
        FontMergeDecision.RecordMissing => "记录缺失",
        FontMergeDecision.Blocked => "阻止",
        _ => "未知"
    };

    public string Note => _item.Note;

    public bool IsMerge => _item.DefaultDecision == FontMergeDecision.Merge;

    public bool IsSkip => _item.DefaultDecision == FontMergeDecision.SkipDuplicate;

    public bool IsMissing => _item.DefaultDecision == FontMergeDecision.RecordMissing;

    private static string FormatState(FontMergeCodePointState state) => state switch
    {
        FontMergeCodePointState.Present => "存在",
        FontMergeCodePointState.Missing => "缺失",
        _ => "未知"
    };
}
