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
        "defaultDecision": "SkipDuplicate",
        "note": request["operation"]
      }
    ],
    "requestedCodePointCount": 1,
    "supplementalCoverageCount": 1,
    "mergeCodePointCount": 0,
    "duplicateCodePointCount": 1,
    "missingCodePointCount": 0
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
            new FontMergeWorkerRequest("base.ttf", "patch.ttf", [new UnicodeRange(0x41, 0x41)], "", "Merged"),
            new Progress<FontMergeProgress>(progressItems.Add),
            CancellationToken.None);

        Assert.Equal(1, result.DuplicateCodePointCount);
        Assert.Single(result.Conflicts);
        Assert.Equal(FontMergeDecision.SkipDuplicate, result.Conflicts[0].DefaultDecision);
        Assert.Contains(progressItems, item => item.Percent == 42);
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
