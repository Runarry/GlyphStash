using System.Reflection;
using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.App;
using GlyphStash.Localization;
using Velopack;
using Velopack.Sources;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
public sealed class VelopackAppUpdateService : IAppUpdateService
{
    public const string GitHubRepositoryUrl = "https://github.com/Runarry/GlyphStash";

    private readonly Func<UpdateManager> _updateManagerFactory;
    private UpdateInfo? _pendingUpdate;

    public VelopackAppUpdateService()
        : this(() => new UpdateManager(new GithubSource(GitHubRepositoryUrl, null, prerelease: false, null)))
    {
    }

    internal VelopackAppUpdateService(Func<UpdateManager> updateManagerFactory)
    {
        _updateManagerFactory = updateManagerFactory;
    }

    public string UpdateSource => GitHubRepositoryUrl;

    public string CurrentVersion => ResolveCurrentVersion();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manager = _updateManagerFactory();
            var currentVersion = manager.CurrentVersion?.ToString() ?? CurrentVersion;
            if (!manager.IsInstalled)
            {
                _pendingUpdate = null;
                return AppUpdateCheckResult.Unavailable(
                    currentVersion,
                    AppText.TranslateLiteral("当前运行的是开发或便携构建，无法应用自动更新。"));
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (update is null)
            {
                _pendingUpdate = null;
                return AppUpdateCheckResult.NoUpdate(
                    currentVersion,
                    AppText.TranslateLiteral("当前已是最新版本。"));
            }

            _pendingUpdate = update;
            var target = update.TargetFullRelease;
            return AppUpdateCheckResult.Available(
                currentVersion,
                target.Version?.ToString() ?? AppText.TranslateLiteral("未知版本"),
                AppText.TranslateLiteral("发现新版本，可以下载并重启更新。"),
                target.NotesMarkdown ?? target.NotesHTML ?? "");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _pendingUpdate = null;
            return AppUpdateCheckResult.Failed(
                CurrentVersion,
                AppText.FormatLiteral("检查更新失败：{0}", "Update check failed: {0}", ex.Message));
        }
    }

    public async Task<AppUpdateApplyResult> DownloadAndApplyUpdateAsync(IProgress<int>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var manager = _updateManagerFactory();
            if (!manager.IsInstalled)
            {
                return new AppUpdateApplyResult(false, AppText.TranslateLiteral("当前运行的是开发或便携构建，无法应用自动更新。"));
            }

            var update = _pendingUpdate ?? await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (update is null)
            {
                return new AppUpdateApplyResult(false, AppText.TranslateLiteral("当前已是最新版本。"));
            }

            Action<int>? progressCallback = progress is null ? null : progress.Report;
            await manager.DownloadUpdatesAsync(update, progressCallback, cancellationToken).ConfigureAwait(false);
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
            return new AppUpdateApplyResult(true, AppText.TranslateLiteral("更新已下载，正在重启应用。"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AppUpdateApplyResult(
                false,
                AppText.FormatLiteral("下载或应用更新失败：{0}", "Update download or apply failed: {0}", ex.Message));
        }
    }

    private static string ResolveCurrentVersion()
    {
        var assembly = typeof(Program).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
