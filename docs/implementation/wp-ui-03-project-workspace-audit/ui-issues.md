# WP-UI-03 问题清单

优先级定义：
- **P0**：核心流程无法使用或存在数据安全风险。
- **P1**：核心交互缺失或职责边界严重不合理。
- **P2**：体验、布局、信息层级或维护性问题。
- **P3**：可选优化。

统计：P0 ×1 · P1 ×5 · P2 ×7 · P3 ×3（共 16 项）。
标注 **[PR34 已修]** 表示 PR #34 合并后消除；**[PR34 部分修]** 表示缓解但未根除。

---

## P0

### UI-01 master：关闭项目窗口遗留活动会话，且重复打开产生 UI 状态分叉

- **问题描述**：master 上标题栏 X 直接销毁 `ProjectWindow`，不经过任何会话关闭流程；`ProjectSession` 保持 Ready 继续占用数据库与注册表。此后同包再开走 `DuplicateOpen`，`StartViewModel` 调 `ProjectOpened?.Invoke(result.ExistingSession!)`，`StartWindow.OnProjectOpened` 为该旧会话**新建**窗口与 ViewModel，其 `Workspaces` 集合为空，与会话真实 `_workspaces` 分叉。另：`CloseAsync` 异常时会话永久停留在 `Closing`（再次调用直接 return），资源永不释放。
- **代码证据**：`Views/ProjectWindow.axaml.cs`（master 全文无 Closing 处理）；`ViewModels/StartViewModel.cs` L151-154；`Views/StartWindow.axaml.cs` L32-39（master 无条件 new 窗口）；`Sessions/ProjectSession.cs` master L208-253（Closing 短路）。
- **用户影响**：泄漏的数据库连接/文件句柄；被"关掉"的项目无法重新打开为可写会话；新旧两个窗口显示同一项目的不同工作区列表。
- **建议解决方向**：合入 PR #34（状态机 `CloseFailed` 可重试、标题栏拦截、窗口去重、`ShutdownMode.OnMainWindowClose`）。**[PR34 已修]**
- **验收标准**：Windows/macOS 上：标题栏关闭项目窗口后会话进入 `Closed`、注册表解除占用、同包可再开且只有一个工作区窗口；注入释放异常时窗口保留、状态文本显示失败、再次关闭成功（对应 PR34 `ProjectSessionCloseTests` / `ProjectWorkspaceViewModelCloseTests` + 人工验收清单）。

## P1

### UI-02 地图工作区没有 UI 终点，"打开地图"路径断裂

- **问题描述**：`OpenWorkspaceAsync` 成功后仅把 `MapWorkspaceSession` 加入 ListBox；无工作区窗口、无编辑器视图。列表项展示 `WorkspaceId` 与 `Context.MapId` 两个 GUID，并有一行不可点击的装饰文字"打开"。
- **代码证据**：`ProjectWorkspaceViewModel.cs` L100-129；`ProjectWindow.axaml` L98-122（`x:DataType="session:MapWorkspaceSession"`，TextBlock "打开" L115-117）。
- **用户影响**：用户执行"打开地图工作区"后看不到任何可感知变化，只多出一行乱码式 GUID；核心用户价值链在此中断。
- **建议解决方向**：WP-UI-04 落地独立 `MapWorkspaceWindow`（只承载会话上下文与占位内容区，不做绘制）；列表项改为显示地图名（经 `IMapQueryService.GetMapAsync` 或扩展 `MapSummary`）。
- **验收标准**：双击/按钮打开地图后出现独立工作区窗口，标题含地图名；同一地图可多会话并存且互不影响；关闭工作区窗口只关闭该会话；项目关闭时全部工作区窗口随之关闭。

### UI-03 地图列表副标题为硬编码假数据"世界地图"

- **问题描述**：地图项副标题固定显示"世界地图"，与数据无关；`MapSummary` 仅 `MapId + DisplayName`，无任何真实元信息支撑该文案。
- **代码证据**：`ProjectWindow.axaml` L56-57；`Abstractions/IMapQueryService.cs` L26-31。
- **用户影响**：所有地图都被标注为"世界地图"，误导用户并违反项目"不以占位数据冒充真实数据"纪律（AF-02 §4.6）。
- **建议解决方向**：立即移除该 TextBlock；元信息展示等 `MapSummary` 扩展（地图类型/空间参考/地图数量概览）后再引入真实字段。
- **验收标准**：列表项仅显示 Core Domain 返回的真实字段；grep 项目窗口 XAML 无硬编码业务名词。

### UI-04 项目级信息展示不完整：无路径、无设置入口、标识冗余

- **问题描述**：`CanonicalPackagePath` 未展示；无项目设置入口（设置文档基础设施已落地）；VM 的 `SessionId` 属性无绑定（冗余）；多项目场景下窗口标题无法区分项目。
- **代码证据**：`ProjectSession.cs` L57（路径存在）；`ProjectWindow.axaml` 全文无路径绑定、菜单仅"关闭项目"（L25-29）；`ProjectWorkspaceViewModel.cs` L52-53。
- **用户影响**：用户无法确认当前编辑的是磁盘上哪个包；报障时无法提供项目定位信息。
- **建议解决方向**：头部或"关于项目"弹层展示包路径；窗口 `Title` 拼接项目名；删除未使用的 `SessionId` 属性或绑定之；设置入口等设置功能有真实内容后再加。
- **验收标准**：项目工作区内可复制完整包路径；窗口标题含项目显示名；无未消费的公开属性。

### UI-05 错误呈现单一：所有失败挤在底部一行状态文本

- **问题描述**：打开失败（包损坏长报告）、依赖门槛长说明、关闭失败、列表加载失败全部经 `StatusText`/`MapsStatusText`/`WorkspaceStatusText` 单行灰字呈现；地图列表加载失败无重试入口。
- **代码证据**：`StartWindow.axaml` L154-157；`ProjectWindow.axaml` L63-64、L136-138；`StartViewModel.OpenPackageAsync` L147-149（`Report.ToSummary()` 长文本）。
- **用户影响**：重要失败（无法打开项目）与普通状态（"就绪"）视觉权重相同，易被忽略；长报告不可读；列表加载失败只能重开窗口恢复。
- **建议解决方向**：为失败类状态引入区分样式（警示色/图标）；包校验报告改为可展开详情；地图列表失败态附"重试"按钮（重调 `LoadMapsAsync`）。
- **验收标准**：失败状态与成功状态视觉可区分；打开失败可查看完整校验报告；列表加载失败可就地重试。

### UI-06 "工作区"区域语义混乱：会话管理冒充编辑入口，破坏性按钮占据 primary 位

- **问题描述**：右栏标题"EDITOR WORKSPACES / 工作区"+文案"开始构建和编辑你的世界"，实际只是会话列表；"关闭项目"按钮使用 primary 样式且紧邻"关闭所选工作区"（`ProjectWindow.axaml` L124-127 master），误触后果最重的操作占据视觉最高权重。
- **代码证据**：`ProjectWindow.axaml` L78-88（标题与文案）、L124-127（按钮排布）。
- **用户影响**：用户误以为此处可编辑；误触"关闭项目"直接终止会话（无确认——当前无脏数据尚可接受，语义错误已成立）。
- **建议解决方向**：该区域改名为"已打开的地图会话"，文案去除编辑承诺；"关闭项目"降为普通/危险样式并移至菜单；primary 位让给"打开地图工作区"已有按钮（或未来工作区入口）。
- **验收标准**：UI 文案不承诺不存在的编辑能力；"关闭项目"不再是 primary 按钮且不可与工作区按钮相邻混淆。

## P2

### UI-07 启动面侧栏导航为静态装饰

- **证据**：`StartWindow.axaml` L30-32："最近项目""地图与场景"为纯 TextBlock，不可点击、无目标视图。
- **影响**：暗示不存在的导航结构；"地图与场景"指向的功能整个应用都不存在。
- **建议**：移除"地图与场景"；侧栏仅保留品牌 + 项目中心（当前唯一真实视图）；未来导航能力落地后再扩展。
- **验收**：侧栏无不指向任何行为的导航项。

### UI-08 地图列表空状态薄弱（与 PR34 最近项目空状态不一致）

- **证据**：空列表仅 `MapsStatusText = "共 0 个地图。"`（VM L90）；PR34 为最近项目加了引导空状态（`StartWindow.axaml` 新增分支），地图列表未跟进。
- **影响**：新项目首屏主要区域是一张只有一行小字的空卡片，引导性差；两处空状态设计语言不一致。
- **建议**：地图列表空态复用最近项目空态的模式（标题 + 引导 + 主行动按钮"创建第一个地图"）。
- **验收**：新项目打开后空地图列表有明确引导与创建入口。

### UI-09 地图列表加载无 loading 指示且无手动刷新

- **证据**：`LoadMapsAsync` 执行期间无 UI 反馈（初始文本"尚未加载。"直到被覆盖）；创建成功外的场景（窗口重开）无刷新入口。
- **影响**：大项目加载慢时用户不知道是否在工作；异常中断后无法恢复。
- **建议**：加载中置 `MapsStatusText` 为"正在加载…"（或 IsBusy 禁用按钮）；失败态带重试（与 UI-05 合并处理）。
- **验收**：加载过程可感知；失败可重试。

### UI-10 VM 的 `Workspaces` 集合是会话真实列表的手工镜像

- **证据**：`ProjectWorkspaceViewModel` L56-59、L121（Add）、L142（Remove）；`ProjectSession` L177、L198 各自维护一份。**[PR34 部分修]**（单窗口策略规避了分叉场景，镜像模式仍在）。
- **影响**：两份状态需手工同步；未来任何绕过 VM 的会话变更（如工作区窗口自行关闭）都会造成 UI 失真。
- **建议**：`ProjectSession` 暴露工作区变更事件，VM 订阅刷新；或工作区窗口化后项目 VM 不再持有集合（见 `architecture-boundaries.md` §4）。
- **验收**：会话列表只有一份权威来源，UI 为单向投影。

### UI-11 重复装饰面板："项目数据保存在本机"出现两处且无操作价值

- **证据**：`ProjectWindow.axaml` L68-74（LOCAL WORKSPACE 面板）；`StartWindow.axaml` L35-40 与 L154-158（页脚 + 状态栏区域）。同一信息在同屏重复。
- **影响**：占用项目页左栏垂直空间（正是 PR34 刚修复过布局挤压的区域），无决策价值。
- **建议**：项目页删除该面板；启动面保留一处（页脚）即可。
- **验收**：同一信息单屏至多出现一次。

### UI-12 fire-and-forget 异步模式不一致

- **证据**：`ProjectWindow.axaml.cs` L37 `_ = loaded.LoadMapsAsync()`（方法内全 catch，实际安全）；master `StartWindow.axaml.cs` L28 同模式但曾有未观察异常风险——**[PR34 已修]** StartWindow 侧（`ObserveRecentProjectsLoadAsync`），ProjectWindow 侧保持旧模式。
- **影响**：同一代码库两种异步观察模式；`LoadMapsAsync` 的 catch 会连 `OperationCanceledException` 一起吞（当前无取消源，风险低）。
- **建议**：按 PR34 模式统一为观察式；为 `LoadMapsAsync` 补 cancellation 语义或注明无需取消。
- **验收**：全库 fire-and-forget 调用均有观察与日志；模式一致。

### UI-13 窗口层生命周期交互零自动化测试

- **证据**：`WorldView/tests/WorldView.Desktop.Tests` 仅 ViewModel 测试；PR34 生命周期文档 §5 明确 Window 生命周期不写无头假测试，转为人工验收清单。
- **影响**：Hide/Show、Closing 拦截、ShutdownMode 等关键交互回归只能靠人工；双平台 × 多场景的人工成本随交互增长。
- **建议**：评估 Avalonia Headless（`HeadlessWindowTestPlatform`）对窗口级断言的支持范围，能覆盖的部分（如 VM 事件与窗口命令的绑定契约）以构造函数级测试覆盖；纯平台行为维持人工清单。
- **验收**：窗口编排的可测部分有测试；人工验收清单文档化并随 PR 更新。

## P3

### UI-14 macOS 上 `StartWindow.Hide()` 的 dock/菜单栏行为未验证

- **证据**：PR34 `StartWindow.axaml.cs`：打开项目后 `Hide()` 主窗口。macOS 上隐藏主窗口后应用菜单栏仍显示 StartWindow 相关状态、Dock 点击行为需验证。
- **建议**：PR34 人工验收在 macOS 上补"隐藏首页期间点击 Dock 图标/切换应用"用例。
- **验收**：macOS 上首页隐藏期间 Dock 无异常窗口恢复行为，工作区关闭后首页正常前置。

### UI-15 Unicode 装饰符号跨平台渲染不一致

- **证据**：`ProjectWindow.axaml` L50（⌖）、L105（▦）；`StartWindow.axaml` L26（▦）、L128（▧）。
- **建议**：统一为 Segoe Fluent/内置字体或矢量图标资源；不阻塞任何阶段。
- **验收**：Windows/macOS 截图比对无缺字形。

### UI-16 状态文本风格不统一（GUID 直出、中英/标点混排）

- **证据**：VM L122 `"工作区 {workspace.WorkspaceId:N}[{workspace.Context.MapId}] 已打开。"`；门槛文案中文长句混用半角冒号。
- **建议**：状态文本走统一措辞表；会话标识改为短格式（前 8 位）并随 UI-02 工作区窗口化自然消失。
- **验收**：状态栏文案风格统一；无裸 GUID 直出（除非诊断需要）。
