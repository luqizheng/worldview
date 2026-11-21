# 运行 World View 桌面应用（Windows）。
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet run --project src/WorldView.Desktop @args
