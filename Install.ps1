#Requires -Version 5.1
<#
.SYNOPSIS
    Flink Installer — keyboard-driven Alt+Tab replacement for Windows
.DESCRIPTION
    Downloads the latest Flink.exe from GitHub and installs it to
    %LOCALAPPDATA%\Flink\. Creates a Start Menu shortcut and registers
    an uninstall entry in Windows Settings > Apps.
.EXAMPLE
    # Run directly from PowerShell:
    irm https://raw.githubusercontent.com/YOUR_USER/flink/main/Install.ps1 | iex
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AppName    = 'Flink'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Flink'
$ExePath    = Join-Path $InstallDir 'Flink.exe'
$RepoOwner  = 'YOUR_GITHUB_USER'   # <-- change this
$RepoName   = 'flink'              # <-- change this if repo name differs

function Write-Step($msg) {
    Write-Host "  $msg" -ForegroundColor Cyan
}

function Write-Ok($msg) {
    Write-Host "  OK  $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "  ERR $msg" -ForegroundColor Red
    exit 1
}

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  ███████╗██╗     ██╗███╗   ██╗██╗  ██╗" -ForegroundColor Blue
Write-Host "  ██╔════╝██║     ██║████╗  ██║██║ ██╔╝" -ForegroundColor Blue
Write-Host "  █████╗  ██║     ██║██╔██╗ ██║█████╔╝ " -ForegroundColor Blue
Write-Host "  ██╔══╝  ██║     ██║██║╚██╗██║██╔═██╗ " -ForegroundColor Blue
Write-Host "  ██║     ███████╗██║██║ ╚████║██║  ██╗" -ForegroundColor Blue
Write-Host "  ╚═╝     ╚══════╝╚═╝╚═╝  ╚═══╝╚═╝  ╚═╝" -ForegroundColor Blue
Write-Host ""
Write-Host "  Keyboard-driven Alt+Tab replacement" -ForegroundColor DarkGray
Write-Host ""

# ── Fetch latest release from GitHub ─────────────────────────────────────────

Write-Step "Fetching latest release..."

try {
    $release = Invoke-RestMethod "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    $asset   = $release.assets | Where-Object { $_.name -eq 'Flink.exe' } | Select-Object -First 1

    if (-not $asset) { Write-Fail "Flink.exe not found in latest release." }

    $version    = $release.tag_name
    $downloadUrl = $asset.browser_download_url
}
catch {
    Write-Fail "Could not fetch release info: $_"
}

Write-Ok "Found version $version"

# ── Download ──────────────────────────────────────────────────────────────────

Write-Step "Downloading Flink.exe..."

$null = New-Item -ItemType Directory -Force -Path $InstallDir

$tmpFile = Join-Path $env:TEMP 'Flink_setup.exe'

try {
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($downloadUrl, $tmpFile)
}
catch {
    Write-Fail "Download failed: $_"
}

Write-Ok "Downloaded to $tmpFile"

# ── Stop running instance ─────────────────────────────────────────────────────

Write-Step "Stopping existing Flink instance (if any)..."
Get-Process -Name 'Flink' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# ── Install ───────────────────────────────────────────────────────────────────

Write-Step "Installing to $InstallDir..."
Copy-Item $tmpFile $ExePath -Force
Remove-Item $tmpFile -Force
Write-Ok "Installed Flink.exe"

# ── Start Menu shortcut ───────────────────────────────────────────────────────

Write-Step "Creating Start Menu shortcut..."

$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuDir 'Flink.lnk'

$shell    = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath       = $ExePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description      = 'Flink — keyboard-driven window switcher'
$shortcut.Save()

Write-Ok "Start Menu shortcut created"

# ── Uninstall entry in Windows Settings > Apps ────────────────────────────────

Write-Step "Registering uninstall entry..."

$uninstallScript = Join-Path $InstallDir 'Uninstall.ps1'
@"
Remove-Item '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item '$shortcutPath' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Flink' -Name * -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Flink' -ErrorAction SilentlyContinue
Write-Host 'Flink uninstalled.' -ForegroundColor Green
"@ | Set-Content $uninstallScript

$uninstallCmd = "powershell.exe -ExecutionPolicy Bypass -File `"$uninstallScript`""

$regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Flink'
$null = New-Item -Path $regPath -Force
Set-ItemProperty $regPath -Name 'DisplayName'          -Value 'Flink'
Set-ItemProperty $regPath -Name 'DisplayVersion'       -Value $version
Set-ItemProperty $regPath -Name 'Publisher'            -Value $RepoOwner
Set-ItemProperty $regPath -Name 'InstallLocation'      -Value $InstallDir
Set-ItemProperty $regPath -Name 'UninstallString'      -Value $uninstallCmd
Set-ItemProperty $regPath -Name 'NoModify'             -Value 1 -Type DWord
Set-ItemProperty $regPath -Name 'NoRepair'             -Value 1 -Type DWord

Write-Ok "Registered in Apps & Features"

# ── Launch ────────────────────────────────────────────────────────────────────

Write-Step "Starting Flink..."
Start-Process $ExePath
Write-Ok "Flink is running"

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  Flink $version installed successfully." -ForegroundColor Green
Write-Host "  Config: $env:USERPROFILE\.flink\flink.json" -ForegroundColor DarkGray
Write-Host "  Uninstall via Windows Settings > Apps > Flink" -ForegroundColor DarkGray
Write-Host ""
