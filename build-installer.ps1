#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectFile = Join-Path $PSScriptRoot 'Flink.csproj'
$IssFile     = Join-Path $PSScriptRoot 'Flink.iss'
$IsccPaths   = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)

function Write-Step($msg) { Write-Host "`n  >> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  OK  $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "`n  ERR $msg`n" -ForegroundColor Red; exit 1 }

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Flink - Build + Installer" -ForegroundColor Blue
Write-Host "  ──────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── Check Inno Setup ─────────────────────────────────────────────────────────

Write-Step "Looking for Inno Setup..."

$Iscc = $IsccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    Write-Fail "Inno Setup not found. Download it from https://jrsoftware.org/isdl.php"
}

Write-Ok "Found: $Iscc"

# ── Stop running Flink ────────────────────────────────────────────────────────

Write-Step "Stopping Flink (if running)..."

$proc = Get-Process -Name 'Flink' -ErrorAction SilentlyContinue
if ($proc) {
    $proc | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    Write-Ok "Stopped"
} else {
    Write-Ok "Not running"
}

# ── dotnet publish ────────────────────────────────────────────────────────────

Write-Step "Building release..."

& dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --nologo -v:m

if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet publish failed." }

$ExePath = Join-Path $PSScriptRoot 'bin\Release\net9.0-windows\win-x64\publish\Flink.exe'
if (-not (Test-Path $ExePath)) { Write-Fail "Flink.exe not found after publish." }

Write-Ok "Flink.exe built ($('{0:N1} MB' -f ((Get-Item $ExePath).Length / 1MB)))"

# ── Inno Setup ────────────────────────────────────────────────────────────────

Write-Step "Compiling installer..."

& $Iscc $IssFile /Q

if ($LASTEXITCODE -ne 0) { Write-Fail "Inno Setup compilation failed." }

$Installer = Get-ChildItem (Join-Path $PSScriptRoot 'installer\*.exe') |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

if (-not $Installer) { Write-Fail "Installer not found in installer\ folder." }

Write-Ok "Installer built: $($Installer.Name) ($('{0:N1} MB' -f ($Installer.Length / 1MB)))"

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Done! Installer is at:" -ForegroundColor Green
Write-Host "  $($Installer.FullName)" -ForegroundColor White
Write-Host ""

# Ask to run installer
$answer = Read-Host "  Run installer now? [y/N]"
if ($answer -match '^[yY]') {
    Start-Process $Installer.FullName
}
