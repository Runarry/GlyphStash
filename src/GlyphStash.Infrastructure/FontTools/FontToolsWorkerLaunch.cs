namespace GlyphStash.Infrastructure.FontTools;

public sealed record FontToolsWorkerLaunch(
    string FileName,
    IReadOnlyList<string> ArgumentsPrefix,
    string Description);

public sealed class FontToolsWorkerLocator
{
    private const string WorkerExecutableName = "glyphstash-fonttools-worker.exe";
    private static readonly string[] ScriptRelativePath = ["tools", "fonttools-worker", "worker.py"];

    public FontToolsWorkerLaunch Locate()
    {
        var explicitWorker = Environment.GetEnvironmentVariable("GLYPHSTASH_FONTTOOLS_WORKER");
        if (!string.IsNullOrWhiteSpace(explicitWorker) && File.Exists(explicitWorker))
        {
            return new FontToolsWorkerLaunch(explicitWorker, [], "configured bundled worker");
        }

        foreach (var candidate in EnumerateBundledExecutableCandidates())
        {
            if (File.Exists(candidate))
            {
                return new FontToolsWorkerLaunch(candidate, [], "bundled worker");
            }
        }

        var explicitScript = Environment.GetEnvironmentVariable("GLYPHSTASH_FONTTOOLS_WORKER_SCRIPT");
        if (!string.IsNullOrWhiteSpace(explicitScript) && File.Exists(explicitScript))
        {
            return CreatePythonLaunch(explicitScript, "configured development script");
        }

        foreach (var candidate in EnumerateDevelopmentScriptCandidates())
        {
            if (File.Exists(candidate))
            {
                return CreatePythonLaunch(candidate, "development script fallback");
            }
        }

        throw new InvalidOperationException("未找到内置 fontTools worker，也未找到开发脚本 fallback。");
    }

    private static FontToolsWorkerLaunch CreatePythonLaunch(string scriptPath, string description)
    {
        var python = Environment.GetEnvironmentVariable("GLYPHSTASH_PYTHON");
        if (string.IsNullOrWhiteSpace(python))
        {
            python = "python";
        }

        return new FontToolsWorkerLaunch(python, [scriptPath], description);
    }

    private static IEnumerable<string> EnumerateBundledExecutableCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, WorkerExecutableName);
        yield return Path.Combine(AppContext.BaseDirectory, "tools", "fonttools-worker", WorkerExecutableName);
    }

    private static IEnumerable<string> EnumerateDevelopmentScriptCandidates()
    {
        foreach (var root in EnumerateAncestorDirectories(AppContext.BaseDirectory)
                     .Concat(EnumerateAncestorDirectories(Directory.GetCurrentDirectory()))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine([root, .. ScriptRelativePath]);
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }
}
