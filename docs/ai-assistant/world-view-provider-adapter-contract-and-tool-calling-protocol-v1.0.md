# World View — Provider Adapter Contract & Tool Calling Protocol

- 文档版本：V1.0
- 日期：2026-11-20
- 状态：技术设计推荐基线（具体语言类型、SDK 与首批供应商实现待工程验证）
- 适用范围：World View 单用户、单机桌面软件；用户自行配置 LLM 供应商
- 上位文档：`world-view-llm-provider-integration-architecture-v1.0.md`

## 1. 目的与边界

本文把 Provider Integration 架构细化为可实现的内部契约，定义统一消息、模型请求/响应、能力描述、工具调用、错误、取消与执行结果回传。

本文不定义某个供应商的完整 API，也不冻结具体编程语言、SDK 或序列化库。供应商协议由 Provider Adapter 负责映射；AI Assistant 和 Domain Services 只依赖 World View 内部契约。

必须保持以下边界：

1. World View 是单用户、单机应用；模型连接由用户自行配置。
2. 模型只负责理解、推理、规划和生成候选内容。
3. 模型输出的 Tool Call 是请求，不是已执行操作或授权。
4. 本机 Tool Gateway 负责白名单、参数、授权、状态与风险检查。
5. 本机 Domain Services 是正式地图数据的唯一写入入口和权威。
6. Provider Adapter 不得拥有地图领域权限，也不得直接访问地图数据库或项目文件。
7. 任何供应商协议细节不得泄漏到地图领域模型。

## 2. 分层与调用关系

```text
AI Assistant
  │  Internal ModelRequest / ModelResponse
  ▼
Model Gateway
  │  connection selection, capability gate, timeout, cancellation
  ▼
Provider Adapter
  │  provider-specific request / response
  ▼
User-configured LLM endpoint

AI Assistant ── ToolCallProposal ──> Tool Gateway
                                      │ validate + authorize
                                      ▼
                                Domain Services
                                      │ structured result
                                      ▼
                                Tool Gateway
                                      │ ToolResult
                                      ▼
                                  AI Assistant
```

职责：

- **AI Assistant**：维护任务上下文、决定本轮模型目标、处理模型响应、决定是否继续调用模型、向用户解释结果。
- **Model Gateway**：选择连接、检查能力门槛、执行超时/取消/有限重试、记录安全用量元数据、统一错误。
- **Provider Adapter**：协议转换、供应商认证、流式事件转换、供应商错误归一化。
- **Tool Gateway**：验证工具名、参数、调用轮次、授权上下文、地图基准状态和风险要求；调用领域服务。
- **Domain Services**：执行地图业务规则、几何与语义校验、事务写入及正式历史记录。

## 3. 统一内部消息模型

### 3.1 Message

建议统一消息至少包含：

| 字段 | 类型/含义 | 规则 |
|---|---|---|
| `message_id` | 本机生成的稳定 ID | 单次会话内唯一，避免复用供应商 ID |
| `role` | system / developer / user / assistant / tool | 由内部协议定义，不接受模型自行声明高权限角色 |
| `content` | 文本或受控多模态内容块 | 不允许任意文件路径或任意 URL 引用 |
| `tool_calls` | Assistant 发出的工具调用提议 | 仅 assistant 消息可携带 |
| `tool_call_id` | 对应工具调用 ID | tool 结果必须引用对应 ID |
| `metadata` | 本机内部元数据 | 默认不序列化给供应商 |

角色映射由 Adapter 负责。供应商不支持某种角色时，Adapter 采用经测试的映射策略；不得把用户内容拼接成 system/developer 指令来伪造角色权限。

### 3.2 Content Parts

V1 建议支持：

- `text`：普通文本。
- `image`：由 Context Builder 提供的受控图片数据或受控引用；发送前经过用户数据范围策略。
- `untrusted_text`：外部资料、OCR、导入文本等不可信内容，明确标注来源与不可信性质。

音频、视频、任意二进制附件不作为 V1 必备内容类型。若某供应商支持额外模态，应通过能力扩展加入，不得破坏基础消息契约。

## 4. ModelRequest

建议统一请求结构：

```text
ModelRequest {
  request_id: RequestId
  conversation_id: ConversationId
  connection_id: ConnectionId
  model_id: String
  messages: Message[]
  tool_definitions: ToolDefinition[]
  response_mode: Text | Structured(schema_id)
  stream: Boolean
  generation: GenerationOptions
  deadline: Timestamp
  cancellation_token: CancellationToken
  context_manifest: ContextManifestRef
}
```

约束：

- `connection_id` 和 `model_id` 必须来自本机已保存的用户配置，不能由模型响应覆盖。
- `tool_definitions` 仅包含本轮任务所需且已获准暴露的工具。
- `response_mode` 只要求 Adapter 已验证支持的格式；不支持时由 Gateway 拒绝或选择明确降级。
- `generation` 只包含当前能力快照确认支持的参数。
- `deadline` 和取消令牌由本机控制；供应商不支持真正取消时，仍必须停止本机后续处理并标记远端状态未知。
- `context_manifest` 是本机审计引用，不代表把完整上下文清单发送给模型。

## 5. ModelResponse 与流式事件

### 5.1 ModelResponse

```text
ModelResponse {
  request_id: RequestId
  assistant_message: Message
  finish_reason: Stop | ToolCalls | Length | ContentFilter | Unknown
  usage: Usage? 
  provider_request_id: String?
  actual_model_id: String?
  adapter_version: String
  capability_snapshot_id: String
}
```

`usage` 为可选信息，只有供应商明确提供时记录；不得推算并伪装成供应商账单数据。`actual_model_id` 用于记录供应商实际返回的模型标识，不得反向更改用户配置。

### 5.2 StreamEvent

统一流式事件建议包括：

- `started`
- `text_delta`
- `tool_call_delta`
- `usage_update`（可选）
- `completed`
- `failed`

Adapter 必须处理供应商事件顺序、分片拼接和结束标志。只有完整解析并通过校验的工具调用才能交给 Tool Gateway；不完整的流式片段不得执行。

## 6. Provider Adapter 契约

建议定义以下逻辑接口（伪代码，不绑定具体语言）：

```text
interface ProviderAdapter {
  adapter_id() -> AdapterId
  adapter_version() -> Version
  validate_config(config) -> ConfigValidationResult
  test_connection(config, credential_ref, deadline) -> ConnectionTestResult
  discover_models?(config, credential_ref, deadline) -> ModelListResult
  probe_capabilities(config, model_id, credential_ref, deadline) -> CapabilitySnapshot
  send(request, config, credential_ref) -> Result<ModelResponse, ProviderError>
  stream(request, config, credential_ref) -> AsyncStream<StreamEvent>
  cancel?(request_id) -> CancelResult
  normalize_error(raw_error) -> ProviderError
}
```

接口要求：

1. Adapter 接收凭据引用，由安全凭据服务在请求时提供秘密；不得把密钥放入普通请求对象或错误对象。
2. `test_connection` 不得携带地图上下文、工具定义或触发工具执行。
3. `discover_models` 是可选能力；不支持时允许用户手动输入 Model ID。
4. `probe_capabilities` 必须区分声明、探测与运行时证据。
5. `send` 与 `stream` 使用同一内部语义，避免两套行为不一致。
6. Adapter 不得重试非幂等 Domain Service 操作；其重试仅限模型 HTTP 请求层。
7. Adapter 必须限制响应体大小、连接/读取超时，并对取消给出明确语义。
8. Adapter 返回的原始错误不得直接展示给用户，必须先归一化并清理敏感内容。

## 7. CapabilitySnapshot

能力以连接、端点、模型和 Adapter 版本的组合为粒度：

```text
CapabilitySnapshot {
  snapshot_id: Id
  connection_id: ConnectionId
  endpoint_fingerprint: String
  model_id: String
  adapter_version: Version
  checked_at: Timestamp
  expires_at: Timestamp?
  capabilities: {
    text_chat: Supported | Unsupported | Unknown
    tool_calling: Supported | Unsupported | Unknown
    structured_output: Supported | Unsupported | Unknown
    streaming: Supported | Unsupported | Unknown
    vision: Supported | Unsupported | Unknown
    cancellation: Supported | BestEffort | Unsupported | Unknown
    context_window: Integer?
    max_output_tokens: Integer?
  }
  evidence: CapabilityEvidence[]
}
```

能力值不得只有 Boolean：`Unknown` 与 `Unsupported` 必须区分。`BestEffort` 表示本机可停止等待，但不能保证供应商停止远端执行。

执行前能力门槛：

- 纯文本问答要求 `text_chat=Supported`。
- AI 自主调用本机工具要求 `tool_calling=Supported`；否则不能把模型作为可自主执行模型。
- 强约束结构化输出任务要求 `structured_output=Supported`，或使用经过验证的严格解析降级流程。
- 图片理解任务要求 `vision=Supported`。
- 流式响应不是正确性前提；不支持时可以退回非流式。
- 未知能力不得默认为支持。

## 8. Tool Definition 与工具注册

### 8.1 ToolDefinition

```text
ToolDefinition {
  tool_name: String
  description: String
  input_schema: JsonSchema
  risk_class: ReadOnly | ReversibleWrite | HighImpactWrite
  availability: Enabled | Disabled
}
```

工具注册表由 World View 本机代码维护。模型不能创建、修改或提升工具定义。每轮仅暴露当前任务所需工具，遵循最小权限原则。

### 8.2 工具粒度

工具应表达用户可理解的领域动作，而不是数据库或底层文件操作。例如：

- `map.get_selected_objects`
- `map.search_objects`
- `map.create_candidate_objects`
- `map.preview_geometry_operation`
- `map.apply_validated_change_set`
- `map.get_layer_summary`

以上为命名示例，不代表 V1 工具清单已冻结。不得提供 `execute_sql`、`run_shell_command`、`write_file`、任意 HTTP 请求或任意路径读写等通用高权限工具。

工具应保持单一、明确的业务目的；高风险操作不能通过宽泛的 `map.update_anything` 隐藏授权边界。

## 9. ToolCallProposal 与 ToolResult

### 9.1 ToolCallProposal

```text
ToolCallProposal {
  tool_call_id: ToolCallId
  tool_name: String
  arguments: JSON
  originating_request_id: RequestId
}
```

模型提供的参数一律视为不可信输入。不得信任模型附带的用户身份、授权声明、风险判断、对象当前版本或执行成功描述。

### 9.2 ToolResult

```text
ToolResult {
  tool_call_id: ToolCallId
  status: Succeeded | Rejected | NeedsUserDecision | Failed | Stale
  result: JSON?
  error: NormalizedToolError?
  affected_object_ids: ObjectId[]
  applied_change_id: ChangeId?
  resulting_map_revision: MapRevision?
}
```

只有 Domain Services 确认事务提交后，工具结果才可标记 `Succeeded` 并返回 `applied_change_id`。模型声称成功、生成了 JSON 或输出了地图描述，都不能视为正式应用。

## 10. Tool Calling Loop

标准调用循环：

1. AI Assistant 根据用户任务和本机授权状态准备上下文。
2. Gateway 检查模型能力，并向模型提供本轮白名单工具定义。
3. 模型返回文本或一个/多个 ToolCallProposal。
4. Gateway 校验调用 ID、工具名、Schema、参数大小、调用次数和并发限制。
5. Tool Gateway 重新读取相关地图状态，校验对象存在性、版本和授权。
6. 根据工具风险等级决定直接执行、需要用户决策或拒绝。
7. Domain Services 执行确定性操作；写操作使用事务和幂等键。
8. ToolResult 返回 AI Assistant；只有任务确实需要后续推理时，才将必要结果回传模型。
9. AI Assistant 汇总真实执行结果，并区分已应用、未应用、部分完成和建议内容。

多工具调用处理：

- 默认串行执行可能相互影响的写操作。
- 只有明确声明无依赖且均为只读的调用才可并行。
- 多个写调用不能因为模型在同一响应中并列返回就自动组成一个原子事务。
- 需要原子性的一组操作应由一个明确的领域服务用例处理。
- 超出每任务工具调用轮数或总调用数上限时暂停任务，不得无限循环。

## 11. 授权、风险与用户确认

工具调用不能替代用户授权。授权上下文由本机 AI Assistant / Tool Gateway 生成，模型不能自行扩大。

建议风险分类：

| 风险类别 | 示例 | 默认处理 |
|---|---|---|
| ReadOnly | 查询对象、读取图层摘要 | 在当前任务范围内可直接执行 |
| ReversibleWrite | 创建候选、修改可撤销的普通地图内容 | 依据用户当前授权和影响范围执行；必要时预览 |
| HighImpactWrite | 大范围删除、改变重要正式设定、影响共享边界或大量对象 | 暂停并请求用户决策，或要求先生成预览/影响分析 |

具体风险等级由 World View 领域规则定义，不能由模型自行指定。用户确认必须绑定明确的操作摘要、目标对象/范围和地图基准版本；确认后若关键状态变化，原确认失效。

## 12. 参数与结果校验

Tool Gateway 至少执行：

- JSON Schema 校验；
- 字段白名单、字符串/数组/嵌套深度和 payload 大小限制；
- 数值范围、坐标范围与几何类型检查；
- 对象 ID、图层 ID、地图 ID 的本机存在性和归属检查；
- 当前地图状态、对象版本和共享拓扑影响检查；
- 用户授权与风险等级检查；
- 领域服务二次校验。

模型生成的对象名称、历史事实、政治归属、来源声明等内容必须保留其来源/不确定性标记；不能因为参数 Schema 合法就视为事实正确。

## 13. 幂等、并发与地图基准状态

每个可能产生正式写入的工具调用应带本机生成的 `operation_id` / idempotency key。相同任务重试时，Domain Services 能识别已完成操作，避免重复创建对象或重复追加历史事件。

写入请求应携带预期地图修订号或相关对象版本：

- 版本匹配时继续执行；
- 版本不匹配时返回 `Stale`，重新读取状态并重新规划；
- 不允许模型用旧上下文覆盖新地图状态；
- 事务失败时不得返回成功或部分伪造的正式结果。

模型请求重试、工具调用重试和 Domain Services 事务重试是不同层次，必须分别控制。

## 14. 错误契约

建议内部统一错误类别：

- `configuration_invalid`
- `credential_missing`
- `authentication_failed`
- `connection_failed`
- `rate_limited`
- `quota_exceeded`
- `model_unavailable`
- `capability_unsupported`
- `context_too_large`
- `request_timed_out`
- `request_cancelled`
- `provider_response_invalid`
- `tool_not_allowed`
- `tool_arguments_invalid`
- `authorization_required`
- `authorization_denied`
- `domain_validation_failed`
- `map_state_stale`
- `operation_conflict`
- `operation_failed`
- `internal_error`

错误对象应包含稳定类别、是否可重试、用户可读摘要、可选安全诊断 ID。不得包含 API Key、完整 Authorization Header、未经清理的提示词、完整地图上下文或原始敏感响应。

## 15. 取消与超时

- 用户取消后，本机立即停止发起新的模型请求和工具调用。
- 正在执行的只读工具尽可能取消；正在提交的事务由 Domain Services 按事务边界完成或回滚，不允许处于不明状态。
- 正式写入一旦提交，取消任务不自动撤销该写入。
- Adapter 支持远端取消时尽力发送取消；不支持或无法确认时标记远端状态未知。
- 取消后迟到的模型响应不得继续触发工具调用。
- 所有工具调用都必须检查任务取消状态和授权上下文有效性。

## 16. 日志与可观测性

默认可记录：request ID、connection ID、模型 ID、Adapter 版本、能力快照 ID、耗时、usage（如供应商提供）、错误类别、工具名、工具状态、地图修订引用。

默认不得记录：API Key、完整提示词、完整地图上下文、未经用户同意的参考文件内容、完整敏感工具参数。

对工具写入应记录本机审计信息：触发任务、工具名、执行结果、影响对象、应用的地图修订和必要的用户决策引用。日志与正式地图历史是不同用途，不能互相替代。

## 17. V1 实施顺序

推荐按以下顺序实施：

1. 定义内部消息、ModelRequest、ModelResponse、错误和 CapabilitySnapshot 类型。
2. 实现 Mock Provider Adapter 和契约测试。
3. 实现 Model Gateway 的连接选择、超时、取消、错误归一化和能力门槛。
4. 实现只读工具注册与 Tool Gateway 校验。
5. 接入一个经验证的真实供应商 Adapter，跑通文本与工具调用闭环。
6. 增加结构化输出、流式响应和能力探测。
7. 接入 Domain Services 的候选创建、预览和受控应用用例。
8. 增加幂等、并发失效、取消迟到响应、安全日志和网络故障测试。

先以 Mock Provider 验证内部契约，避免领域层在开发早期被某个供应商 SDK 绑架。

## 18. 验收标准

1. 更换 Provider Adapter 不需要修改 AI Assistant 领域逻辑。
2. 未知能力不会被当作支持；无工具调用能力的模型不能进入自主工具执行路径。
3. 模型不能创建工具、修改工具 Schema 或改变工具权限。
4. 非白名单工具、非法参数、越权调用和过期地图状态均被本机拒绝。
5. Tool Call 只有经 Domain Services 成功提交后才能报告成功。
6. 多个互相依赖的写调用不会因同批返回而被盲目并行执行。
7. 重试不会重复创建对象或重复写入历史事件。
8. 用户取消后迟到的模型响应不会继续执行工具。
9. 供应商错误不会泄露凭据或敏感上下文。
10. 流式工具调用必须在完整解析和校验后才可执行。
11. 地图修订变化会使旧写入请求或候选失效，不覆盖新状态。
12. 无模型连接时，地图非 AI 功能保持可用。

## 19. 尚待工程验证

以下事项不在本文件中提前冻结：

- 最终编程语言、异步运行时和具体类型系统；
- JSON Schema 方言及验证库；
- 首批供应商的原生协议和工具调用差异；
- 统一多模态内容块的具体编码方式；
- 工具调用最大轮数、单轮最大调用数和 payload 上限的具体数值；
- 连接能力快照有效期和探测成本；
- 供应商流式事件与取消语义的逐家兼容矩阵；
- 本机审计日志的保留期限与导出格式。

这些参数应通过原型、集成测试和安全测试确定，不能改变模型不拥有地图数据权威、本机 Tool Gateway 控权、本机 Domain Services 正式执行的基本架构。

## 20. 与相关文档的关系

- `world-view-ai-assistant-single-user-local-first-architecture-v1.1.md`：产品形态、用户自带 LLM、本机数据权威和隐私约束。
- `world-view-ai-assistant-product-architecture-v1.0.md`：AI Assistant、内部工具与 Domain Services 职责边界。
- `world-view-ai-assistant-user-experience-v1.0.md`：模型配置、用户交互、预览和异常恢复。
- `world-view-llm-provider-integration-architecture-v1.0.md`：Provider Adapter 总体接入、安全和隐私架构。
- `02-map-generation-specification-v0.2.md`：复杂地图任务意图和约束。
- `03-map-generation-workflow-v0.2.md`：地图任务规划、执行、验证和应用流程。
- `04-generation-session-lifecycle-v0.2.md`：复杂任务生命周期、候选隔离和状态恢复。