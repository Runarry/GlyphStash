param(
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $Root
try {
    & $Python -m pip install -r requirements.txt
    & $Python -m PyInstaller --onefile --clean --name glyphstash-fonttools-worker worker.py
}
finally {
    Pop-Location
}
