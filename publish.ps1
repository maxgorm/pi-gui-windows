$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$output = Join-Path $PSScriptRoot 'artifacts\win-x64'
New-Item -ItemType Directory -Force -Path $output | Out-Null
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts\win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item README.md,LICENSE,oauth-helper.mjs,pi-gui-approval-extension.ts,package.json,package-lock.json -Destination artifacts\win-x64 -Force
npm install --omit=dev --prefix artifacts\win-x64
if ($LASTEXITCODE -ne 0) { throw 'Runtime packaging failed.' }
Write-Host 'Portable build created in artifacts\win-x64' -ForegroundColor Green
