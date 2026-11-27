# WP-UI-03 用户路径审计

按真实操作顺序逐步审计。每步给出：当前行为（master / **[PR34]**）、异常处理、缺失交互。
引用行号：master 为审计 HEAD `4442058`；PR34 为 `35a16ba`。

---

## 路径总览

```
启动应用 → 首页 → 打开项目 → 查看项目 → 浏览地图 → 选择地图
        → 打开地图（工作区）→ 返回项目 → 关闭项目
```

---

## 1. 启动应用

**当前行为**：`Program.Main` 构建DI 容器 → `App.OnFrameworkInitializationCompleted` 设 `MainWindow = new StartWindow()`。`StartWindow` 构造时解析 `StartViewModel` 并异步加载最近项目。

- master：`_ = vm.LoadRecentProjectsAsync()`（`StartWindow.axaml.cs` L28）——fire-and-forget，存储异常成为未观察异常。
- **[PR34]**：`ObserveRecentProjectsLoadAsync` 观察任务；失败转 `RecentProjectsLoadDiagnostic` + 应用日志，空状态中可见（`StartWindow.axaml` 新增分支）。

**异常处理**：进程级 `UnhandledException`/`UnobservedTaskException` 进控制台（`Program.cs` L27-35）；最近项目存储读取失败静默降级（`JsonRecentProjectsStore.LoadAsync` L64-67 返回空表）→ 用户看到的是"还没有最近项目"而非"读取失败"，**[PR34]** 补上了诊断文本，master 上会误导。

**缺失交互**：无。启动路径完整。

## 2. 首页（创建 / 打开 / 最近项目）

**创建新项目**：选目录 → 输名称 → `CreateProjectCommand`。未选目录时状态栏提示"请先选择保存位置"（`StartViewModel` L73-77）；`DestinationExists`、Core Domain 门槛均有对应文案（L84-100）。创建成功自动打开项目（L88）。

**打开已有项目**：文件夹选择器限选目录 → `OpenProjectCommand` → `OpenProjectUseCase`（校验 fail-closed → 注册表占位 → 建立会话 → 身份读取 → Ready）。

- 打开失败（包损坏/路径失效）：`InvalidPackage` 显示 `Report.ToSummary()`；门槛显示长文本说明（`StartViewModel` L147-149）。
- **缺失**：所有失败只出现在底部 `subtle-panel` 一行灰字（`StartWindow.axaml` L154-157），无图标/高亮/模态；长文本（门槛说明两行以上）在小面板里可读性差。

**最近项目**：

- 选择即同步 `SelectedRecentItem`；"打开所选项目"按钮经 `OpenRecentCommand`。
- 打开最近列表中已失效的路径：`OpenProjectUseCase` 返回 `InvalidPackage` → 状态栏报错。**缺失**：失效条目无标记、无"从列表移除"入口，下次启动仍显示。
- **[PR34]** 空列表有引导性空状态；列表上限 10 条（`JsonRecentProjectsStore` L94-97），无管理入口（可接受）。

## 3. 打开项目（首页 → 项目工作区）

**当前行为**：

- master：`ProjectOpened` 事件 → `OnProjectOpened` 无条件 `new ProjectWindow { ... }.Show(this)`；启动面保留。同一包再次打开走 `DuplicateOpen` → `ProjectOpened?.Invoke(result.ExistingSession!)`（`StartViewModel` L151-154）→ **为同一会话再造第二个窗口/ViewModel**，新 VM 的 `Workspaces` 集合为空，与会话真实 `_workspaces` 分叉（P0 UI-01 组成部分）。
- **[PR34]**：`_workspaceWindow` 去重（可见则 `Activate()`）；打开后 `Show()` 工作区 + `Hide()` 首页；工作区 `Closed` 后恢复首页。`ShutdownMode.OnMainWindowClose` 保证首页隐藏期间最后可见窗口关闭不退出进程。

**异常处理**：会话建立失败不产生窗口（用例层 fail-closed）；重复打开不再产生分叉窗口（PR34）。

**缺失交互**：

- master 上双开同包的既有窗口不会被激活（新窗口覆盖在旧窗口上）。
- 打开耗时操作（校验+迁移）无进度指示，仅"正在打开 …"状态文本。

## 4. 查看项目（项目工作区头部）

**当前行为**：头部显示 eyebrow "WORLD PROJECT"、项目显示名、ProjectId；右上"文件 → 关闭项目"菜单。

**缺失交互**（均无证据表明有实现）：

- 包路径不展示：`Session.CanonicalPackagePath` 无绑定。用户无法确认"我现在编辑的是哪个文件夹里的项目"。
- 无项目设置入口（`SettingsDocumentService` 基础设施已在 master 落地，UI 未接）。
- 无项目状态（Ready/Closing…）展示——正常流程不需要，但 PR34 的 `CloseFailed` 场景中用户只能从一行状态文本推断。
- `ProjectId` 显示为裸 GUID；`SessionId` 属性存在于 VM 但未绑定（冗余）。若两个会话并行，仅靠标题栏 Title"World View — 项目工作区"无法区分项目。

## 5. 浏览地图（左栏列表）

**当前行为**：窗口 `Opened` 后 `LoadMapsAsync()`（fire-and-forget，方法内全 catch）。经 `IMapQueryService.ListMapsAsync`（Core Domain 正式实现）加载。

- 加载中：无指示（状态文本仍为"尚未加载。"直到完成）。
- 空列表：显示"共 0 个地图。"小字；**[PR34]** 最近项目有了引导性空状态，地图列表没有（不一致）。
- 加载失败：`MapsStatusText = "地图列表加载失败（详见日志）。"`——**无重试按钮**，用户只能重开项目窗口。
- 列表项：名称 + 硬编码副标题"世界地图"（假数据）。

**缺失交互**：无刷新入口（创建成功后靠对话框回调刷新，外部变更无法感知——单机单窗口场景可接受，但属已知限制）；无地图右键菜单（重命名/删除均未实现）。

## 6. 选择地图 → 打开地图工作区

**当前行为**：单选地图 → 点"打开地图工作区"按钮 → `Session.OpenWorkspaceAsync`（地图存在性 + Branch/时间解析 + 晚到达保护）→ 成功后 `Workspaces.Add(workspace)`，状态栏显示 `工作区 {GUID}[{MapId GUID}] 已打开。`。

**缺失交互（本路径最大断裂）**：

- 打开后**没有任何窗口/视图切换**。所谓"工作区"只是右栏 ListBox 里多出一行：图标 + "地图 {GUID}" + "地图编辑会话" + "打开"字样——其中"打开"是纯 TextBlock 装饰（`ProjectWindow.axaml` L115-117），不可点击。
- 列表项显示两个 GUID，用户无法对应到刚才选的地图名。
- 可以重复为同一地图打开多个会话（无去重提示）——多会话本身是合法语义（多 Branch/时间），但 UI 无 Branch/时间选择，全部走默认值（VM L113-114 传 `branchId: null, historicalTimeInput: null`），用户无从感知会话间差异。

## 7. 返回项目

**当前行为**：master 语义是"启动面保留"，项目窗口只是另一个窗口——但没有任何入口从项目窗口回到首页（首页在窗口链上仍在，手动点即可）。**[PR34]** 隐藏/恢复机制下"返回项目"语义自动成立。

**缺失交互**：无显式"返回首页/切换项目"操作；多项目并存的 V1 语义（master）与 PR34 的单窗口语义存在产品级分歧，PR34 合并后应明确放弃多项目并存还是保留（见 `implementation-roadmap.md` 阶段 0 决策项）。

## 8. 关闭项目

**当前行为**：

- 菜单/主按钮路径：master `CloseProjectAsync` = `CloseAsync` + 无条件 `ProjectClosed` → 窗口 `Close()`。若 `CloseAsync` 抛异常：窗口不关，但 master 状态机把会话留在 `Closing` 永久卡死（再次关闭直接 return）。
- 标题栏 X 路径：master **完全绕过会话关闭**——窗口销毁、会话遗留 Ready、注册表占用不解除；同包再开 `DuplicateOpen` → 造出新窗口但 UI 状态分叉（P0）。
- **[PR34]**：两条路径统一——标题栏关闭先取消、走 `CloseProjectCommand`；只有 `Session.State == Closed` 才触发 `ProjectClosed` → 窗口真关 → 首页恢复；失败时 `WorkspaceStatusText = "关闭项目失败：…"`，窗口保留可重试；`CloseFailed` 状态机保证重试只补未释放的资源。

**异常处理**：**[PR34]** 后闭环（状态机 + UI 契约 + 216 行 Session 测试 + 117 行 VM 测试）。人工验收项：Windows/macOS 桌面环境重复打开/关闭、关闭失败保留窗口、关闭主窗口退出（PR34 生命周期文档 §5）。

**缺失交互**：关闭前无"未保存更改"确认——当前无编辑能力所以无脏数据，但阶段 2（工作区窗口）落地前必须补此语义（见路线图）。

## 9. 关闭应用

**当前行为**：主窗口（StartWindow）关闭 → 生命周期结束 → `Program.Main` 同步 `registry.CloseAllAsync()` → 逐会话关闭（先子工作区后资源）。**[PR34]**：仅真正 `Closed` 的会话解除登记，失败向上抛（停机阶段异常只进控制台）。

**缺失交互**：无（停机路径无 UI 是合理的；未完成事务由会话关闭序列自然完成，符合 AF-01 §4.2）。
