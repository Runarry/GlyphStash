param(
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Invoke-Checked {
    param(
        [scriptblock]$Command,
        [string]$Description
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

Push-Location $Root
try {
    Invoke-Checked { & $Python -m pip install -r requirements.txt } "Installing fontTools worker dependencies"
    Invoke-Checked { & $Python -m PyInstaller --onefile --clean --name glyphstash-fonttools-worker worker.py } "Building fontTools worker"
}
finally {
    Pop-Location
}
