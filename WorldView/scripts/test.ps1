# 运行全部单元测试（Windows）。
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test WorldView.sln @args
exit $LASTEXITCODE
