using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public enum OnlineFontDownloadStatus
{
    Queued,
    Downloading,
    Succeeded,
    Failed
}

public sealed partial class OnlineFontDownloadQueueItemViewModel : ObservableObject
{
    [ObservableProperty]
    private OnlineFontDownloadStatus _status = OnlineFontDownloadStatus.Queued;

    [ObservableProperty]
    private string _message = AppText.TranslateLiteral("等待下载");

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private DateTimeOffset? _completedAt;

    public OnlineFontDownloadQueueItemViewModel(
        RemoteFontFamily family,
        IReadOnlyList<RemoteFontStyle> styles,
        OnlineFontImportOptions options)
    {
        Family = family;
        Styles = styles.ToList();
        Options = options;
        EnqueuedAt = DateTimeOffset.UtcNow;
    }

    public RemoteFontFamily Family { get; }

    public IReadOnlyList<RemoteFontStyle> Styles { get; }

    public OnlineFontImportOptions Options { get; }

    public DateTimeOffset EnqueuedAt { get; }

    public string FamilyName => Family.FamilyName;

    public string StylesLabel => Styles.Count == 0
        ? L("未选择样式")
        : string.Join(", ", Styles.Select(style => RemoteFontStyleOptionViewModel.FormatVariantLabel(style.Variant)));

    public string StatusLabel => Status switch
    {
        OnlineFontDownloadStatus.Queued => L("排队中"),
        OnlineFontDownloadStatus.Downloading => L("下载中"),
        OnlineFontDownloadStatus.Succeeded => L("已完成"),
        OnlineFontDownloadStatus.Failed => L("失败"),
        _ => L("未知")
    };

    public bool IsFailed => Status == OnlineFontDownloadStatus.Failed;

    public bool CanRetry => IsFailed;

    public void MarkDownloading()
    {
        Status = OnlineFontDownloadStatus.Downloading;
        Message = L("正在下载");
        ErrorMessage = "";
        CompletedAt = null;
    }

    public void MarkSucceeded(string message)
    {
        Status = OnlineFontDownloadStatus.Succeeded;
        Message = string.IsNullOrWhiteSpace(message) ? L("下载完成") : message;
        ErrorMessage = "";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = OnlineFontDownloadStatus.Failed;
        Message = L("下载失败");
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void ResetForRetry()
    {
        Status = OnlineFontDownloadStatus.Queued;
        Message = L("等待重试");
        ErrorMessage = "";
        CompletedAt = null;
    }

    partial void OnStatusChanged(OnlineFontDownloadStatus value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanRetry));
    }

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(StylesLabel));
        OnPropertyChanged(nameof(StatusLabel));
    }

    private static string L(string text) => AppText.TranslateLiteral(text);
}
