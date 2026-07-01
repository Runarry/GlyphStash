using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.FontTools;

namespace GlyphStash.Infrastructure.Tests;

public sealed class FontToolsWorkerClientTests
{
    [Fact]
    public async Task PreviewAsync_UsesStructuredRequestAndReadsWorkerResponse()
    {
        if (!await PythonIsAvailableAsync())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var scriptPath = Path.Combine(directory, "fake-worker.py");
        await File.WriteAllTextAsync(scriptPath, """
import argparse
import json

parser = argparse.ArgumentParser()
parser.add_argument("--request", required=True)
args = parser.parse_args()
with open(args.request, "r", encoding="utf-8") as handle:
    request = json.load(handle)
print(json.dumps({"percent": 42, "stage": "fixture", "message": "progress"}), flush=True)
response = {
  "preview": {
    "issues": [],
    "conflicts": [
      {
        "codePoint": 65,
        "character": "A",
        "baseState": "Present",
        "supplementalState": "Present",
        "defaultDecision": request["mergeMode"],
        "note": request["operation"]
      }
    ],
    "requestedCodePointCount": 1,
    "supplementalCoverageCount": 1,
    "mergeCodePointCount": 0,
    "duplicateCodePointCount": 1,
    "missingCodePointCount": 0,
    "overwrittenCodePointCount": 1
  },
  "outputPath": request["outputPath"],
  "errorMessage": ""
}
with open(request["responsePath"], "w", encoding="utf-8") as handle:
    json.dump(response, handle)
""");
        var client = new FontToolsWorkerClient(new FontToolsWorkerLaunch("python", [scriptPath], "test"));
        var progressItems = new List<FontMergeProgress>();

        var result = await client.PreviewAsync(
            new FontMergeWorkerRequest("base.ttf", "patch.ttf", [new UnicodeRange(0x41, 0x41)], "", "Merged", FontMergeMode.Overwrite),
            new Progress<FontMergeProgress>(progressItems.Add),
            CancellationToken.None);

        Assert.Equal(1, result.DuplicateCodePointCount);
        Assert.Equal(1, result.OverwrittenCodePointCount);
        Assert.Single(result.Conflicts);
        Assert.Equal(FontMergeDecision.Overwrite, result.Conflicts[0].DefaultDecision);
        Assert.Contains(progressItems, item => item.Percent == 42);
    }

    [Fact]
    public async Task WorkerClassifiesFontToolsEqualityFailureAsOpenTypeLayoutConflict()
    {
        if (!await PythonIsAvailableAsync())
        {
            return;
        }

        var workerPath = Path.Combine(FindRepositoryRoot(), "tools", "fonttools-worker", "worker.py");
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var scriptPath = Path.Combine(directory, "classify-worker-error.py");
        await File.WriteAllTextAsync(scriptPath, """
import importlib.util
import json
import sys
import types

font_tools = types.ModuleType("fontTools")
font_tools.__path__ = []
merge = types.ModuleType("fontTools.merge")
subset = types.ModuleType("fontTools.subset")
tt_lib = types.ModuleType("fontTools.ttLib")
tt_lib.__path__ = []
scale_upem = types.ModuleType("fontTools.ttLib.scaleUpem")

class Merger:
    pass

class MergeOptions:
    def __init__(self, **kwargs):
        self.drop_tables = kwargs.get("drop_tables", [])

class Options:
    pass

class Subsetter:
    pass

class TTFont:
    pass

merge.Merger = Merger
merge.Options = MergeOptions
subset.Options = Options
subset.Subsetter = Subsetter
tt_lib.TTFont = TTFont
scale_upem.scale_upem = lambda *args, **kwargs: None

sys.modules["fontTools"] = font_tools
sys.modules["fontTools.merge"] = merge
sys.modules["fontTools.subset"] = subset
sys.modules["fontTools.ttLib"] = tt_lib
sys.modules["fontTools.ttLib.scaleUpem"] = scale_upem

spec = importlib.util.spec_from_file_location("glyphstash_worker", sys.argv[1])
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)
results = {
    "equality": worker.create_merge_failure_issue(Exception("Expected all items to be equal: [NotImplemented, 0]")),
    "comparison": worker.create_merge_failure_issue(Exception("'>' not supported between instances of 'int' and 'NotImplementedType'")),
    "dropTables": worker.create_optional_merge_drop_tables({"head", "hhea", "vhea", "vmtx", "GSUB"}, {"head", "hhea", "GSUB"}),
}
print(json.dumps(results, ensure_ascii=False))
""");
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add(workerPath);
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, error);
        using var document = System.Text.Json.JsonDocument.Parse(output);
        var root = document.RootElement;
        var equality = root.GetProperty("equality");
        Assert.Equal("OpenTypeLayoutConflict", equality.GetProperty("kind").GetString());
        Assert.Equal("Error", equality.GetProperty("severity").GetString());
        Assert.Contains("OpenType 表结构不兼容", equality.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("Expected all items to be equal", equality.GetProperty("message").GetString(), StringComparison.Ordinal);

        var comparison = root.GetProperty("comparison");
        Assert.Equal("OpenTypeLayoutConflict", comparison.GetProperty("kind").GetString());
        Assert.Equal("Error", comparison.GetProperty("severity").GetString());
        Assert.Contains("OpenType 表结构不兼容", comparison.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("NotImplementedType", comparison.GetProperty("message").GetString(), StringComparison.Ordinal);

        var dropTables = root.GetProperty("dropTables").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(["vhea", "vmtx"], dropTables);
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "GlyphStash.slnx")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static async Task<bool> PythonIsAvailableAsync()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add("--version");
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
