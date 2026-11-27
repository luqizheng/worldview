# WP-UI-03 现有实现清单

判定口径：
- **已完整实现**：功能闭环可用，异常路径有处理。
- **已部分实现**：主路径可用，但有明确缺口（缺异常分支 / 缺空状态 / 缺管理操作）。
- **仅有 UI 占位**：界面元素存在，背后无真实数据或行为。
- **尚未实现**：无代码或仅接口定义。

标注 **[PR34]** 的条目表示修复/能力由 PR #34 引入，合并前 master 上不生效。

---

## 1. 项目身份与项目信息

| 功能 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| 项目名称展示 | 已完整实现 | `ProjectWorkspaceViewModel.cs` L47（`ProjectDisplayName`，取自 `Session.ProjectDisplayName`，Core Domain 身份读取）；`ProjectWindow.axaml` L21 | 无身份服务时显示"（未命名项目 {id}）"兜底 |
| 项目 ID 展示 | 已完整实现 | `ProjectWorkspaceViewModel.cs` L50；`ProjectWindow.axaml` L23 | 与 SessionId 语义在注释中区分（L52-53） |
| 会话 ID 属性 | 已部分实现 | `ProjectWorkspaceViewModel.cs` L52-53（`SessionId` 属性）；`ProjectWindow.axaml` 无绑定 | 属性存在但 XAML 未使用，属冗余代码 |
| 项目路径展示 | **尚未实现** | `ProjectSession.CanonicalPackagePath`（`ProjectSession.cs` L57）存在于会话，但 `ProjectWindow.axaml` 全文无绑定 | 用户无法确认打开的是磁盘上哪个包 |
| 项目状态展示 | **尚未实现** | `ProjectSession.State`/`IsReady` 无任何 UI 绑定 | 生命周期状态对用户不可见（仅关闭失败时经 `WorkspaceStatusText` 间接体现） |
| 项目设置入口 | **尚未实现** | `ProjectWindow.axaml` 顶部菜单仅一项"文件 → 关闭项目"（L25-29）；`SettingsDocumentService` 等设置基础设施已存在但无 UI | 设置文档基础设施已就绪，UI 缺位 |
| 打开失败/路径失效处理 | 已部分实现 | `StartViewModel.OpenPackageAsync`（L128-160）：`InvalidPackage` 显示 `Report.ToSummary()`、`ProjectIdentityUnavailable` 显示门槛说明、`DuplicateOpen` 激活既有会话；**[PR34]** 最近项目读取失败转为可见诊断 + 日志 | 全部经 StartWindow 底部一行状态文本呈现，无差异化 UI；最近项目中的失效路径无标记/清理入口 |
| 项目不存在时打开 | 已完整实现 | `OpenProjectUseCase.ExecuteAsync` L136-152：包校验 fail-closed，返回 `InvalidPackage` 或 `Failed`，无半建立会话暴露 | |

## 2. 地图列表与地图选择

| 功能 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| 地图列表加载 | 已完整实现 | `ProjectWorkspaceViewModel.LoadMapsAsync`（L73-97）经 `IMapQueryService`（Core Domain 正式实现 `CoreDomainMapQueryService`，组合根 `Program.cs` L112）；加载中/失败/依赖门槛均有状态文本 | 加载失败仅一行文本 + 日志，无重试按钮 |
| 地图名称展示 | 已完整实现 | `MapSummary.DisplayName`（`IMapQueryService.cs` L30）；`ProjectWindow.axaml` L55 | |
| 地图描述/元信息 | **尚未实现** | `MapSummary` 仅 `MapId + DisplayName` 两个字段（`IMapQueryService.cs` L26-31） | |
| 地图副标题"世界地图" | **仅有 UI 占位（假数据）** | `ProjectWindow.axaml` L56-57：`<TextBlock Text="世界地图" ...>` 硬编码 | 违反"不以占位数据冒充真实地图"纪律（AF-02 §4.6） |
| 地图选择 | 已完整实现 | `ProjectWindow.axaml` L44（ListBox `SelectedItem="{Binding SelectedMap}"`） | 仅单选；无双击打开（打开需另按"打开地图工作区"按钮） |
| 地图创建 | 已完整实现 | `CreateMapDialog` + `CreateMapDialogViewModel`（提交防重、错误留在对话框、取消零副作用）；经会话绑定的 `CreateMapUseCase` → `IMapCreationService` 正式 Core Domain 路径；成功后父窗口刷新列表（`ProjectWindow.axaml.cs` L46-70） | 仅支持名称；无描述等元数据输入 |
| 地图删除/重命名 | **尚未实现** | 全仓库无对应用例与命令 | |
| 地图列表空状态 | 已部分实现 | 空列表时 `MapsStatusText = "共 0 个地图。"`（L90） | 仅一行小字，无引导性空状态；**[PR34]** 为最近项目做了空状态，地图列表未跟进 |
| 列表布局 | **[PR34] 已完整实现** | master `ProjectWindow.axaml` L44 固定 `Height="300"`；PR34 改为 Grid 星号行布局，本地项目页脚不再被挤出屏幕 | |

## 3. 工作区生命周期与导航

| 功能 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| 打开项目工作区 | 已部分实现 | master `StartWindow.OnProjectOpened`（L32-39）无条件 `workspaceWindow.Show(this)`，启动面保留（多项目并存 V1 语义）；**[PR34]** 改为隐藏首页 + 唯一工作区窗口，重复打开 `Activate()` 激活既有窗口 | 两版语义不同；PR34 版本与"单工作区窗口"交互更一致 |
| 窗口切换/返回首页 | **[PR34] 已完整实现** | `StartWindow.axaml.cs`：`workspaceWindow.Show(); Hide();`，`Closed` 后 `Show(); Activate();`；配合 `App.axaml.cs` `ShutdownMode.OnMainWindowClose` 避免隐藏首页期间误退出 | |
| 打开地图工作区会话 | 已完整实现（会话层） | `ProjectSession.OpenWorkspaceAsync`（L144-180）：Ready 检查、地图存在性、Branch/历史时间经 `IWorkspaceContextResolver` 解析、晚到达保护 | 见下条：UI 层无终点 |
| 工作区 UI 终点 | **尚未实现** | `ProjectWorkspaceViewModel.OpenWorkspaceAsync` 成功后仅 `Workspaces.Add(workspace)` + 状态文本；`ProjectWindow.axaml` L98-122 用 ListBox 显示 `WorkspaceId` 与 `Context.MapId`（两个 GUID） | 无工作区窗口、无编辑器 |
| 关闭单个工作区 | 已完整实现 | `CloseWorkspaceAsync`（VM L131-145）→ `Session.CloseWorkspaceAsync`（兄弟会话与项目会话不受影响）；**[PR34]** `MapWorkspaceSession.DisposeAsync` 幂等化，项目关闭路径统一 Close+Dispose | |
| 关闭项目 | **[PR34] 已完整实现**（master 为已部分实现） | master `CloseProjectAsync`（L150-156）无条件触发 `ProjectClosed`；**[PR34]** 仅在 `Session.State == Closed` 才触发一次，失败保留窗口可重试 | |
| 标题栏关闭拦截 | **[PR34] 已完整实现** | `ProjectWindow.axaml.cs`：`Closing` 先取消 → 执行 `CloseProjectCommand` → 仅 `ProjectClosed` 允许真关闭；`_closeInProgress` finally 复位 | |
| 多工作区管理 | 已部分实现 | `Workspaces` 集合 + 关闭所选；无"切换/聚焦某工作区"交互（因为无工作区窗口） | |
| 项目会话防双开 | 已完整实现 | `ProjectSessionRegistry`（路径占用 + `SessionClosed` 解除占用）；**[PR34]** `CloseAllAsync` 仅在真正 `Closed` 才 `Remove`，并解除事件订阅 | |
| 应用停机关闭 | 已完整实现 | `Program.Main` L44-45：消息循环结束后同步 `CloseAllAsync()` | |

## 4. 页面结构与视觉布局

| 功能 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| 启动面布局 | 已完整实现 | `StartWindow.axaml`：左导航（品牌/项目中心/页脚）+ 创建卡片 + 打开卡片 + 最近项目卡片 + 状态栏 | 侧栏"最近项目/地图与场景"为静态 TextBlock（装饰性） |
| 最近项目空状态 | **[PR34] 已完整实现** | `StartWindow.axaml` 新增 `IsRecentProjectsEmpty` 分支（"还没有最近项目"+ 引导文案 + 诊断） | |
| 项目工作区布局 | 已完整实现 | `ProjectWindow.axaml`：头部（项目身份 + 文件菜单）/ 左栏（地图卡片 + 本地项目页脚）/ 右栏（工作区标题 + 会话列表卡片 + 操作按钮）/ 底部状态栏 | 右栏大量空间承载的是会话日志性质内容 |
| 空状态（地图列表） | 已部分实现 | 仅 `MapsStatusText` 一行 | 无专用空状态视觉 |
| 加载状态 | 已部分实现 | 打开项目/创建项目有"正在…"状态文本；地图列表加载无 loading 指示（`LoadMapsAsync` 期间无 UI 反馈） | |
| 错误状态 | 已部分实现 | 全部经 `StatusText` / `MapsStatusText` / `WorkspaceStatusText` 单行文本 | 无高亮/模态/重试 |
| 信息密度 | 已完整实现 | 无 KPI 卡片、无图表装饰；装饰限于图标方块与 eyebrow 标签 | 符合"专业、克制"原则 |
| 平台差异 | 已完整实现（静态检查） | 唯一平台分支：`JsonRecentProjectsStore.GetDefaultBaseDirectory`（Windows/macOS/Linux 路径）；Desktop 层无 `#if` 平台分支（仅 `#if DEBUG` 开发者工具） | macOS `Hide()` 的 dock 行为未验证（人工验收项） |

## 5. 代码结构与职责边界

| 功能 | 状态 | 证据 | 说明 |
| --- | --- | --- | --- |
| View/VM 分层 | 已完整实现 | 两个窗口 code-behind 只做窗口编排（对话框、文件选择器、窗口切换）；业务命令全部在 ViewModel；VM 只经用例/端口交互 | |
| 项目级/地图级服务隔离 | 已完整实现 | 会话绑定端口按 `session.Database` 构造（`Program.cs` L109-122），项目间无共享数据库连接 | |
| 导航逻辑 | 已完整实现 | `StartViewModel.ProjectOpened` / `ProjectWorkspaceViewModel.ProjectClosed` 事件，窗口层消费；VM 不触碰 Window | |
| Session 状态管理 | 已完整实现 | `ProjectSession` 状态机 + 短临界区锁 + 晚到达保护；**[PR34]** `CloseFailed` 可重试、资源释放标记 | 设计文档 `WorldView/docs/architecture/project-session-close-lifecycle.md` |
| 重复状态 | 已部分实现 | VM `Workspaces` 集合是 `Session._workspaces` 的 UI 镜像，两边手工同步（Add/Remove 各写一遍）；master 上窗口重建时镜像会与会话真实列表分叉（P0 的一部分） | PR34 通过"单窗口 + 不重建 VM"规避，但镜像本身仍在 |
| 测试覆盖 | 已部分实现 | Session/用例：`SessionUseCaseTests`、**[PR34]** `ProjectSessionCloseTests`（216 行）；VM：`ProjectWorkspaceViewModelTests`、**[PR34]** `ProjectWorkspaceViewModelCloseTests`（117 行）、`StartViewModelTests`（+105 行）；窗口层：零自动化（PR34 文档明确以人工验收替代） | |
