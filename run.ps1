$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$runtime = Join-Path $PSScriptRoot 'node_modules\@earendil-works\pi-coding-agent\dist\cli.js'
$app = Join-Path $PSScriptRoot 'bin\Release\net7.0-windows\PiGUI.exe'
if (-not (Test-Path $runtime) -or -not (Test-Path $app)) {
    & (Join-Path $PSScriptRoot 'setup.ps1')
}
Start-Process -FilePath $app -WorkingDirectory $PSScriptRoot
