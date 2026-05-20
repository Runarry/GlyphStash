using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Infrastructure.FontTools;

public sealed class FontToolsWorkerClient : IFontMergeWorker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter<FontMergeMode>(),
            new JsonStringEnumConverter<FontMergeIssueKind>(),
            new JsonStringEnumConverter<FontMergeIssueSeverity>(),
            new JsonStringEnumConverter<FontMergeCodePointState>(),
            new JsonStringEnumConverter<FontMergeDecision>()
        }
    };
    private static readonly FontToolsWorkerJsonContext JsonContext = new(JsonOptions);

    private readonly Func<FontToolsWorkerLaunch> _locateWorker;

    public FontToolsWorkerClient()
        : this(() => new FontToolsWorkerLocator().Locate())
    {
    }

    public FontToolsWorkerClient(FontToolsWorkerLaunch launch)
        : this(() => launch)
    {
    }

    public FontToolsWorkerClient(Func<FontToolsWorkerLaunch> locateWorker)
    {
        _locateWorker = locateWorker;
    }

    public async Task<FontMergeWorkerPreviewResult> PreviewAsync(
        FontMergeWorkerRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var response = await RunAsync("preview", request, progress, cancellationToken).ConfigureAwait(false);
        return response.Preview ?? throw new InvalidOperationException(L("fontTools worker 未返回预览结果。"));
    }

    public async Task<FontMergeWorkerMergeResult> MergeAsync(
        FontMergeWorkerRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var response = await RunAsync("merge", request, progress, cancellationToken).ConfigureAwait(false);
        var preview = response.Preview ?? throw new InvalidOperationException(L("fontTools worker 未返回合并结果。"));
        return new FontMergeWorkerMergeResult(preview, response.OutputPath ?? request.OutputPath);
    }

    private async Task<FontToolsWorkerResponseMessage> RunAsync(
        string operation,
        FontMergeWorkerRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var launch = _locateWorker();
        var workDirectory = Path.Combine(Path.GetTempPath(), "GlyphStash.FontTools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var requestPath = Path.Combine(workDirectory, "request.json");
        var responsePath = Path.Combine(workDirectory, "response.json");

        try
        {
            var payload = new FontToolsWorkerRequestMessage(
                operation,
                request.BaseFontPath,
                request.SupplementalFontPath,
                request.Ranges,
                request.OutputPath,
                request.OutputFamilyName,
                request.MergeMode,
                responsePath);
            var requestJson = JsonSerializer.Serialize(payload, JsonContext.FontToolsWorkerRequestMessage);
            await File.WriteAllTextAsync(requestPath, requestJson, cancellationToken).ConfigureAwait(false);

            using var process = CreateProcess(launch, requestPath);
            process.Start();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

            var stdoutTask = ReadProgressAsync(process, progress, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                    ? AppText.FormatLiteral("fontTools worker 退出码：{0}", "fontTools worker exit code: {0}", process.ExitCode)
                    : stderr.Trim());
            }

            if (!File.Exists(responsePath))
            {
                throw new InvalidOperationException(L("fontTools worker 未写入响应文件。"));
            }

            await using var stream = File.OpenRead(responsePath);
            var response = await JsonSerializer.DeserializeAsync(stream, JsonContext.FontToolsWorkerResponseMessage, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(L("fontTools worker 响应格式无效。"));
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
            {
                throw new InvalidOperationException(response.ErrorMessage);
            }

            return response;
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    private static Process CreateProcess(FontToolsWorkerLaunch launch, string requestPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = launch.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in launch.ArgumentsPrefix)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(requestPath);

        return new Process { StartInfo = startInfo };
    }

    private static async Task ReadProgressAsync(
        Process process,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var item = JsonSerializer.Deserialize(line, JsonContext.FontMergeProgress);
                if (item is not null)
                {
                    progress?.Report(item);
                }
            }
            catch
            {
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static string L(string text) => AppText.TranslateLiteral(text);
}

internal sealed record FontToolsWorkerRequestMessage(
    string Operation,
    string BaseFontPath,
    string SupplementalFontPath,
    IReadOnlyList<UnicodeRange> Ranges,
    string OutputPath,
    string OutputFamilyName,
    FontMergeMode MergeMode,
    string ResponsePath);

internal sealed record FontToolsWorkerResponseMessage(
    FontMergeWorkerPreviewResult? Preview,
    string? OutputPath,
    string? ErrorMessage);
