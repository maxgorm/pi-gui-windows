$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js 22.19 or newer is required. Install it from https://nodejs.org and run setup.ps1 again.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 7 SDK or newer is required. Install it from https://dotnet.microsoft.com/download and run setup.ps1 again.'
}

Write-Host 'Installing the pinned pi agent runtime...'
npm install
if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }

Write-Host 'Building Pi GUI for Windows...'
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

Write-Host ''
Write-Host 'Setup complete. Run .\run.ps1 to open Pi GUI.' -ForegroundColor Green
