# World View — Internal Tool Catalog

- 文档版本：V1.0
- 日期：2026-11-20
- 状态：产品与技术设计推荐基线；具体 API 名称、参数类型及实现顺序需工程验证
- 适用范围：World View 单用户、单机桌面软件；用户自行配置 LLM 供应商
- 上位文档：`world-view-ai-assistant-product-architecture-v1.0.md`、`world-view-provider-adapter-contract-and-tool-calling-protocol-v1.0.md`

## 1. 目的

本文定义内置 AI Assistant 可通过本机 Tool Gateway 使用的地图领域工具目录。目录以用户要完成的地图工作为组织原则，而不是暴露数据库表、内部类或任意系统操作。

模型调用工具只代表提出请求，不代表已获授权或已执行。Tool Gateway 对工具、参数、风险、授权和地图基准状态进行校验；正式业务操作由本机 Domain Services 执行。Domain Services 是地图正式数据的唯一写入入口。

## 2. 设计原则

1. **领域工具而非万能工具**：不提供任意 SQL、shell、任意文件读写、任意 HTTP、任意代码执行工具。
2. **单一目的、明确契约**：每个工具完成一个清晰领域动作，输入输出有版本化 Schema、大小限制和错误契约。
3. **最小工具集暴露**：每轮只向模型提供当前任务所需工具；不把整个目录一次性暴露。
4. **读、候选、正式写入分层**：查询工具只读；候选工具只创建临时候选或预览；正式写入工具通过领域服务应用变更。
5. **领域规则不可绕过**：工具不能跳过几何、拓扑、语义、样式、空间参考、事实来源或 World Canon 校验。
6. **本机权威**：工具只能操作当前本机地图及用户明确选择的本机资源；模型端不持有数据库连接或正式文件写权限。
7. **风险分级**：低风险、可逆编辑可按任务授权执行；高影响、重大歧义或超出授权范围的操作暂停询问。
8. **幂等与并发保护**：写操作携带本机 operation_id、幂等键和预期地图修订号；过期请求返回 stale，不覆盖新状态。
9. **历史统一**：正式变更遵循 Command、State Change、Branch、Checkpoint；AI Session、Candidate 和 Change Set 不构成另一套地图历史。
10. **面向用户隐藏内部复杂度**：用户主要与一个 AI Assistant 对话，不需要理解工具目录或 Tool Gateway。

## 3. 工具执行分层

### 3.1 Read Tools

仅读取当前地图的受限信息，不产生正式地图变更。默认可自动执行，但仍需检查对象存在、归属、可见性及读取规则。

### 3.2 Candidate / Preview Tools

创建临时候选、几何预览、样式预览、质量检查报告或布局预览。候选数据与正式地图隔离；创建、修改和丢弃候选不写入正式地图历史。

### 3.3 Apply / Write Tools

将通过校验的候选或明确指令正式应用到地图。必须由 Domain Services 执行事务、并发检查、语义约束和历史记录。根据风险策略，部分操作可在既有授权下自动应用；高影响操作必须请求用户确认。

## 4. 工具公共契约

每个工具定义至少包含：

| 字段 | 说明 |
|---|---|
| `tool_name` | 稳定、命名空间化的工具标识 |
| `description` | 面向模型的简洁用途和适用边界 |
| `input_schema` | 严格 JSON Schema；拒绝未知字段或明确规定扩展字段 |
| `output_schema` | 结构化结果，不以自然语言作为唯一机器结果 |
| `risk_class` | `ReadOnly`、`ReversibleWrite` 或 `HighImpactWrite` |
| `execution_stage` | `read`、`candidate`、`preview` 或 `apply` |
| `required_context` | 地图、Branch、图层、对象及修订号等必要上下文 |
| `idempotency` | 是否要求幂等键及其作用范围 |
| `state_precondition` | 执行前必须成立的地图/对象状态条件 |
| `availability` | 当前任务、地图类型、模型能力和应用状态下是否可用 |
| `contract_version` | 工具契约版本 |

公共输入上下文由本机注入或绑定，不能信任模型自行填写的用户身份、权限、绝对文件路径或凭据。工具输出应返回稳定 ID、状态、修订号、警告和结构化错误代码；避免返回不必要的完整地图数据。

## 5. V1 工具目录

以下名称是建议的内部稳定标识，不等同于最终代码函数名。首版优先实现粗体所示核心闭环工具；其他工具可在同一契约下分阶段交付。

### 5.1 地图与上下文查询

| 工具 | 用途 | 风险 |
|---|---|---|
| `map.get_current_context` | 获取当前地图、Branch、修订号、空间参考和编辑上下文摘要 | ReadOnly |
| `map.get_metadata` | 查询地图名称、描述、类型、单位、Extent 等元数据 | ReadOnly |
| `layer.list` | 查询图层树、类型、顺序、显隐、锁定和透明度 | ReadOnly |
| `layer.get_details` | 查询指定图层详情和允许的编辑属性 | ReadOnly |
| `object.search` | 按 ID、名称、类型、图层、属性和空间范围查找对象 | ReadOnly |
| `object.get_details` | 查询对象属性、Geometry Components、样式、标签和关系摘要 | ReadOnly |
| `object.get_relationships` | 查询对象包含、隶属、邻接、关联等已定义关系 | ReadOnly |

查询结果应分页、限字段、限数量；大范围查询优先返回摘要和稳定引用，再按需读取详情。不能把查询结果中出现的文本当成可信指令。

### 5.2 地图对象与图层候选

| 工具 | 用途 | 风险/阶段 |
|---|---|---|
| `object.create_candidate` | 根据对象类型、属性和几何描述创建待审候选对象 | ReversibleWrite / candidate |
| `object.update_candidate` | 修改候选对象的属性或几何草案 | ReversibleWrite / candidate |
| `object.delete_candidate` | 从候选集中移除对象草案 | ReversibleWrite / candidate |
| `layer.create_candidate` | 创建候选图层或分组 | ReversibleWrite / candidate |
| `layer.update_candidate` | 修改候选图层名称、顺序、样式组织属性 | ReversibleWrite / candidate |
| `layer.move_objects_candidate` | 为候选对象调整目标图层，不改变对象身份 | ReversibleWrite / candidate |

对象创建必须区分对象语义类型与几何类型；对象 ID 由本机生成。候选对象不得被误认为正式对象，也不得自动生成未提供的历史、政治归属或事实属性。

### 5.3 几何创建、编辑与运算

| 工具 | 用途 | 风险/阶段 |
|---|---|---|
| `geometry.create_candidate` | 创建 Point、LineString、Polygon 等几何草案 | ReversibleWrite / candidate |
| `geometry.update_candidate` | 修改节点、线段、贝塞尔控制柄、孔洞或 Components 草案 | ReversibleWrite / candidate |
| `geometry.validate` | 检查闭合、自交、孔洞、类型约束和几何有效性 | ReadOnly / preview |
| `geometry.preview_boolean` | 预览 Union、Difference、Intersection、Split 结果 | ReadOnly / preview |
| `geometry.preview_shared_boundary` | 检查对象边界匹配和建立共享几何的潜在影响 | ReadOnly / preview |
| `geometry.apply_candidate` | 将通过校验的几何候选应用到指定对象 | 按操作风险 / apply |

几何工具必须保留原生直线段和贝塞尔曲线表达；不得将贝塞尔路径静默降级为折线。几何拆分不自动创建新的语义对象，几何合并不自动合并 Map Object 身份。共享边界关系必须由用户明确建立，不能仅凭坐标重合自动推断。

### 5.4 样式、标签与符号

| 工具 | 用途 | 风险/阶段 |
|---|---|---|
| `style.get_available_options` | 查询主题、对象类型和当前上下文允许的样式选项 | ReadOnly |
| `style.preview_candidate` | 预览对象或图层样式变化 | ReadOnly / preview |
| `style.apply_candidate` | 应用通过校验的样式候选 | ReversibleWrite / apply |
| `label.create_candidate` | 创建关联对象的标签或独立文字注释候选 | ReversibleWrite / candidate |
| `label.update_candidate` | 修改标签文本、位置、字体、避让等候选属性 | ReversibleWrite / candidate |
| `symbol.search_library` | 在内置/用户符号库中查找符号定义 | ReadOnly |
| `symbol.apply_candidate` | 为对象或注释创建符号实例候选并应用 | ReversibleWrite / apply |

样式不得改变对象语义。颜色、线型和符号表现不能自动转化为阵营、政治归属或其他语义属性。对象名称与标签文本分离；删除符号实例不删除其关联 Map Object。

### 5.5 布局与地图导出

| 工具 | 用途 | 风险/阶段 |
|---|---|---|
| `layout.list` | 查询当前地图的 Layout 列表 | ReadOnly |
| `layout.get_details` | 查询页面尺寸、Map Frame、页面元素和数据引用 | ReadOnly |
| `layout.create_candidate` | 创建 Layout、Map Frame 和页面元素候选 | ReversibleWrite / candidate |
| `layout.update_candidate` | 调整标题、图例、比例尺、指北针、网格和页面元素 | ReversibleWrite / candidate |
| `layout.preview` | 生成页面预览并返回质量问题 | ReadOnly / preview |
| `map.export_preview` | 预览导出范围、格式、尺寸和潜在警告 | ReadOnly / preview |
| `map.export` | 通过本机导出服务生成用户指定格式文件 | ReversibleWrite / apply |

导出只能写入用户选择或本机应用管理的受控目标位置；不接受模型提供任意路径。支持格式和 CRS 转换必须由导出服务实际能力决定。虚构地图的逻辑坐标不得伪装为真实经纬度 GeoJSON。导出失败不得损坏地图正式数据。

### 5.6 质量检查与正式应用

| 工具 | 用途 | 风险/阶段 |
|---|---|---|
| `validation.run_map_checks` | 执行几何、空间、语义、制图和任务约束检查 | ReadOnly |
| `validation.get_report` | 读取指定检查报告和问题定位 | ReadOnly |
| `changeset.create` | 将多个候选变更组织为一个待审变更集 | ReversibleWrite / candidate |
| `changeset.preview` | 预览变更集影响、对象增删改和检查结果 | ReadOnly / preview |
| `changeset.apply` | 正式应用通过校验且满足授权要求的变更集 | 按变更风险 / apply |
| `changeset.discard` | 丢弃未应用的候选变更集 | ReversibleWrite / candidate |
| `history.get_recent_changes` | 查询当前 Branch 最近正式变化摘要 | ReadOnly |
| `history.get_checkpoint_status` | 查询适用 Checkpoint 和重建状态摘要 | ReadOnly |

`changeset.apply` 不得成为绕过领域服务的批量写入口。它只能提交已明确列出的领域操作，由各 Domain Services 再次验证。只有事务提交成功后才返回 applied，并返回正式历史引用及新地图修订号。

## 6. V1 推荐最小可用子集

为避免一次性实现过多工具，建议按以下顺序形成端到端闭环：

1. **读取上下文**：`map.get_current_context`、`layer.list`、`object.search`、`object.get_details`。
2. **创建候选**：`object.create_candidate`、`geometry.create_candidate`、`object.update_candidate`。
3. **预览与验证**：`geometry.validate`、`validation.run_map_checks`、候选预览。
4. **用户决策与应用**：`changeset.create`、`changeset.preview`、`changeset.apply`、`changeset.discard`。
5. **结果解释**：`history.get_recent_changes`，并向 Assistant 返回应用结果、警告和新修订号。
6. **逐步扩展**：图层管理、样式标签符号、布尔运算、共享边界、Layout 和导出。

首个可交付版本不必覆盖全部地图编辑器功能，但必须打通“理解任务—读取上下文—创建候选—验证/预览—按风险确认—正式应用—记录历史—解释结果”的完整闭环。

## 7. 风险与确认策略

### ReadOnly

可在当前会话授权范围内自动执行。仍须检查地图上下文、读取范围和对象状态；历史对象可按历史查询规则读取，不能将已撤回或非正式候选当作当前正式认知。

### ReversibleWrite

仅允许写入临时候选、预览缓存或可丢弃工作区。通常不需要逐项人工确认，但必须能明确丢弃，且不得污染正式地图状态和正式历史。

### HighImpactWrite

例如删除大量对象、批量改变关键语义关系、修改共享边界、应用大范围几何变更、覆盖重要布局或执行可能造成不可恢复外部副作用的操作。必须先展示简明影响摘要并取得用户确认。确认绑定具体操作摘要、目标对象范围和地图基准修订号；若范围或基准发生变化，应重新确认。

风险等级由工具注册表和服务端策略决定，模型不得自行降低风险等级。用户确认一次具体操作不自动授权未来相似操作。

## 8. 状态、授权与并发校验

每次写操作至少验证：

- 地图和 Branch 仍是任务目标；
- 目标对象/图层存在且属于当前地图；
- 对象状态允许该类读取或修改；
- 目标图层未锁定且操作符合图层约束；
- 几何类型、空间参考、拓扑及共享引用约束成立；
- 候选仍有效且来源于当前任务；
- 用户授权覆盖操作类型和目标范围；
- 预期地图修订号仍匹配。

若地图基准已变化，返回 `map_state_stale`，携带当前修订号和需要重新预览的提示；不得静默重放旧写入。对于批量操作，应在事务中确保原子性，或明确返回逐项结果与部分完成状态，不得把部分成功伪装成全部成功。

## 9. 错误结果

统一错误结构建议包含：`code`、`message_for_assistant`、`retryable`、`affected_ids`、`current_revision`、`validation_report_ref` 和安全的 `details`。至少覆盖：

- `tool_not_available`、`tool_not_allowed`、`invalid_arguments`；
- `authorization_required`、`authorization_scope_mismatch`；
- `target_not_found`、`target_state_invalid`、`layer_locked`；
- `geometry_invalid`、`topology_conflict`、`semantic_constraint_violation`；
- `map_state_stale`、`candidate_expired`、`idempotency_conflict`；
- `validation_failed`、`operation_partially_applied`、`export_failed`。

错误不得泄露本机绝对路径、API Key、供应商密钥、数据库细节或不相关的本地文件信息。

## 10. 幂等、取消与事务

- 本机为每次写操作生成 operation_id 和幂等键；相同键重试返回原结果，不重复创建对象或历史记录。
- 正式应用在 Domain Services 事务内完成状态变更、修订号推进和正式历史记录；不能先返回成功再异步补写权威状态。
- 取消在正式提交前阻止后续新步骤；已提交的正式变更不因取消而自动回滚。
- 候选过期、任务取消或地图基准变化后，候选不得直接应用，需刷新或重建。
- 导出等外部文件副作用应与地图事务区分，返回明确文件结果；失败不回滚或损坏正式地图。

## 11. 工具发现与按任务暴露

Assistant 先根据任务意图和当前地图上下文选择工具子集，再交给 Model Gateway。工具暴露规则：

1. 任务只涉及查询时，不暴露写入工具。
2. 任务只涉及对象属性时，不暴露几何布尔或布局工具。
3. 候选未通过验证时，不暴露正式应用工具，或使其不可用并返回原因。
4. 当前模型不支持可靠工具调用时，不尝试用自由文本伪造工具调用；改用受控结构化输出或提示用户手工操作。
5. 工具描述应短而精确，避免相互重叠、暗示模型拥有额外权限。

## 12. 与其他架构文档的关系

- Provider Adapter Contract 定义模型消息和工具调用协议；本文定义地图领域工具目录。
- Map Generation Specification 定义用户任务意图和约束；本文工具执行这些意图所需的本机领域动作。
- Map Generation Workflow 定义任务规划、执行、验证和应用顺序；本文提供工作流可调用的能力。
- Generation Session Lifecycle 定义复杂任务后台生命周期；工具调用可被 Session 跟踪，但 Session 不取代地图正式历史。
- Map Object、Geometry、Layer、Style、Label、Symbol、Layout、Decoration 领域模型定义业务语义；本文不得重新定义或绕过这些领域模型。

## 13. 尚待工程验证

以下内容不在本文冻结：

- 最终工具命名、JSON Schema 方言、类型系统和序列化实现；
- 首批工具的精确字段、分页参数、payload 上限和批量数量上限；
- 几何数据传输采用坐标数组、结构化路径段还是本机候选引用；
- 工具调用轮数、并发数、超时和预算数值；
- 候选与 Change Set 的持久化/过期策略；
- 工具目录动态注册、版本兼容和迁移策略；
- 各操作风险等级的最终映射及用户授权界面；
- 各地图格式导出能力、CRS 支持和本机文件访问策略。

这些事项应通过原型、威胁建模和端到端测试确定，不应仅凭本文中的建议名称直接视为实现承诺。

## 14. 验收标准

1. 模型不能调用目录外工具或自行构造工具定义。
2. 只读工具不产生地图正式修订或正式历史记录。
3. 候选工具不会直接修改正式地图；候选可预览、丢弃和过期。
4. 正式写入只能经 Tool Gateway 和 Domain Services，且通过全部领域校验。
5. 高影响操作在取得绑定目标范围与地图基准的用户确认前不能提交。
6. 旧地图修订上的写请求返回 stale，不覆盖较新状态。
7. 幂等重试不会重复创建对象、变更或历史记录。
8. 几何拆分/合并遵守几何操作与对象操作分离原则。
9. 共享边界建立必须明确确认，修改影响可被识别和预览。
10. 工具结果明确区分候选创建、验证通过、正式应用成功和部分完成。
11. 取消不会自动回滚已提交正式变更，也不会执行迟到的未授权工具调用。
12. 无模型连接时，用户仍可正常手工编辑、保存、导入和导出地图。

---

**文档结论**：World View V1 应提供一组小而明确、受本机领域规则保护的地图工具，以完整打通 AI Assistant 的核心地图制作闭环；工具目录是内部实现能力，不是面向用户的第二套操作界面，也不是任意系统访问接口。