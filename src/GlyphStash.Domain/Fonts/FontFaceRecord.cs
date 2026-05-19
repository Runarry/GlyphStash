namespace GlyphStash.Domain.Fonts;

public sealed record FontFaceRecord(
    string FamilyName,
    string SubfamilyName,
    string FullName,
    string PostScriptName,
    int Weight,
    string Width,
    string Slant,
    FontFileRecord File);
