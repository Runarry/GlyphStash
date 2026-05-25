param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.+][0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Python = "python",
    [switch]$SkipWorkerBuild,
    [string]$ReleaseNotes = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

function Assert-LastExitCode {
    param([string]$Description)

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

& dotnet tool restore
Assert-LastExitCode "Restoring local dotnet tools"

$PublishScript = Join-Path $Root "tools\publish-nativeaot.ps1"
& $PublishScript -Configuration $Configuration -Version $Version -Python $Python -SkipWorkerBuild:$SkipWorkerBuild
Assert-LastExitCode "Publishing GlyphStash NativeAOT package input"

$PackageInputDir = Join-Path $Root "artifacts\package\GlyphStash.Desktop-win-x64-aot"
$ReleasesDir = Join-Path $Root "artifacts\releases\win-x64"
$IconPath = Join-Path $Root "src\GlyphStash.Desktop\Assets\glyphstash.ico"

New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null

$vpkArgs = @(
    "pack",
    "--packId", "GlyphStash",
    "--packTitle", "GlyphStash",
    "--packVersion", $Version,
    "--packDir", $PackageInputDir,
    "--mainExe", "GlyphStash.Desktop.exe",
    "--runtime", "win-x64",
    "--outputDir", $ReleasesDir,
    "--icon", $IconPath,
    "--msiDeploymentTool",
    "--msiDeploymentToolVersion", $Version
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $vpkArgs += @("--releaseNotes", $ReleaseNotes)
}

& dotnet tool run vpk -- $vpkArgs
Assert-LastExitCode "Packing GlyphStash Windows installer and update artifacts"

Write-Host "Velopack Windows releases: $ReleasesDir"
