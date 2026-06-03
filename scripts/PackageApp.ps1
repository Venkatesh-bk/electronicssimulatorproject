# EdaSimulator Professional Packaging & Release Script
# Target Platform: Windows (win-x64)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   EDA Simulator - Professional Packaging Pipeline      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Path configurations
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceDir = Resolve-Path (Join-Path $scriptDir "..")
$distDir = Join-Path $workspaceDir "dist"
$buildDir = Join-Path $distDir "EdaSimulator"
$archivePath = Join-Path $distDir "EdaSimulator-win-x64.zip"

Write-Host "Workspace: $workspaceDir" -ForegroundColor Gray
Write-Host "Dist Target: $distDir" -ForegroundColor Gray

# 2. Cleanup old builds
if (Test-Path $distDir) {
    Write-Host "Cleaning up previous distribution artifacts..." -ForegroundColor Yellow
    Remove-Item -Path $distDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

# 3. Compile/Publish the Application in Release mode
Write-Host "Building and publishing EdaSimulator.UI in Release (win-x64)..." -ForegroundColor Blue
$uiProject = Join-Path $workspaceDir "src\Frontend\EdaSimulator.UI\EdaSimulator.UI.csproj"

# Run dotnet publish
dotnet publish $uiProject -c Release -r win-x64 --self-contained false -o $buildDir /p:PublishReadyToRun=true

# 4. Copy Assets and Component Databases
Write-Host "Packaging databases and external assets..." -ForegroundColor Blue

$librarySrcDir = Join-Path $workspaceDir "src\Engines\EdaSimulator.Engines\Library"
$databaseFile = Join-Path $librarySrcDir "MasterComponentDatabase.json"

if (Test-Path $databaseFile) {
    Copy-Item -Path $databaseFile -Destination $buildDir -Force
    Write-Host "Copied MasterComponentDatabase.json to application root." -ForegroundColor Green
} else {
    Write-Warning "MasterComponentDatabase.json not found in library source path!"
}

# Copy Documentation
$docsTarget = New-Item -ItemType Directory -Force -Path (Join-Path $buildDir "docs")
$docsSrcDir = Join-Path $workspaceDir "docs"
if (Test-Path $docsSrcDir) {
    Copy-Item -Path (Join-Path $docsSrcDir "*") -Destination $docsTarget -Recurse -Force
    Write-Host "Copied documentation files to build folder." -ForegroundColor Green
}

# 5. Verify Executable Build Completeness
Write-Host "Running release validation checks..." -ForegroundColor Blue
$exeName = "EdaSimulator.UI.exe"
$exePath = Join-Path $buildDir $exeName

if (Test-Path $exePath) {
    Write-Host "[PASS] Application executable generated successfully: $exeName" -ForegroundColor Green
} else {
    throw "Build validation failed: $exeName not found in output directory!"
}

# 6. Compress package into distribution zip
Write-Host "Creating distribution archive: $archivePath ..." -ForegroundColor Blue
Compress-Archive -Path (Join-Path $buildDir "*") -DestinationPath $archivePath -Force

# 7. Check for WiX Toolset to compile native Windows MSI
$candlePath = Get-Command "candle" -ErrorAction SilentlyContinue
$lightPath = Get-Command "light" -ErrorAction SilentlyContinue
$msiPath = Join-Path $distDir "EdaSimulator-win-x64.msi"

if ($candlePath -and $lightPath) {
    Write-Host "WiX Toolset detected. Compiling native Windows MSI Installer..." -ForegroundColor Blue
    try {
        $wxsFile = Join-Path $scriptDir "EdaSimulator.wxs"
        $objFile = Join-Path $distDir "EdaSimulator.wixobj"
        
        # Run candle to compile .wixobj
        & "candle" -arch x64 -out $objFile $wxsFile
        
        # Run light to link and generate .msi
        & "light" -out $msiPath -ext WixUIExtension $objFile -b $buildDir
        
        Write-Host "Native MSI Installer generated successfully at: $msiPath" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to compile MSI Installer: $_"
    }
} else {
    Write-Host "WiX Toolset (candle/light) not detected in environment PATH. Skipping MSI installer generation." -ForegroundColor Yellow
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   PACKAGING COMPLETE: EdaSimulator is ready for release! " -ForegroundColor Green
Write-Host "   Archive: $archivePath" -ForegroundColor Green
if (Test-Path $msiPath) {
    Write-Host "   MSI Installer: $msiPath" -ForegroundColor Green
}
Write-Host "==========================================================" -ForegroundColor Cyan
