# 测试与平台证据（WP-30 PRE-FLIGHT）

- **日期:** 2026-11-24
- **基准:** `master` @ `28897fc6c96a0e448a377950392f403956af0357`

## 1. 本机测试运行（本审计独立执行，非引用文档）

**环境:** Windows 10.0.26300 x64（win32，Git Bash）；SDK 11.0.100；TFM net11.0。
**命令:** 从 `WorldView/` 目录执行（MTP 要求，见仓库惯例）：

```
dotnet test --no-restore            # Debug
dotnet test -c Release --no-restore # Release
```

**结果（2026-11-24 07:51–07:52 本地时间）：**

| 配置 | 总计 | 通过 | 失败 | 跳过 | 耗时 |
|---|---:|---:|---:|---:|---:|
| Debug | 728 | 728 | 0 | 0 | 27.5s |
| Release | 728 | 728 | 0 | 0 | 28.9s |

**按工程分解（Debug/Release 相同）：**

| 测试工程 | 测试数 | PRA-01 相关覆盖 |
|---|---:|---|
| WorldView.Domain.Tests | 266 | Workspace 上下文规则、Branch 世系、HistoricalTime（PRA-01 的**输入侧**相邻覆盖） |
| WorldView.Application.Tests | 133 | 会话生命周期（工作区独立性/关闭次序/晚到达保护）、Map 创建用例 |
| WorldView.Infrastructure.Tests | 318 | Core Domain 持久化、Wp20aMapCreationTests(5) |
| WorldView.Desktop.Tests | 11 | ProjectWorkspaceViewModelTests(4)+CreateMapDialogViewModelTests(6)+Fakes；纯 ViewModel，无 Avalonia headless |
| **Presentation.Tests（不存在）** | — | **0**（PRA-01 六组必需测试全缺） |

## 2. CI 证据（GitHub Actions）

- 工作流: `.github/workflows/dotnet-ci.yml`——矩阵 `windows-latest`/`macos-latest` × `Debug`/`Release`（4 job），`working-directory: WorldView`，SDK `11.0.x`，build+test（`--show-slowest-tests 20`）。
- **当前 HEAD 对应运行:** run `45291249883`（master push，"Merge pull request #26..."，2026-11-23T23:43Z）→ **completed / success**，4 job 全绿（`gh run list` 实查）。
- 早前同分支运行 45211773969（PR #26）亦全绿（含 macOS runner 排队取消后 rerun，见 WP-20a 记录）。

## 3. 平台验证现状

| 平台 | 构建证据 | 测试证据 | GUI 运行证据 |
|---|---|---|---|
| Windows | 本机 Debug+Release 构建通过；CI windows-latest ×2 | 728/728 本机双配置；CI 全绿 | WP-20a 期间开发机日常运行（无正式 M1 流程记录） |
| macOS | CI macos-latest ×2 全绿 | CI 测试 job 全绿 | **零证据**（WP-00 GAP-P1-03 未关闭：CI build/test 不能替代 GUI 运行证据） |

## 4. macOS / Windows 尚缺的验证证据（WP-30 验收 10/11/12 直接相关）

1. **macOS 实机 M1 用户流程**（验收 10）：roadmap WP-30 十二项逐项执行的实机记录——设备型号、系统版本、应用 commit、逐项结果。当前完全缺失。
2. **Windows 正式 M1 流程记录**（验收 11）：开发机日常使用不等于正式验收流程记录；需按 roadmap 十二项留痕。
3. **渲染类自动化测试**（验收 12）：快照一致性/变换正确性/平台视口行为三类测试不存在，随 WP-30 从零新建；headless Avalonia 测试基建为零（GAP-P1-04）。
4. **打包/分发形态**：无 RID/self-contained/publish 配置（FRAMEWORK_AND_PACKAGE_MATRIX.md §4）——"exit the application, restart and reopen"（验收 8）在无打包形态下的可重复性需要实施时定义（开发机 dotnet run vs 打包产物）。
5. **GUI 停机路径测试**：`Program.Main` 停机编排（`Program.cs:26-45`）无自动化覆盖（WP-00 GAP-P1-04 同源）。

## 5. 证据边界声明

- 本文件所有本机结果为审计当日独立运行；CI 结果为 `gh run list` 实查。
- 未验证 macOS 本机（本审计环境为 Windows）；未运行任何 GUI。
- 工作区未跟踪目录 `docs/map-editor/implementation/` 未纳入本审计（非 master 内容）。
