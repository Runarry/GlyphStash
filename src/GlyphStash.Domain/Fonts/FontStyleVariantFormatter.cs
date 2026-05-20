using GlyphStash.Localization;

namespace GlyphStash.Domain.Fonts;

public static class FontStyleVariantFormatter
{
    public static string FormatGoogleFontsVariant(string variant)
    {
        if (string.IsNullOrWhiteSpace(variant))
        {
            return AppText.TranslateLiteral("未知样式");
        }

        var normalized = variant.Trim();
        var isItalic = normalized.EndsWith("italic", StringComparison.OrdinalIgnoreCase);
        var weightText = isItalic ? normalized[..^6] : normalized;
        var weight = string.Equals(weightText, "regular", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(weightText)
            ? 400
            : int.TryParse(weightText, out var parsedWeight) ? parsedWeight : 0;
        var label = FormatWeightAndSlant(weight, isItalic ? "Italic" : "Normal");
        return string.IsNullOrWhiteSpace(label) ? normalized : label;
    }

    public static string FormatFaceStyle(string subfamilyName, int weight, string slant)
    {
        if (IsGoogleFontsVariantToken(subfamilyName))
        {
            return FormatGoogleFontsVariant(subfamilyName);
        }

        var label = FormatWeightAndSlant(weight, slant);
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.IsNullOrWhiteSpace(subfamilyName) ? AppText.TranslateLiteral("未知样式") : subfamilyName;
        }

        if (string.IsNullOrWhiteSpace(subfamilyName)
            || string.Equals(subfamilyName, "Regular", StringComparison.OrdinalIgnoreCase)
            || string.Equals(subfamilyName, "Italic", StringComparison.OrdinalIgnoreCase)
            || WeightName(weight).Length > 0 && subfamilyName.Contains(WeightName(weight), StringComparison.OrdinalIgnoreCase))
        {
            return label;
        }

        return subfamilyName;
    }

    public static int WeightFromGoogleFontsVariant(string variant)
    {
        var normalized = variant.Trim();
        if (normalized.EndsWith("italic", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^6];
        }

        return int.TryParse(normalized, out var weight) ? weight : 400;
    }

    public static string SlantFromGoogleFontsVariant(string variant) =>
        variant.Contains("italic", StringComparison.OrdinalIgnoreCase) ? "Italic" : "Normal";

    private static string FormatWeightAndSlant(int weight, string slant)
    {
        var name = WeightName(weight);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        return string.Equals(slant, "Italic", StringComparison.OrdinalIgnoreCase)
            ? $"{name} Italic {weight}"
            : $"{name} {weight}";
    }

    private static string WeightName(int weight) => weight switch
    {
        100 => "Thin",
        200 => "Extra Light",
        300 => "Light",
        400 => "Regular",
        500 => "Medium",
        600 => "Semi Bold",
        700 => "Bold",
        800 => "Extra Bold",
        900 => "Black",
        _ => ""
    };

    private static bool IsGoogleFontsVariantToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "regular", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "italic", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.EndsWith("italic", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^6];
        }

        return int.TryParse(normalized, out var weight) && weight is >= 100 and <= 900 && weight % 100 == 0;
    }
}
