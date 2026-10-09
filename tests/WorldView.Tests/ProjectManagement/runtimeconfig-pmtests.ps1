# Patch the xunit.console runtimeconfig to net8 so it can run tests targeting net8.
# Required after every `dotnet build` clears tests/WorldView.Tests/bin/Debug/net8.0.
# Also patches WorldView.Tests.runtimeconfig.json so the test DLL itself targets net8.

$ErrorActionPreference = 'Stop'

$bin = 'tests\WorldView.Tests\bin\Debug\net8.0'
$xunitRuntimeConfigSrc = 'C:\Users\leo\.nuget\packages\xunit.runner.console\2.9.2\tools\net6.0\xunit.console.runtimeconfig.json'
$xunitRuntimeConfigDest = Join-Path $bin 'xunit.console.runtimeconfig.json'
$testsRuntimeConfig = Join-Path $bin 'WorldView.Tests.runtimeconfig.json'

if (-not (Test-Path $xunitRuntimeConfigSrc)) {
    Write-Error "找不到 xunit.console.runtimeconfig 源：$xunitRuntimeConfigSrc"
}

Copy-Item -Path $xunitRuntimeConfigSrc -Destination $xunitRuntimeConfigDest -Force

$j = Get-Content $xunitRuntimeConfigDest -Raw | ConvertFrom-Json
$j.runtimeOptions.framework = @{ name='Microsoft.NETCore.App'; version='8.0.0' }
$j.runtimeOptions.tfm = 'net8.0'
$j | ConvertTo-Json -Depth 8 | Set-Content $xunitRuntimeConfigDest

if (Test-Path $testsRuntimeConfig) {
    $j2 = Get-Content $testsRuntimeConfig -Raw | ConvertFrom-Json
    $j2.runtimeOptions.framework = @{ name='Microsoft.NETCore.App'; version='8.0.0' }
    $j2 | ConvertTo-Json -Depth 8 | Set-Content $testsRuntimeConfig
}

Write-Host "xunit.console and WorldView.Tests runtimeconfig patched to net8."
