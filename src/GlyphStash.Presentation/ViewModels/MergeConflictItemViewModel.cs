using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeConflictItemViewModel : ObservableObject
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
        FontMergeDecision.Merge => L("合并"),
        FontMergeDecision.SkipDuplicate => L("跳过"),
        FontMergeDecision.RecordMissing => L("记录缺失"),
        FontMergeDecision.Blocked => L("阻止"),
        FontMergeDecision.Overwrite => AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "Overwrite" : "覆盖",
        _ => L("未知")
    };

    public string Note => _item.Note;

    public bool IsMerge => _item.DefaultDecision == FontMergeDecision.Merge;

    public bool IsSkip => _item.DefaultDecision == FontMergeDecision.SkipDuplicate;

    public bool IsMissing => _item.DefaultDecision == FontMergeDecision.RecordMissing;

    public bool IsOverwrite => _item.DefaultDecision == FontMergeDecision.Overwrite;

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(BaseStateLabel));
        OnPropertyChanged(nameof(SupplementalStateLabel));
        OnPropertyChanged(nameof(DecisionLabel));
    }

    private static string FormatState(FontMergeCodePointState state) => state switch
    {
        FontMergeCodePointState.Present => L("存在"),
        FontMergeCodePointState.Missing => L("缺失"),
        _ => L("未知")
    };

}
