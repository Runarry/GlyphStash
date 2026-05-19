namespace GlyphStash.Domain.Fonts;

public enum FontSourceKind
{
    Unknown = 0,
    System = 1,
    UserInstalled = 2,
    GlyphStashManaged = 3,
    Temporary = 4
}

public enum FontActivationState
{
    Unknown = 0,
    Installed = 1,
    TemporarilyEnabled = 2,
    NotEnabled = 3
}

public enum LicenseStatus
{
    Unknown = 0,
    Known = 1,
    Missing = 2,
    ExternalLink = 3
}
