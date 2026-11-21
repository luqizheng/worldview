# 构建 World View 新架构（Windows）。
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build WorldView.sln @args
exit $LASTEXITCODE
