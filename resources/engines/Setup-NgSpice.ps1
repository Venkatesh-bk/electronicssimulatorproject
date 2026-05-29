#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads and extracts ngspice-46 (Windows 64-bit) for the EdaSimulator project.

.DESCRIPTION
    This script automates the download and extraction of the ngspice SPICE simulation
    engine (version 46, 64-bit Windows) from the SourceForge CDN mirror into the
    project's vendor directory at:
        resources\engines\ngspice\Spice64\bin\ngspice_con.exe

    Run this script once after cloning the repository to enable real circuit simulation.

.EXAMPLE
    .\resources\engines\Setup-NgSpice.ps1
#>

param(
    [string]$Version = "46",
    [string]$DestDir = "$PSScriptRoot\ngspice"
)

$ErrorActionPreference = "Stop"

$archiveName = "ngspice-${Version}_64.7z"
$mirrorUrl   = "https://cfhcable.dl.sourceforge.net/project/ngspice/ng-spice-rework/$Version/$archiveName"
$archivePath = Join-Path $DestDir $archiveName
$exePath     = Join-Path $DestDir "Spice64\bin\ngspice_con.exe"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗"
Write-Host "║       EdaSimulator — ngspice Engine Setup            ║"
Write-Host "╚══════════════════════════════════════════════════════╝"
Write-Host ""

# Already installed?
if (Test-Path $exePath) {
    $ver = & $exePath --version 2>&1 | Select-String "ngspice-\d+" | Select-Object -First 1
    Write-Host "✅ ngspice already installed at: $exePath"
    Write-Host "   Version: $ver"
    exit 0
}

# Create destination directory
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# Step 1: Download
Write-Host "⬇ Downloading ngspice-$Version (Windows 64-bit, ~10 MB)..."
Write-Host "  Source: $mirrorUrl"

& curl.exe -L --progress-bar -o $archivePath $mirrorUrl
$size = [math]::Round((Get-Item $archivePath).Length / 1MB, 1)
Write-Host "  Downloaded: $size MB"

# Verify it's a real 7z (not an HTML error page)
$magic = [System.IO.File]::ReadAllBytes($archivePath)[0..1] | ForEach-Object { $_.ToString("X2") }
if ($magic[0] -ne "37" -or $magic[1] -ne "7A") {
    Write-Error "Download appears to be an HTML page, not a 7z archive. Check your internet connection."
    exit 1
}

# Step 2: Extract using 7za if available, otherwise download it
Write-Host ""
Write-Host "📦 Extracting archive..."

$7zaExe = $null
if (Get-Command "7za" -ErrorAction SilentlyContinue) { $7zaExe = "7za" }
elseif (Test-Path "C:\Program Files\7-Zip\7z.exe") { $7zaExe = "C:\Program Files\7-Zip\7z.exe" }
else {
    # Download standalone 7za
    $temp7za = "$env:TEMP\7za920.zip"
    Write-Host "  7za not found — downloading standalone 7za..."
    & curl.exe -L --silent -o $temp7za "https://www.7-zip.org/a/7za920.zip"
    Expand-Archive $temp7za -DestinationPath "$env:TEMP\7za_tools" -Force
    $7zaExe = "$env:TEMP\7za_tools\7za.exe"
}

& $7zaExe x $archivePath -o"$DestDir" -y | Out-Null

# Step 3: Verify
if (Test-Path $exePath) {
    Remove-Item $archivePath -Force
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════╗"
    Write-Host "║  ✅ ngspice-$Version installed successfully!           ║"
    Write-Host "║                                                      ║"
    Write-Host "║  Path: $exePath"
    Write-Host "║                                                      ║"
    Write-Host "║  EdaSimulator will detect this automatically.        ║"
    Write-Host "║  Open EdaSimulator and click Simulate to begin.     ║"
    Write-Host "╚══════════════════════════════════════════════════════╝"
} else {
    Write-Error "Extraction appeared to succeed but ngspice_con.exe was not found at: $exePath"
    exit 1
}
