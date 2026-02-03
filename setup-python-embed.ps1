# Setup python-embed folder for Complete Edition
Write-Host "Setting up Python embed for Complete Edition..." -ForegroundColor Cyan

# Clean previous
if (Test-Path "python-embed") {
    Write-Host "Removing existing python-embed folder..." -ForegroundColor Yellow
    Remove-Item -Path "python-embed" -Recurse -Force
}

# Download Python embedded
Write-Host "Downloading Python 3.11.8 embedded..." -ForegroundColor Yellow
$pythonUrl = "https://www.python.org/ftp/python/3.11.8/python-3.11.8-embed-amd64.zip"
$zipPath = "python-embed.zip"

try {
    Invoke-WebRequest -Uri $pythonUrl -OutFile $zipPath
    Write-Host "Downloaded Python embedded" -ForegroundColor Green
} catch {
    Write-Host "Failed to download Python: $_" -ForegroundColor Red
    exit 1
}

# Extract
Write-Host "Extracting Python..." -ForegroundColor Yellow
Expand-Archive -Path $zipPath -DestinationPath "python-embed" -Force
Write-Host "Extracted to python-embed/" -ForegroundColor Green

# Configure to use site-packages
Write-Host "Configuring Python..." -ForegroundColor Yellow
$pthFile = "python-embed/python311._pth"
if (Test-Path $pthFile) {
    $content = Get-Content $pthFile -Raw
    $content = $content -replace '#import site', 'import site'
    if ($content -notmatch 'Lib\\site-packages') {
        $content += "`nLib`nLib/site-packages`n"
    }
    Set-Content -Path $pthFile -Value $content -NoNewline
    Write-Host "Configured site-packages" -ForegroundColor Green
}

# Install pip
Write-Host "Installing pip..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile "python-embed/get-pip.py"
& "python-embed/python.exe" "python-embed/get-pip.py" --no-warn-script-location
Write-Host "Installed pip" -ForegroundColor Green

# Install requirements
Write-Host "Installing dependencies from requirements.txt..." -ForegroundColor Yellow
& "python-embed/python.exe" -m pip install -r requirements.txt --target "python-embed/Lib/site-packages" --no-warn-script-location
Write-Host "Installed dependencies" -ForegroundColor Green

# Cleanup
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item -Path "python-embed/get-pip.py" -Force -ErrorAction SilentlyContinue
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue

# Show size
$size = (Get-ChildItem -Path "python-embed" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "`nPython embed setup complete!" -ForegroundColor Green
Write-Host "Size: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan
Write-Host "`nNow run: dotnet build -c Debug" -ForegroundColor Yellow
