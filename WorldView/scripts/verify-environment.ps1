# 环境自检（Windows）：Smart App Control 关闭后的完整验证清单。
# 背景：2026-11-20 SAC 已通过注册表关闭，重启后生效；重启前 git、dotnet test、
# Avalonia 构建、桌面应用运行均被拦截。详见 docs/development/dev-environment.md。
$ErrorActionPreference = 'Continue'
Set-Location (Join-Path $PSScriptRoot '..')

Write-Host '=== 1. Smart App Control 状态（期望 Off） ==='
$sac = (Get-MpComputerStatus).SmartAppControlState
Write-Host "SmartAppControlState = $sac"

Write-Host "`n=== 2. git 可用性 ==="
git --version
git status --short --untracked-files=normal | Select-Object -First 30

Write-Host "`n=== 3. 构建全部项目 ==="
dotnet build WorldView.sln --nologo -v m
if ($LASTEXITCODE -ne 0) { Write-Error 'build failed' }

Write-Host "`n=== 4. 全部单元测试 ==="
dotnet test WorldView.sln --nologo
if ($LASTEXITCODE -ne 0) { Write-Error 'tests failed' }

Write-Host "`n=== 5. 桌面应用可执行文件就绪检查 ==="
$exe = 'src/WorldView.Desktop/bin/Debug/net11.0/WorldView.Desktop.exe'
if (Test-Path $exe) {
  Write-Host "已生成: $exe（启动请用 scripts/run.ps1）"
} else {
  Write-Warning "未找到 $exe"
}

Write-Host "`n=== 自检结束：以上全部通过后，环境即完全恢复 ==="
