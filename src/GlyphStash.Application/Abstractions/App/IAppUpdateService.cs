namespace GlyphStash.Application.Abstractions.App;

public interface IAppUpdateService
{
    string UpdateSource { get; }

    string CurrentVersion { get; }

    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);

    Task<AppUpdateApplyResult> DownloadAndApplyUpdateAsync(IProgress<int>? progress, CancellationToken cancellationToken);
}

public enum AppUpdateCheckStatus
{
    NoUpdateAvailable,
    UpdateAvailable,
    Unavailable,
    Failed
}

public sealed record AppUpdateCheckResult(
    AppUpdateCheckStatus Status,
    string CurrentVersion,
    string? AvailableVersion,
    string Message,
    string ReleaseNotes = "",
    bool CanDownload = false)
{
    public static AppUpdateCheckResult NoUpdate(string currentVersion, string message) =>
        new(AppUpdateCheckStatus.NoUpdateAvailable, currentVersion, null, message);

    public static AppUpdateCheckResult Available(string currentVersion, string availableVersion, string message, string releaseNotes) =>
        new(AppUpdateCheckStatus.UpdateAvailable, currentVersion, availableVersion, message, releaseNotes, CanDownload: true);

    public static AppUpdateCheckResult Unavailable(string currentVersion, string message) =>
        new(AppUpdateCheckStatus.Unavailable, currentVersion, null, message);

    public static AppUpdateCheckResult Failed(string currentVersion, string message) =>
        new(AppUpdateCheckStatus.Failed, currentVersion, null, message);
}

public sealed record AppUpdateApplyResult(
    bool Succeeded,
    string Message);
