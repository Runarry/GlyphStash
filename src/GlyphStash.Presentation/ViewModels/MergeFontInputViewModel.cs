using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeFontInputViewModel : ObservableObject
{
    private readonly string _emptyStatusLiteral;
    private FontMergeFontSelection? _selection;
    private string _filePath = "";
    private string _fileName = "";
    private string _familyName = "";
    private string _styleName = "";
    private string _format = "";
    private string _licenseText = "";
    private LicenseStatus _licenseStatus = LicenseStatus.Unknown;
    private string _errorMessage = "";
    private bool _isLoading;

    public MergeFontInputViewModel(string emptyStatusLiteral)
    {
        _emptyStatusLiteral = emptyStatusLiteral;
    }

    public FontMergeFontSelection? Selection
    {
        get => _selection;
        private set
        {
            if (SetProperty(ref _selection, value))
            {
                NotifyStateProperties();
            }
        }
    }

    public string FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    public string FamilyName
    {
        get => _familyName;
        private set => SetProperty(ref _familyName, value);
    }

    public string StyleName
    {
        get => _styleName;
        private set => SetProperty(ref _styleName, value);
    }

    public string Format
    {
        get => _format;
        private set => SetProperty(ref _format, value);
    }

    public string LicenseText
    {
        get => _licenseText;
        private set
        {
            if (SetProperty(ref _licenseText, value))
            {
                OnPropertyChanged(nameof(LicenseLabel));
            }
        }
    }

    public LicenseStatus LicenseStatus
    {
        get => _licenseStatus;
        private set
        {
            if (SetProperty(ref _licenseStatus, value))
            {
                OnPropertyChanged(nameof(IsLicenseUnknown));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                NotifyStateProperties();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public bool HasSelection => Selection is not null;

    public bool HasMetadata => HasSelection && !HasError;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !HasFile && !HasError;

    public bool IsLicenseUnknown => LicenseStatus != LicenseStatus.Known;

    public string LicenseLabel => FormatLicenseLabel(LicenseText);

    public string StatusLabel
    {
        get
        {
            if (IsLoading)
            {
                return L("正在读取字体元数据...");
            }

            if (HasError)
            {
                return ErrorMessage;
            }

            if (HasSelection)
            {
                return L("已读取字体元数据。");
            }

            return AppText.TranslateLiteral(_emptyStatusLiteral);
        }
    }

    public void SetLoading(string path)
    {
        Selection = null;
        FilePath = path;
        FileName = Path.GetFileName(path);
        FamilyName = "";
        StyleName = "";
        Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        LicenseText = "";
        LicenseStatus = LicenseStatus.Unknown;
        ErrorMessage = "";
        IsLoading = true;
        NotifyStateProperties();
    }

    public void SetMetadata(FontMetadata metadata)
    {
        var licenseText = string.IsNullOrWhiteSpace(metadata.LicenseText) ? L("未知授权") : metadata.LicenseText;
        var licenseStatus = string.IsNullOrWhiteSpace(metadata.LicenseText) ? LicenseStatus.Unknown : LicenseStatus.Known;
        var styleName = FontStyleVariantFormatter.FormatFaceStyle(metadata.SubfamilyName, metadata.Weight, metadata.Slant);
        var selection = new FontMergeFontSelection(
            metadata.FamilyName,
            styleName,
            new FontFileRef(metadata.SourcePath, metadata.Format, metadata.Sha256),
            new LicenseSnapshot(licenseStatus, licenseText));

        IsLoading = false;
        FilePath = metadata.SourcePath;
        FileName = Path.GetFileName(metadata.SourcePath);
        FamilyName = metadata.FamilyName;
        StyleName = styleName;
        Format = metadata.Format;
        LicenseText = licenseText;
        LicenseStatus = licenseStatus;
        ErrorMessage = "";
        Selection = selection;
        NotifyStateProperties();
    }

    public void SetError(string path, string message)
    {
        Selection = null;
        IsLoading = false;
        FilePath = path;
        FileName = Path.GetFileName(path);
        FamilyName = "";
        StyleName = "";
        Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        LicenseText = L("未知授权");
        LicenseStatus = LicenseStatus.Unknown;
        ErrorMessage = message;
        NotifyStateProperties();
    }

    public void Clear()
    {
        Selection = null;
        IsLoading = false;
        FilePath = "";
        FileName = "";
        FamilyName = "";
        StyleName = "";
        Format = "";
        LicenseText = "";
        LicenseStatus = LicenseStatus.Unknown;
        ErrorMessage = "";
        NotifyStateProperties();
    }

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(LicenseLabel));
        OnPropertyChanged(nameof(StatusLabel));
    }

    private void NotifyStateProperties()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasMetadata));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(StatusLabel));
    }

    private static string FormatLicenseLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.Equals(text, "未知授权", StringComparison.Ordinal)
            || string.Equals(text, "Unknown license", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Unknown licenses", StringComparison.OrdinalIgnoreCase))
        {
            return L("未知授权");
        }

        const string zhSourcePrefix = "请查看来源页面：";
        const string enSourcePrefix = "See source page: ";
        if (text.StartsWith(zhSourcePrefix, StringComparison.Ordinal))
        {
            return AppText.FormatLiteral("请查看来源页面：{0}", "See source page: {0}", text[zhSourcePrefix.Length..]);
        }

        if (text.StartsWith(enSourcePrefix, StringComparison.Ordinal))
        {
            return AppText.FormatLiteral("请查看来源页面：{0}", "See source page: {0}", text[enSourcePrefix.Length..]);
        }

        return AppText.TranslateLiteral(text);
    }
}
