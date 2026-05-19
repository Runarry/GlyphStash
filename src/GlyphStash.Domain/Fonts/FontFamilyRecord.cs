namespace GlyphStash.Domain.Fonts;

public sealed record FontFamilyRecord(
    string FamilyName,
    IReadOnlyList<FontFaceRecord> Faces,
    FontSourceKind SourceKind,
    FontActivationState ActivationState,
    LicenseStatus LicenseStatus,
    string LicenseText,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Collections,
    bool IsFavorite)
{
    public int StyleCount => Faces.Count;

    public string PrimaryFilePath => Faces.FirstOrDefault()?.File.Path ?? string.Empty;

    public string FormatSummary => string.Join(", ", Faces.Select(face => face.File.Format).Distinct(StringComparer.OrdinalIgnoreCase));
}
