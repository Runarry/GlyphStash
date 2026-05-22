namespace GlyphStash.Localization;

public static class AppTextExtensions
{
    public static string L(string text) => AppText.TranslateLiteral(text);

    public static string F(string zhTemplate, string enTemplate, params object?[] args) =>
        AppText.FormatLiteral(zhTemplate, enTemplate, args);

    public static string ToUserMessage(this Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is InvalidOperationException && !string.IsNullOrWhiteSpace(candidate.Message))
            {
                return candidate.Message;
            }
        }

        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
    }
}
