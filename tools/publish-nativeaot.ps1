param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$Python = "python",
    [switch]$SkipWorkerBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

function Assert-LastExitCode {
    param([string]$Description)

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($env:OS)) {
    $env:OS = "Windows_NT"
}

if (-not $SkipWorkerBuild) {
    & (Join-Path $Root "tools\fonttools-worker\build.ps1") -Python $Python
    Assert-LastExitCode "Building fontTools worker"
}

$PublishArtifactsDir = Join-Path $Root "artifacts\publish"
$PublishLog = Join-Path $PublishArtifactsDir "nativeaot-publish.log"
New-Item -ItemType Directory -Force -Path $PublishArtifactsDir | Out-Null

& dotnet publish `
    (Join-Path $Root "src\GlyphStash.Desktop\GlyphStash.Desktop.csproj") `
    -c $Configuration `
    /p:PublishProfile=win-x64-nativeaot `
    /p:Version=$Version `
    /p:AssemblyVersion=$Version `
    /p:FileVersion=$Version `
    /p:InformationalVersion=$Version `
    -v minimal `
    "-flp:logfile=$PublishLog;verbosity=normal"
Assert-LastExitCode "Publishing NativeAOT desktop app"

$PublishDir = Join-Path $Root "artifacts\publish\GlyphStash.Desktop-win-x64-aot"
$PackageDir = Join-Path $Root "artifacts\package\GlyphStash.Desktop-win-x64-aot"
$SymbolsDir = Join-Path $Root "artifacts\symbols\GlyphStash.Desktop-win-x64-aot"

if (-not (Test-Path $PublishDir)) {
    throw "NativeAOT publish directory was not created: $PublishDir"
}

foreach ($Path in @($PackageDir, $SymbolsDir)) {
    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

Get-ChildItem -LiteralPath $PublishDir -Recurse -File | ForEach-Object {
    $RelativePath = [System.IO.Path]::GetRelativePath($PublishDir, $_.FullName)
    $TargetRoot = if ($_.Extension -ieq ".pdb") { $SymbolsDir } else { $PackageDir }
    $TargetPath = Join-Path $TargetRoot $RelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $TargetPath) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $TargetPath -Force
}

Write-Host "NativeAOT user package: $PackageDir"
Write-Host "NativeAOT symbols: $SymbolsDir"
