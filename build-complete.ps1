# Build Complete Edition - jakkes-tools with embedded Python and .NET Runtime
# This creates a fully portable package with no external dependencies

Write-Host "Building jakkes-tools-Complete..." -ForegroundColor Cyan

# Clean previous builds
if (Test-Path "publish/complete") {
    Remove-Item -Path "publish/complete" -Recurse -Force
}

# Build self-contained .NET app
Write-Host "`nBuilding .NET application..." -ForegroundColor Yellow
dotnet publish CS2KZMappingTools.csproj -c Release `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:RuntimeIdentifier=win-x64 `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish/complete

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Rename executable
Write-Host "`nRenaming executable..." -ForegroundColor Yellow
Rename-Item -Path "publish/complete/CS2KZMappingTools.exe" -NewName "jakkes-tools-Complete.exe"

# Copy Python scripts
Write-Host "`nCopying Python scripts..." -ForegroundColor Yellow
Copy-Item -Path "scripts" -Destination "publish/complete/scripts" -Recurse -Force
Copy-Item -Path "utils" -Destination "publish/complete/utils" -Recurse -Force
Copy-Item -Path "requirements.txt" -Destination "publish/complete/" -Force

# Download embedded Python
Write-Host "`nDownloading embedded Python 3.11.8..." -ForegroundColor Yellow
$pythonZip = "publish/python-embed.zip"
if (-not (Test-Path $pythonZip)) {
    Invoke-WebRequest -Uri "https://www.python.org/ftp/python/3.11.8/python-3.11.8-embed-amd64.zip" -OutFile $pythonZip
}

# Extract Python
Write-Host "Extracting Python..." -ForegroundColor Yellow
Expand-Archive -Path $pythonZip -DestinationPath "publish/complete/python" -Force

# Configure Python to allow pip (uncomment import site in python311._pth)
$pthFile = "publish/complete/python/python311._pth"
if (Test-Path $pthFile) {
    $content = Get-Content $pthFile
    $content = $content -replace '#import site', 'import site'
    Set-Content -Path $pthFile -Value $content
}

# Install pip in embedded Python
Write-Host "`nInstalling pip..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile "publish/complete/python/get-pip.py"
& "publish/complete/python/python.exe" "publish/complete/python/get-pip.py"

# Install required Python packages
Write-Host "`nInstalling Python dependencies..." -ForegroundColor Yellow
& "publish/complete/python/python.exe" -m pip install -r requirements.txt --target "publish/complete/python/Lib/site-packages"

# Create README
Write-Host "`nCreating README..." -ForegroundColor Yellow
@"
# jakkes-tools-Complete - Fully Portable Edition

This package includes everything you need to run jakkes-tools with no external dependencies:
- .NET 8 Runtime (embedded)
- Python 3.11.8 (embedded)
- All Python dependencies (VPK library, etc.)

## How to Use

1. Extract this entire folder anywhere on your computer
2. Run jakkes-tools-Complete.exe
3. That's it! No installation needed.

## Package Contents

- jakkes-tools-Complete.exe - Main application
- python/ - Embedded Python 3.11.8 with all dependencies
- scripts/ - Python import scripts
- utils/ - Utility modules

## System Requirements

- Windows 10/11 (64-bit)
- ~500MB disk space

## Notes

- This package is completely portable - no installation required
- You can copy this folder to any location or USB drive
- Python is configured to use the embedded interpreter automatically

Version: $(git rev-parse --short HEAD 2>$null)
Built: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@ | Out-File -FilePath "publish/complete/README.txt" -Encoding UTF8

Write-Host "`n✓ Build complete!" -ForegroundColor Green
Write-Host "Output: publish/complete/jakkes-tools-Complete.exe" -ForegroundColor Green
Write-Host "`nPackage size:" -ForegroundColor Cyan
$size = (Get-ChildItem -Path "publish/complete" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "$([math]::Round($size, 2)) MB" -ForegroundColor Cyan

Write-Host "`nTo create a distributable ZIP:" -ForegroundColor Yellow
Write-Host "Compress-Archive -Path 'publish/complete/*' -DestinationPath 'jakkes-tools-Complete.zip' -Force" -ForegroundColor White
