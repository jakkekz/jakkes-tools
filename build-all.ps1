# Build all three editions of jakkes-tools

Write-Host "Building all editions of jakkes-tools..." -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Clean
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path "publish") {
    Remove-Item -Path "publish" -Recurse -Force
}

# 1. Lite Edition (requires .NET 8 + Python)
Write-Host "`n[1/3] Building Lite Edition..." -ForegroundColor Magenta
dotnet publish CS2KZMappingTools.csproj -c Release `
    -p:PublishSingleFile=true `
    -p:SelfContained=false `
    -p:RuntimeIdentifier=win-x64 `
    -p:EnableCompressionInSingleFile=false `
    -o publish/lite

Rename-Item -Path "publish/lite/CS2KZMappingTools.exe" -NewName "jakkes-tools-Lite.exe"
$liteSize = (Get-Item "publish/lite/jakkes-tools-Lite.exe").Length / 1MB
Write-Host "✓ Lite: $([math]::Round($liteSize, 2)) MB" -ForegroundColor Green

# 2. Standard Edition (includes .NET, requires Python)
Write-Host "`n[2/3] Building Standard Edition..." -ForegroundColor Magenta
dotnet publish CS2KZMappingTools.csproj -c Release `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:RuntimeIdentifier=win-x64 `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish/standard

Rename-Item -Path "publish/standard/CS2KZMappingTools.exe" -NewName "jakkes-tools.exe"
$stdSize = (Get-Item "publish/standard/jakkes-tools.exe").Length / 1MB
Write-Host "✓ Standard: $([math]::Round($stdSize, 2)) MB" -ForegroundColor Green

# 3. Complete Edition (includes everything)
Write-Host "`n[3/3] Building Complete Edition..." -ForegroundColor Magenta
& "$PSScriptRoot\build-complete.ps1"

# Summary
Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "`nLite Edition:" -ForegroundColor Yellow
Write-Host "  File: publish/lite/jakkes-tools-Lite.exe"
Write-Host "  Size: $([math]::Round($liteSize, 2)) MB"
Write-Host "  Requires: .NET 8 + Python 3.11+"

Write-Host "`nStandard Edition:" -ForegroundColor Yellow
Write-Host "  File: publish/standard/jakkes-tools.exe"
Write-Host "  Size: $([math]::Round($stdSize, 2)) MB"
Write-Host "  Requires: Python 3.11+"

Write-Host "`nComplete Edition:" -ForegroundColor Yellow
Write-Host "  Folder: publish/complete/"
Write-Host "  Main: publish/complete/jakkes-tools-Complete.exe"
$completeSize = (Get-ChildItem -Path "publish/complete" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Size: $([math]::Round($completeSize, 2)) MB (full package)"
Write-Host "  Requires: Nothing - fully portable!"

Write-Host "`n✓ All builds complete!" -ForegroundColor Green
