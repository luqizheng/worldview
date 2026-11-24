# WP-30A 架构说明

## 1. 项目结构与依赖方向

```
WorldView.Desktop ──────► WorldView.Application ──────► WorldView.Domain
        │                        ▲                            ▲
        │                        │                            │ (传递暴露 typed 值)
        └──► WorldView.Infrastructure ──┘            WorldView.Presentation
              (Persistence/Sqlite)                          │
                                                     WorldView.Presentation.Tests
```

- **`WorldView.Presentation`（新增）**：渲染宿主无关的呈现基础设施。唯一的项目引用是
  `WorldView.Application`（依赖方向单向：Presentation → Application → Domain）。
  HistoricalTime 等 Domain typed 值经 Application 的校验上下文传递暴露；Presentation
  **不**引用 Infrastructure、数据库、Avalonia、SkiaSharp、ViewModel 或任何 UI 调度器
  （PRA-01 契约 §1.2 禁令；由 `PresentationBoundaryTests` 以程序集引用与公开 API 反射双轨锁定）。
- **`WorldView.Presentation.Tests`（新增）**：xunit.v3 + MTP（与仓库既有测试技术栈一致，
  global.json runner 钉扎自动生效）。
- **Solution/CI 集成**：两个新项目已加入 `WorldView.sln`（src/tests 解决方案文件夹）；
  CI workflow 以 sln 级 `dotnet build`/`dotnet test` 运行，新工程与新测试自动纳入四平台矩阵。

### 1.1 Desktop 集成决策（本阶段不接线）

Desktop 尚无 viewport 创建入口（WP-30B 交付 `MapViewportControl`）。按任务 §12 纪律
"只建设必要的基础注册和可测试接口，不提前实现 UI"：

- 本阶段**不修改** Desktop/Program.cs 组合根——避免注册无消费者的死 DI 条目；
- 每-视口生命周期由 `ViewportPresentationRuntime.Create()` 提供（publisher/coordinator/
  generation tracker 三件套，视口私有，绝不全局共享）；
- WP-30B 的接线方式已在 `IMPLEMENTATION_SUMMARY.md` §7 交接：在组合根的
  `Func<ProjectSession, ProjectWorkspaceViewModel>` 工厂内按 workspace 创建 runtime，
  workspace 关闭（`MapWorkspaceSession.CloseAsync`）时 dispose runtime——与会话生命周期
  现有编排（晚到达保护/派生工作取消）对齐。

## 2. Context → SceneSnapshot 完整数据链

```
Core Domain 权威解析
  WorkspaceContextRules.TryResolve（原子快照 InSnapshotScope 内）
        │ ResolvedWorkspaceContext（ProjectId/MapId/MapName/RootHistoryId/BranchId/Lineage/typed Time）
        ▼
Infrastructure 适配（CoreDomainWorkspaceContextResolver —— 只做映射，规则归 Domain）
        │ ValidatedWorkspaceContext（MapId/BranchId/RootHistoryId/typed HistoricalTime）
        ▼                                  [WP-30A 拓宽：原边界丢弃 RootHistoryId/typed 时间]
Application 会话（ProjectSession.OpenWorkspaceAsync / MapWorkspaceSession.Context）
        │ 已校验、不可变的工作区上下文
        ▼
Presentation 映射（PresentationContextFactory.FromValidatedWorkspace）
        │ + 视口适配器捕获的 ViewportGeneration（请求创建时捕获为不可变值）
        │ + M1 固定语义态：NotEvaluated / NotConfigured / BuiltinNeutralStyle/v1 / BuiltinFontBaseline/v1
        ▼
PresentationContext（构造时计算 Fingerprint + SemanticIdentity，不可变）
        │
        ▼
协调器请求管线（PresentationRequestCoordinator.RequestSceneAsync）
  1. 激活请求（取代旧的；语义切换时发布器原子清空当前快照）
  2. 准备（可注入；M1 默认 = BlankSceneComposer 组合中性背景 → PresentationResult<BlankSceneContent>
     在组合时盖上请求指纹，绝不事后改贴）
  3. 装配（SceneAssembler.Assemble：必填/可选输入校验 + 发布器预分配版本 → SceneSnapshot）
  4. 发布校验（发布器六步检查）→ Interlocked.Exchange 原子交换
        │
        ▼
SceneSnapshotPublisher.Current（Volatile.Read；读者一次捕获、全程复用）
        │
        ▼
（WP-30B）渲染器 draw 回调：捕获一个快照引用绘制，不参与准备/发布
```

数据链每一跳都只传递不可变值；Presentation 从不回查 Domain、从不触达持久化、
从不保留会话/ViewModel/可变 Domain 实体。

## 3. 组件职责边界

| 组件 | 拥有 | 绝不拥有 |
|---|---|---|
| `PresentationContext` | 全部身份字段 + 指纹 + 语义身份（构造期固定） | 可变引用、时间戳、进程信息 |
| `PresentationContextCanonicalizer`（internal） | 版本化 canonical 字节序列化 | — |
| `PresentationResult<T>` | 子系统输出的指纹印记/schema/状态/不可变载荷 | 载荷改贴指纹 |
| `SceneSnapshot` | 完整不可变场景（图层 ImmutableArray 私有持有）+ 诊断 | 后端资源、bounds/extent、Avalonia/Skia 类型 |
| `SceneAssembler` | 必填/显式可选输入校验、显式稳定排序、整体拒绝 | 数据库/Domain 查询、绘制 |
| `SceneSnapshotPublisher` | 当前快照引用、单调版本计数、活动请求序号、期望指纹、语义清空、诊断计数 | 准备工作；渲染路径锁 |
| `PresentationRequestCoordinator` | 请求序号、latest-request-wins、完成门（closed/超替判定）、准备委托注入、请求诊断 | 直接绘制；跨视口协调 |
| `ViewportPresentationRuntime` | 每-视口三件套（generation/publisher/coordinator）+ 关闭顺序 | 全局状态 |
| `ViewportGenerationTracker` | 单调视口代（Interlocked 递增） | 时间/随机源；跨视口共享 |

锁序约定：协调器 gate → 发布器 gate（单方向，无反转）；`Current` 读取路径无锁
（`Volatile.Read`），渲染路径零锁、零等待、零准备工作（PRA-01 §6.3/§6.5）。

## 4. M1 固定语义与防伪造边界

- **WorldStateRevision = `NotEvaluated`**（显式单例语义态）：不伪造 revision、不用时间戳/
  MapId/哈希冒充。`Evaluated(revisionIdentity)` 类型已预留但 M1 不可达（factory 固定注入
  NotEvaluated）；canonical 序列化用类别字节区分，指纹可区分任意 Evaluated 值。
- **Projection = `NotConfigured`**：不猜测 EPSG/CRS。`Configured(id, version)` 预留。
  M1 的 Pan/Zoom（WP-30C）属局部二维视口相机变换，不是地理投影。
- **Style/Font**：`BuiltinNeutralStyle/v1` / `BuiltinFontBaseline/v1` 常量 token——呈现算法
  身份，非 Domain revision。
- **空白场景**：唯一背景权威 = 背景图层（`scene.layer.background.v1`）中的
  `BlankBackgroundItem`（token `builtin-neutral-background/v1`，渲染中立，实际颜色解释归
  WP-30B 渲染后端）。无第二个背景权威（Profile §5.1 单一表述要求）。
- **无 Map bounds/extent**：`SceneSnapshot`/`SceneLayer`/`SceneItem`/`BlankSceneContent`
  类型层面经反射测试证明不存在 Bounds/Extent/Width/Height 属性（Profile §5.2；当前
  `MapRecord` 无边界数据，记录为不可验证项）。
- **默认相机**：快照不携带相机/变换（视口状态归视口适配器）；确定性中性默认相机由
  WP-30B/C 的视口适配器建立，不在快照内伪造。
