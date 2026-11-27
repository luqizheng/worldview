# 验收结果汇总（Windows 本地实测 + GitHub Actions CI + 人工验收状态）

更新时间：2026-11-26 13:12 (GMT+8)

## 一、自动化测试

### 1. Windows 本地实测（本机 RTX 笔记本，.NET SDK 11.0.100，MTP 运行器）

| 树 | 配置 | 结果 |
|---|---|---|
| PR #36 最终分支（`e2c3d4f`，含与 master `8f11250` 合并） | Debug | **947/947 通过，0 失败，0 跳过**（Domain 266 / Application 190 / Infrastructure 318 / Presentation 111 / Desktop 62） |
| PR #36 最终分支 | Release | **947/947 通过，0 失败，0 跳过** |
| PR #38 分支（`8b13c8a`，master 等效内容） | Debug | **57/57 Desktop.Tests**（含 4 个 DebugCloseFaultTests）；其余项目同基线 |
| PR #38 分支 | Release | **53/53 Desktop.Tests**（Debug-only 注入测试被编译排除，自身即 Release 隔离证明） |
| 组合态（PR38 分支 + merge PR36 最终分支，零冲突） | Debug | **951/951 通过** |
| 组合态 | Release | **947/947 通过** |

### 2. GitHub Actions CI（windows-latest / macos-latest × Debug / Release）

| PR | HEAD | 结果 |
|---|---|---|
| PR #36 | `e2c3d4f`（推送于 12:55） | ✅ **4/4 全绿**（windows/macos × Debug/Release，13:05 查证） |
| PR #38 | `d7cb7e0`（推送于 13:07） | 🟡 运行中（13:10 查证，8 条 in_progress：push+PR 双触发）；`8b13c8a` 时 4/4 绿 |
| PR #31（基线） | `b7e4045` | ✅ 4/4 全绿（已合并） |

## 二、Release 隔离验证（PR #38 专项）

- Release 构建 `WorldView.Desktop.dll` 符号扫描：`DebugCloseFault` **零命中**。
- Release 编译的 `DisposePublisherStep` 仅剩一条直调语句（`#if DEBUG` 分支消失）。
- Release 测试集不含任何注入测试（57 → 53 的差值即 Debug-only 测试）。

## 三、真实桌面 UI 人工验收

**本机为 Windows 单机环境，macOS 人工验收物理不可执行；以下按任务书要求以 PASS / FAIL / PARTIAL / NOT_RUN 如实记录。**

| 验收集 | 状态 | 说明 |
|---|---|---|
| WP-30B Phase 3A 人工清单（UI-01~UI-04，PR #31 范围） | **NOT_RUN** | 属 PR #31 验收范围，其合并时由用户裁决；本次任务未执行 |
| WP-UI-04A M-1~M-12（地图窗口人工清单） | **NOT_RUN** | 全部属 WP-UI-04C/04D（窗口层），PR #36 明确 out-of-scope 且 04C 未开始——CI/单元测试不替代真实桌面验收 |
| PR #38 注入路径人工走查（Ctrl+Shift+F9 → 首次关闭失败 → 窗口保留 → 重试成功） | **NOT_RUN（具备条件）** | 注入入口、预期行为与判定标准已在 `PHASE3A-MANUAL-ACCEPTANCE.md` §9 成文；等价的确定性自动化覆盖已通过（DebugCloseFaultTests 4/4）。用户可在 Debug 构建上按 §9 执行，预计 5 分钟 |
| Windows 桌面冒烟（启动 / 打开项目） | **NOT_RUN** | agent 沙箱无法启动 GUI/GPU 程序（企业安全策略限制 d3d12/Vulkan），需用户桌面执行 |

**结论**：自动化层面全部通过；真实桌面 UI 验收均未执行（NOT_RUN），不构成 PR #36/#38 实现完成的阻塞项（其验收窗口分属 04C/04D 与用户裁决范围），但**构成"地图工作启动门槛"第 4 条（必要人工验收完成）的未满足项**——见 FINAL-REPORT.md 的门槛判定。

## 四、无虚假通过声明

- 本阶段未删除任何测试、未降低断言强度、未添加 skip/ignore。
- 全部测试修改均具名留痕：`ProjectSessionWorkspaceLifecycleTests`（A-10，语义收敛）、`ProjectCloseOrchestrationTests`（5 处，B3 去重数据对齐），理由与依据见 PR-36-IMPLEMENTATION-REPORT.md §2.2/§2.3 及 IMPLEMENTATION-LOG.md R3 条目。
