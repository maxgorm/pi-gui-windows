$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$output = Join-Path $PSScriptRoot 'artifacts\win-x64'
if (Test-Path $output) {
    $resolvedRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    $resolvedOutput = [IO.Path]::GetFullPath($output)
    if (-not $resolvedOutput.StartsWith($resolvedRoot + '\artifacts\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an output path outside this repository: $resolvedOutput"
    }
    $extendedOutput = '\\?\' + $resolvedOutput
    [IO.Directory]::EnumerateFiles($extendedOutput, '*', [IO.SearchOption]::AllDirectories) |
        ForEach-Object { [IO.File]::SetAttributes($_, [IO.FileAttributes]::Normal) }
    [IO.Directory]::EnumerateDirectories($extendedOutput, '*', [IO.SearchOption]::AllDirectories) |
        Sort-Object Length -Descending |
        ForEach-Object { [IO.File]::SetAttributes($_, [IO.FileAttributes]::Directory) }
    [IO.File]::SetAttributes($extendedOutput, [IO.FileAttributes]::Directory)
    [IO.Directory]::Delete($extendedOutput, $true)
}
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts\win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item README.md,LICENSE,oauth-helper.mjs,package.json,package-lock.json -Destination artifacts\win-x64 -Force
npm install --omit=dev --prefix artifacts\win-x64
if ($LASTEXITCODE -ne 0) { throw 'Runtime packaging failed.' }
Write-Host 'Portable build created in artifacts\win-x64' -ForegroundColor Green
