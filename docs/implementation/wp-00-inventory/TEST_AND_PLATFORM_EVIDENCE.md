# Test and Platform Evidence

记录本次审计实际执行的命令、操作系统、结果与 CI 证据。**未在当前机器执行的项一律标 NOT_RUN，不编造结果。**

---

## 1. 本机实测（Windows）

| 项 | 值 |
|---|---|
| 操作系统 | Windows 10.0.26300 x64（win32） |
| .NET SDK | 11.0.100（`dotnet --version`） |
| 工作目录 | `WorldView/`（约束：`dotnet test` 必须从 WorldView 目录跑，根目录会报 MTP/VSTest 错误） |
| 基线 | `master` @ `30527eb31d73452703b1bd53c22cea5cce112aa9` |

### 1.1 构建

```
命令：dotnet build WorldView.sln -c Debug --nologo
结果：0 错误；462 警告（全部为 xUnit1051 分析器告警与 Avalonia AVLN5001 Watermark 弃用告警，无功能性问题）
耗时：约 6.4 s
```

### 1.2 测试（2026-11-23）

```
命令：dotnet test WorldView.sln -c Debug --nologo
Runner：Microsoft.Testing.Platform（global.json: {"test":{"runner":"Microsoft.Testing.Platform"}}）
结果：总计 700 / 通过 700 / 失败 0 / 跳过 0（耗时 27.7s）

  WorldView.Domain.Tests          262 通过（1.5s）
  WorldView.Application.Tests     125 通过（2.5s）
  WorldView.Infrastructure.Tests  313 通过（27.5s）
```

与上一轮盘点（基线 `3fd1815`，同一代码）结果一致；与 CI 基线一致。

### 1.3 未在本机执行（NOT_RUN 或未执行）

| 项 | 状态 | 理由 |
|---|---|---|
| Release 配置本机测试 | NOT_RUN | 审计只读纪律下未重复；CI Release 矩阵绿可作证据 |
| GUI 应用启动冒烟（Windows） | **未执行** | 只读审计不启动交互式应用；M1 验收时须补正式冒烟清单 |
| macOS 任何本地执行 | NOT_RUN | 审计环境为 Windows |
| `dotnet publish` 打包验证 | NOT_RUN | 属实施任务（GAP-P0-4），非审计范围 |

---

## 2. GitHub Actions CI 证据

| 项 | 值 |
|---|---|
| 工作流 | `.github/workflows/dotnet-ci.yml`（仓库唯一 workflow） |
| 触发 | push（master 及 stacked-PR base 分支）/ pull_request（master 及 `feat/efcore-v1-complete-model`）/ workflow_dispatch |
| 矩阵 | windows-latest + macos-latest × Debug + Release = 4 job；`fail-fast: false`；`working-directory: WorldView` |
| 步骤 | checkout@v4 → setup-dotnet@v4（11.0.x）→ `dotnet --info` → restore → build（`--no-restore`）→ test（`--no-build` + `-- --show-slowest-tests 20`，MTP 扩展参数） |
| 权限 | `contents: read`（最小权限） |
| **HEAD 运行** | **run [45183823988](https://github.com/luqizheng/worldview/actions/runs/45183823988)（2026-11-23T13:25Z，HEAD `30527eb`）：4 job 全部 success**（windows Debug/Release、macos Debug/Release） |
| 近期基线 | run 45170759757（`3fd1815`）等近 6 次 master 运行全部 success |

**CI 能证明的**：两个平台的编译 + 700 单测/集成测试通过（含真实 SQLite 端到端）。**CI 不能证明的**：GUI 应用在任一平台的实际运行（无 UI 测试、无打包产物、无冒烟 job）。

---

## 3. 测试构成与覆盖

### 3.1 规模

- 49 个测试类 / 515 个 `[Fact]` / 28 个 `[Theory]`（Theory 经 InlineData/JSON 向量展开后实际用例数更高，如 55 条 Building/Cave 聚合校验向量由 `Cd02AggregateVectorConformanceTests` 嵌入 JSON 驱动）。
- 测试栈：xunit.v3 4.0.3 + MTP；**冗余配置**：csproj 同时保留 VSTest 适配器（`Microsoft.NET.Test.Sdk` 18.12.0 + `xunit.runner.visualstudio` 4.0.1）——GAP-P1-13。

### 3.2 类型分布

| 类型 | 工程 | 落地方式 | 状态 |
|---|---|---|---|
| 纯单元 | Domain.Tests / Application.Tests | 内存替身（`FakeCoreDomainDatabase` 920 行、`FakePackageDatabase`、`InMemoryFileSystem`）；golden fixtures 以 EmbeddedResource 嵌入 | IMPLEMENTED_VERIFIED |
| 真实 SQLite 集成 | Infrastructure.Tests 全部 21 类 | 每类独立临时目录 + `SqlitePackageDatabaseFactory.CreateForFile`；部分断言直连（`SqlitePackageDatabaseTests.cs:53` 裸连接） | IMPLEMENTED_VERIFIED |
| 端到端 | `Cd01EndToEndTests`（创建→重开→查询→工作区，镜像 Program.cs 组合根、零端口替身）；`RealPackageFlowTests`；`Cd02MapEditorIntegrationTests`（ME-04 领域全链） | 真实 SQLite + 真实文件系统 | IMPLEMENTED_VERIFIED |
| 并发/事务 | `PersistenceConcurrencyTests`（12） | 共享协调器/Dispose fail-closed/飞行中 Dispose | IMPLEMENTED_VERIFIED |
| UI 测试 | — | 无任何工程引用 WorldView.Desktop；无 Avalonia headless | **MISSING** |
| 应用启停测试 | — | `Program.cs:44-45` 停机无覆盖 | **MISSING** |
| Renderer 生命周期测试 | — | 渲染器不存在 | NOT_APPLICABLE |

### 3.3 已知覆盖缺口（全部有 grep 证据）

1. Desktop 工程 0 覆盖（GAP-P1-04）。
2. `JsonRecentProjectsStore` 0 覆盖（GAP-P1-05）。
3. `MapWorkspaceSession.SwitchContextAsync` 0 覆盖（且为零调用死代码）。
4. EF 迁移防降级守卫（`__EFMigrationsHistory` 未知版本拒绝）无专门测试（GAP-P1-06）。
5. ViewModel 门槛/异常分支无单测。

---

## 4. 平台证据分账

### Windows

| 项 | 证据 | 状态 |
|---|---|---|
| Debug 构建 | 本机实测 0 错误 | ✅ |
| Debug 测试 | 本机实测 700/700 | ✅ |
| Release 构建+测试 | CI run 45183823988 windows-latest/Release success | ✅（仅 CI） |
| GUI 实际运行 | **本审计未执行冒烟**；历史会话亦无正式 GUI 冒烟记录 | ⚠️ IMPLEMENTED_UNVERIFIED |
| 打包产物 | 无（仅 framework-dependent 本地产物） | ❌ |
| 平台代码 | 路径/日志三分支 Windows 侧；`app.manifest`（supportedOS Win10，无 DPI 节点） | ✅ |

### macOS

| 项 | 证据 | 状态 |
|---|---|---|
| Debug 构建+测试 | CI run 45183823988 macos-latest/Debug success | ✅（仅 CI） |
| Release 构建+测试 | CI run 45183823988 macos-latest/Release success | ✅（仅 CI） |
| 本机任何执行 | NOT_RUN（审计环境为 Windows） | NOT_RUN |
| GUI 实际运行 | **零证据**（无 macOS UI 冒烟、无头 Avalonia 测试、发布产物） | ❌ |
| 平台代码 | `JsonRecentProjectsStore.cs:117-122`、`FileAppLog.cs:22-26` 的 macOS 路径分支（未在 macOS 上运行验证） | ⚠️ 代码存在，行为未验证 |

**结论（与 roadmap 约束一致）**：CI 双平台绿 ≠ 双平台 GUI 流程通过。M1 验收的平台门（第 11/12 项）在两个平台都未关闭；macOS 侧连"能启动的发布产物"都不存在。

---

## 5. 审计方法与证据强度声明

- 5 路并行只读取证（Desktop / Project-Map 管理 / Core 基础设施补充 / Rendering / Authoring+Tests-CI），全部结论带 file:line 引用；对否定性结论（如"零命中"）使用穷尽性 grep 并注明搜索模式。
- 对上一轮盘点（基线 `3fd1815`，代码与当前 HEAD 完全一致）的结论做了**交叉复核并修正 3 处**：① `ObjectTypeSeedingService` 经项目创建生产可达（旧结论"全部写服务无生产调用方"过绝对）；② `FieldDatasetService` 的 4 处 Application 调用方本身无 UI 入口；③ `MapInitialStateService` 在 src 的唯一命中是常量引用非服务调用。
- 主审独立完成：git 基线固定、构建、700 测试实测、CI run 核验（gh CLI）、10 份必读文档存在性与关键内容核对、工程统计。
- 本审计未修改任何业务代码/Schema/Migration/CI/正式设计文档；新增文件仅 `docs/implementation/wp-00-inventory/` 下 7 份报告。
