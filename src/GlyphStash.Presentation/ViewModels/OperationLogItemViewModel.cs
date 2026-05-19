using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class OperationLogItemViewModel
{
    public OperationLogItemViewModel(OperationLogEntry entry)
    {
        TimeLabel = entry.Timestamp.ToLocalTime().ToString("HH:mm");
        Category = entry.Category;
        Message = entry.Message;
        StateLabel = entry.Succeeded ? "ok" : "failed";
    }

    public string TimeLabel { get; }

    public string Category { get; }

    public string Message { get; }

    public string StateLabel { get; }
}
