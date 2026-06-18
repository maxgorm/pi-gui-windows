$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build smoke test failed.' }

$request = '{"id":"smoke","type":"get_state"}'
$output = $request | node .\node_modules\@earendil-works\pi-coding-agent\dist\cli.js --mode rpc --provider openai-codex --model gpt-5.5 --approve
$response = $output | Where-Object { $_ -match '"id":"smoke"' } | Select-Object -First 1 | ConvertFrom-Json
if (-not $response.success) { throw 'pi RPC smoke test did not succeed.' }
if ($response.data.model.id -ne 'gpt-5.5') { throw "Expected gpt-5.5, received $($response.data.model.id)." }
if ($response.data.thinkingLevel -ne 'medium') { throw "Expected medium reasoning, received $($response.data.thinkingLevel)." }

$copilotOutput = $request | node .\node_modules\@earendil-works\pi-coding-agent\dist\cli.js --mode rpc --provider github-copilot --model gpt-5.3-codex --approve
$copilot = $copilotOutput | Where-Object { $_ -match '"id":"smoke"' } | Select-Object -First 1 | ConvertFrom-Json
if (-not $copilot.success -or $copilot.data.model.provider -ne 'github-copilot') { throw 'GitHub Copilot RPC smoke test did not succeed.' }

node .\approval-smoke.mjs
if ($LASTEXITCODE -ne 0) { throw 'Approval RPC smoke test failed.' }

$originalPath = $env:PATH
try {
    $env:PATH = "$env:WINDIR\System32"
    $stress = Start-Process -FilePath '.\bin\Release\net7.0-windows\PiGUI.exe' -ArgumentList '--runtime-stress' -WorkingDirectory $PSScriptRoot -Wait -PassThru
    if ($stress.ExitCode -ne 0) { throw 'Packaged runtime discovery or process lifecycle stress test failed.' }
}
finally { $env:PATH = $originalPath }

$uiStress = Start-Process -FilePath '.\bin\Release\net7.0-windows\PiGUI.exe' -ArgumentList '--ui-stress' -WorkingDirectory $PSScriptRoot -Wait -PassThru
if ($uiStress.ExitCode -ne 0) { throw 'Drop-down interaction regression test failed.' }

Write-Host 'Build, provider RPC, approval RPC, Node discovery, lifecycle, and UI interaction tests passed.' -ForegroundColor Green
