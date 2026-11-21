# World View — P2-01 Domain Model 领域语义审计与推荐基线

- **项目**：World View
- **阶段**：Phase 2 / P2-01
- **文档类型**：领域语义审计工作稿
- **版本**：V0.1
- **日期**：2026-11-19
- **状态**：Proposed / Review Working Draft
- **用途**：作为重构《P2-01 Domain Model Proposal V0.2》的直接输入
- **权威边界**：本文件是审计与推荐，不是新 ADR，不修改既有 Accepted / Frozen ADR，也不构成 Phase 2 冻结或开发授权。

---

## 0. 执行摘要

本轮审计的结论是：**P2-01 V0.1 不应继续通过局部补丁演进，应按当前已确认裁决重构为 V0.2。**

旧 V0.1 的主要问题不是所有概念都错，而是它把早期的 State Change 驱动模型写成了领域权威模型；后续 D01 已将权威中心明确调整为 Command。与此同时，ADR-002、ADR-003 又补充了统一 History、Shared Object History 与 Map-local History 的播放语义。若只修正几个字段，会继续保留旧模型的结构性偏差。

本次审计形成以下推荐基线：

1. **Command 是作者创作历史的权威记录。** 用户修改 Command 是直接修正原记录，不建立 Command Revision 链。
2. **History 是历史归属与继承关系的统一领域结构。** Map 根历史与 Branch 自有历史使用同一 History 概念；共享对象历史也应使用同一历史容器抽象，但其归属主体是 Shared Object，而不是 Map。
3. **Event 是叙事表达；Command 是作者指定的历史操作；State Change 是 Command 执行所表达或产生的派生状态变化。** 三者不可合并为同一实体，也不能要求一一对应。
4. **World State 是按指定历史时间执行适用历史操作后得到的状态。** 它是重建结果，不是第二套作者历史。
5. **Branch 是动态继承，不是静态快照。** 子 Branch 继承祖先分叉点及之前（≤分叉点）的有效历史结果，不复制祖先 Command；自有 Command 必须严格晚于分叉点（> parent_at）。
6. **Shared Object 身份共享不等于运行时状态共享。** 共享历史影响所有引用地图；地图/Branch 的局部 Command 只影响当前地图/Branch。
7. **单次播放选择一个 Map 或 Branch。** 对该播放目标，将适用的共享历史命令与当前地图局部命令组织成一条确定执行序列，得到唯一当前状态；不并行播放多个 Branch。
8. **历史时间是内部权威坐标，Calendar 是显示/解释规则。** 日历改革不改变 Command、Event 或 State Change 的内部历史时间。
9. **对象存在性、Command 是否执行、历史质量警告是三个独立维度。** 对已删除对象的后续局部修改允许执行、不自动复活对象，并产生持续警告。
10. **Checkpoint、缓存、State Change、依赖发现、警告均不得成为第二套权威创作历史。** Checkpoint 保存完整合成 World State，仅用于恢复加速。

### 审计范围

已核对资料：
- `phase2-p2-01-domain-model-proposal-v0.1.md`
- `architecture-baseline-v1.0.md`
- `ADR-001-map-history-and-branch-model.md`
- `ADR-002-unified-history-entity.md`
- `ADR-003-shared-local-history-command-playback.md`（V1.2）
- `ADR-005-branch-fork-rules.md`
- D01 拆分记录：第 2、3、4、5、7、8、9、12、15、16、17、18 章及 README 索引
- `ADR-004-batch-repair-undo-redo.md` 用于确认 Undo/Redo 不应扩张为通用领域历史机制。

未在本轮完成 Storage Architecture 原始文档审计；其技术存储问题不纳入本文件的领域语义裁决。

---

## 1. 领域权威与派生结果

### 1.1 审计发现

P2-01 V0.1 的总纲将历史描述为“按 Historical Time 排序的 State Change”，并在 §4 把 State Change 定义为历史最小变更单元及唯一合法状态改变机制。D01-01~04 后续明确：

- Command 是用户创作行为的权威历史记录；
- Command 保存操作本身；
- State Change 的语义可由 Command 类型解释，也可以由一条 Command 表达多个状态变化；
- 一条 Command 可以不产生状态变化、产生一个状态变化或产生多个阶段性状态变化；
- State Change 不自动成为第二套独立的用户创作历史；
- 持续 Command 可产生多个阶段性 State Change，这些是可重建派生结果。

因此，V0.1 的“State Change 是历史最小变更单元/唯一合法状态改变机制”不能继续作为当前模型总纲。

### 1.2 推荐基线

| 概念 | 领域定位 | 权威性 |
|---|---|---|
| Command | 作者创作意图及操作的权威记录 | 权威 |
| Event | 对发生事项的叙事记录 | 作者创作内容；不直接改变状态 |
| State Change | Command 执行所表达/产生的状态变化语义与派生结果 | 派生 |
| World State | 指定历史时间点的合成世界状态 | 派生 |
| Checkpoint | 完整 World State 的可恢复缓存 | 派生缓存 |
| Dependency / 隐式依赖 | 执行顺序与关系的可重建信息；显式约束除外 | 派生或独立约束数据，依具体类别区分 |
| Warning / Error | 对历史质量或执行问题的诊断记录 | 诊断信息，不是创作历史 |

Command 修改采用原记录直接修改，不建立 Revision Chain（D01-08）。诊断记录可以保留旧警告及其解决原因，但这不等于保留 Command 内容版本。

### 1.3 V0.2 必须修改

- 移除“State Change 是唯一合法状态改变机制”的权威表述。
- 将“重建输入”改为作者权威 Command 集合、Initial State、适用历史关系及确定执行规则。
- State Change 作为执行结果/可解释派生层，不再作为作者输入历史的默认主体。
- 保留 State Change 可被查询、解释、用于重建验证或阶段性展示的可能性；其物理持久化策略留给 Engine / Storage 设计。
- 不得把 D01 的 Command 直接等同于 ADR-003 所说的执行序列中的某个数据库记录结构；数据结构未裁决。

**依据：** D01-01~04、D01-08、D01-12；ADR-003 A10-03；D01-143。

---

## 2. History、Map、Branch 与 Shared Object 的归属

### 2.1 已确认结构

ADR-002 已冻结：
- Map 拥有根 History。
- Branch 拥有自己的 History。
- Branch 通过 `parent History + parent_at` 引用父历史，不复制祖先历史。
- Event 与 State Change 都具有 History 归属。
- 统一 History 不意味着 Merge。

ADR-003 又明确 Shared Object（例如 Shared POI）拥有自己的历史；Map-local 修改属于当前地图局部历史。

D01-70~76 进一步明确：
- Shared Object 共享身份与基础数据，不共享所有地图/Branch 的运行时修改结果。
- 对 Shared Object 的局部修改属于当前 Branch 自己的 Command。
- Branch 重建先取得继承状态，再执行当前 Branch 自己的 Command。
- Branch 动态继承父历史截至分叉点的有效结果。
- Shared Object 的局部变化与 Branch Checkpoint 中的合成状态必须正确区分。

### 2.2 推荐的统一抽象

V0.2 应把 History 定义为一种**历史容器/历史归属抽象**，而不是“仅 Map 或 Branch 才能拥有的容器”。

建议支持以下历史主体：

1. Map Root History；
2. Branch Own History；
3. Shared Object History。

这三者共享统一的历史语义与重建规则，但历史归属主体不同。不要把 Shared Object History 误写成每张 Map 的一份复制历史，也不要把 Map-local History 写成 Shared Object 的属性覆盖层。

推荐概念结构：

```text
Map
 ├── Root History
 ├── Object References
 └── Branches
      └── Branch Own History
           └── Parent History + parent_at

Shared Object
 └── Shared Object History

Playback Target (one selected Map or Branch)
 └── Applicable Command Streams
      ├── inherited Map / Branch history
      ├── applicable Shared Object History
      └── selected Map / Branch local Commands
```

注意：上图表达领域归属和播放输入，不代表已经裁定数据库表结构或所有 History 必须采用相同的物理存储方式。

### 2.3 Shared / Local 播放原则

ADR-003 A10-01~04 已确认：
- 当前播放目标适用的共享命令与局部命令，最终组织为一条连续执行序列；
- App 负责默认排序；
- 用户可调整顺序；
- 用户明确调整的顺序必须保存为正式数据；
- 新命令加入时不得擅自覆盖用户指定顺序。

D01-17~28 进一步确认：
- 优先按依赖关系确定顺序；
- 无法确定时才用固定规则处理不改变语义的技术性排序；
- 若顺序歧义会改变世界状态，必须等待用户决定；
- 用户指定顺序保存为 Command 之间的显式顺序约束；
- 显式顺序约束不能被 Branch 覆盖；
- Branch 可增加自己的约束，但父历史约束冲突时保留全部约束并报告问题。

### 2.4 必须保留的未决边界

ADR-003 明确没有裁定：
- 完整排序算法；
- 所有同一历史时间的排序规则；
- 复杂依赖算法；
- 新命令与人工顺序的完整冲突处理；
- 警告分类、生命周期与 UI；
- Transaction Time 与 Valid Time 的完整关系。

因此 V0.2 可以定义领域概念与不变量，但不能把某一种拓扑排序算法、隐式依赖算法或警告状态机写成已冻结规则。

### 2.5 V0.2 的推荐表达

“History 归属”与“Playback Command Stream”应分开描述：

- History 说明创作记录属于谁、如何继承；
- Playback Command Stream 说明对某一个已选定 Map/Branch，哪些共享与局部 Command 适用，以及如何形成单一执行序列；
- 两者不是同一层级概念。
- 形成一条执行序列不等于把多条历史合并成一条新历史，不构成 Merge。

**依据：** ADR-002 §2~5；ADR-003 A10-01~09；D01-17~28、D01-70~76。

---

## 3. Event、Command、State Change 的关系

### 3.1 已确认语义

- Event 回答“发生了什么”，是叙事表达。
- Command 表达作者希望世界执行的历史操作，是权威创作记录。
- State Change 表达 Command 执行所产生/表达的世界状态变化。
- Event 与 State Change 可以关联，但不是一一对应。
- State Change 可以独立于 Event 存在。
- 一条 Command 可以关联一个 Event，也可以没有 Event；Command 与 Event 的完整关系仍需保持可扩展，不能仅凭旧模型固定一对一。
- Event 本身不直接改变 World State。

### 3.2 推荐模型边界

V0.2 不应把 Event 设为 Command 的父对象，也不应要求每条 Command 都必须有 Event。

建议把关系表达为：
- Event 是叙事对象；
- Command 是可执行的作者历史操作；
- State Change 是 Command 执行派生；
- Event 与 Command / State Change 可以建立可选关联；
- 具体关联基数、引用方向、删除策略不在本轮强行冻结。

ADR-002 的“Event 与 State Change 归属于 History”应保留其历史归属语义，但不能据此推导 State Change 是权威历史输入。V0.2 应明确：**派生 State Change 可以标记其来源 History / Command，以支持追溯；其归属不改变其派生属性。**

### 3.3 V0.1 需要废止或改写

V0.1 的 `StateChange` 字段模型（`change_id / history_id / target / effective_time / sequence / change_type / payload / event_id`）可以作为早期设计参考，但不能直接照搬为权威历史实体定义。特别是：
- `history_id` 不能被解释为作者直接编辑的 State Change 所属权威记录；
- `sequence` 不能仅靠同一时间桶内的整数编号替代 D01 的显式 Order Constraint；
- `event_id` 只能是可选关联，不能建立一对多或一对一的先验限制；
- `change_type.apply` 作为纯函数可以是 Engine 的一种实现约束，但不是由 D01 自动裁定的全部领域模型。

**依据：** ADR-001 §5~10；ADR-002 §3；ADR-003 §3；D01-01~04、D01-17~28。

---

## 4. Branch 动态继承与时间边界

### 4.1 已冻结规则

ADR-001、ADR-002、ADR-005 与 D01-71 共同形成以下规则：

- Branch 可从任意历史时间点创建。
- Branch 有自己的 History。
- Branch 不复制祖先 Command。
- Branch 动态继承父历史在分叉点及之前的有效历史结果。
- 父历史在分叉点及之前（≤ parent_at）的后续新增或修改，会影响后代 Branch。
- 父历史在分叉点之后（> parent_at）的新增或修改，不影响该 Branch。
- Branch 自有 Command / State Change 的有效时间必须严格晚于分叉点：`effective_time > parent_at`。
- Branch 自有历史不反向影响父历史。
- 多层 Branch 按继承链逐层重建。

### 4.2 必须明确区分的两个时间规则

不能把下列规则混成一个“分叉点之前”：

| 语义 | 边界 |
|---|---|
| 父历史被 Branch 继承的范围 | `t ≤ parent_at`，闭区间 |
| Branch 自有 Command / State Change 的写入时间 | `effective_time > parent_at`，严格晚于 |

这意味着父历史在分叉点当年的 Command 属于继承范围；Branch 自己不能在分叉点当年写入自有 Command。

### 4.3 推荐的重建概念

```text
reconstruct(Branch B, T):
  if T <= B.parent_at:
      return reconstruct(B.parent_history, T)

  state = reconstruct(B.parent_history, B.parent_at)
  apply B's own applicable Commands after B.parent_at through T
  return state
```

对 Branch-of-Branch 逐层执行同一规则。祖先历史修改导致后代自有 Command 失败时：
- 保留后代 Command；
- 重建 Fail-Fast 停止；
- 报告原因；
- 不自动改写 Command、不自动寻找替代路线。

### 4.4 Checkpoint 边界

Checkpoint 按 Branch 归属，保存该 Branch 指定历史时间点的**完整合成 World State**。后代 Branch 可以复用祖先分叉点或适用历史位置的有效 Checkpoint，但必须校验完整继承路径版本；恢复后继续执行当前 Branch 自有 Command。Checkpoint 不是 Command 或 State Change 的替代物。

### 4.5 V0.1 需要修正

V0.1 的递归重建框架仍可保留，但：
- 不应使用 `State Change` 作为唯一的权威重放输入；
- `parent_history` 与 `parent_at` 应与 D01 的闭区间继承语义并列写清；
- 分支自有历史写入时间严格晚于分叉点，不能将 `t ≤ parent_at` 继承范围误写为 Branch 可写范围；
- Checkpoint 已不再只是一般 memoization；它有明确归属、完整状态内容、继承路径版本校验与原子发布规则。

**依据：** ADR-001 §11~19；ADR-002 §2~4；ADR-005 §1~7；D01-71、D01-76~77、D01-104~106、D01-143。

---

## 5. Shared Object 与局部修改

### 5.1 两种不同操作必须分开

**共享历史修改：**
- 修改属于 Shared Object History；
- 影响所有引用该 Shared Object 的地图；
- 对共享对象的基础身份与共享历史产生共同影响。

**地图/Branch 局部修改：**
- 修改属于当前 Map 或 Branch 的局部 Command；
- 只在当前 Map / Branch 的历史重建中执行；
- 不修改 Shared History；
- 不直接改变其他 Map / Branch 的历史结果；
- 不得简化为一份静态“局部属性覆盖值”。

D01-70 特别澄清：即使对象身份是 Shared Object，某个 Branch 对它做的局部修改仍属于当前 Branch 自己的 Command，不等于共享对象运行时状态被全局改写。

### 5.2 单线播放与合成状态

ADR-003 要求当前选定 Map / Branch 的适用共享命令与局部命令形成一条连续执行序列，播放时只有一个当前状态。共享与局部效果之间不采用固定优先级或多结果合成；效果由确定执行顺序自然产生。异常关系产生警告，但不自动改写命令，也不因警告阻断播放。

### 5.3 对象删除后的修改

已确认：
- 已删除对象的后续属性修改 Command 保留并允许进入执行序列；
- 不得自动复活对象；
- 对象是否存在由明确的创建、删除等生命周期变化决定；
- 必须产生可追踪、持续的历史质量警告；
- 排序、执行或手动调整顺序均不能自动清除警告；
- 只有原始异常条件确实不再成立时才能重新评估；
- 上述规则同样适用于 Branch 动态继承与重新重建。

### 5.4 必须保留的实现未决项

ADR-003 明确把以下内容留给后续技术设计：
- 已删除对象属性修改的内部重建算法；
- 已删除对象属性数据的保存与呈现；
- 对象删除、恢复、重新创建的完整状态机；
- 尚未创建对象的引用行为。

因此，V0.2 应定义“不得隐式复活”的领域不变量，但不得擅自设计 tombstone 字段、`object.restore` 命令或具体数据结构并声称已经裁决。

**依据：** ADR-001 §21~23；ADR-003 A10-01~09；D01-70、D01-73、D01-76。

---

## 6. Historical Time 与 Calendar

### 6.1 当前有效规则

D01-37~49 已将时间模型从旧 V0.1 的 `(calendar_id, ordinal)` 设计方向修正为：
- 每张 Map 独立定义第 0 天；
- 内部时间采用累计天数；
- 精度最高到秒；
- 推荐使用整数天数 + 当天内整数秒偏移，避免浮点误差；
- Branch 完全继承父地图时间轴，不允许独立改变第 0 天或时间轴；
- Calendar 是同一内部时间的显示表达；
- 每张 Map 有一个默认日历，也可显示辅助日历；
- 日历改革可以由 Command 表达历史行为，并关联独立 Calendar Configuration；
- 日历改革只改变日期解释，不改变内部累计天数、Command 执行时间、State Change 执行时间或事件先后关系；
- 允许追溯修改日历改革生效时间，只重新计算日历显示，不重建 World State。

### 6.2 V0.1 的错误/过期设计

V0.1 将 `HistoricalTime = (calendar: CalendarId, ordinal: Ordinal)`，并把 Calendar 参与时间序比较。这会使“切换日历/日历改革”与内部历史时间耦合，和 D01-37~49 冲突。

### 6.3 推荐基线

V0.2 应将概念分为：
- `HistoricalTime`：Map 内部单调、可全序比较的累计时间坐标；
- `CalendarConfiguration`：将内部时间解释/显示为某种日期的规则；
- `CalendarReformCommand`：在世界历史中表达一次历法改革行为，并触发某个 Calendar Configuration 在指定内部时间生效。

推荐内部表达为整数累计天数 + 当天内整数秒偏移。精确序列化、时区概念、历法算法、辅助日历换算等属于 P2-02 / 技术设计，不在本轮定义。

### 6.4 持续 Command 的时间

持续 Command 有开始与结束时间，可以产生多个阶段性 State Change。暂停期间：
- 世界历史时间继续推进；
- 当前持续 Command 自身的执行时钟与行动进度暂停；
- 暂停期间不消耗剩余行动时间；
- 恢复后从暂停进度继续；
- 系统不得擅自修改用户指定结束时间。

因此持续 Command 至少涉及“世界历史坐标”与“Command 自身执行进度”两个概念；后者的精确状态模型、阶段派生算法与算法版本控制仍需技术设计。

**依据：** D01-37~49、D01-50~54、D01-03~04。

---

## 7. Order Constraint、Dependency 与 Warning

### 7.1 已确认的领域原则

- 用户可指定同一历史时间内 Command 的顺序；未指定时 App 默认排序。
- 默认排序优先考虑依赖关系。
- 不影响结果的技术性歧义可用固定规则处理。
- 会改变世界状态的语义歧义必须由用户裁决。
- 用户指定顺序保存为两个 Command 之间的显式 Order Constraint，而不是只保存一次排序结果或编号。
- 父地图历史中的 Order Constraint 被 Branch 继承；Branch 可以增加自己的约束，但不能覆盖父约束。
- 时间修改或约束变化造成冲突/循环时，保留用户约束，报告问题，不擅自删除。
- 完整历史重建采用 Fail-Fast：遇到第一个无法执行的 Command 即停止后续重建。
- Warning 与 Error 不应混为一谈：ADR-003 中特定历史质量警告不阻止播放；D01 的执行错误会使完整重建停止。

### 7.2 推荐领域区分

V0.2 应至少区分：
1. **Explicit Order Constraint**：用户明确指定并持久保存的顺序约束，属于创作规则数据；
2. **Implicit Dependency**：系统从命令语义与目标对象关系中发现的依赖，属于可重新发现的派生信息；
3. **Execution Order**：某次重建中按时间、显式约束、可识别依赖形成的实际执行序列；
4. **Warning**：历史质量或顺序异常的持续诊断记录；
5. **Execution Error**：导致本次完整重建停止的执行失败。

不能将 `sequence` 整数视为 Order Constraint 的完整替代，也不能把“存在 Warning”一律视为重建错误。

### 7.3 Fail-Fast 与不阻断播放的边界

两套规则针对不同问题：
- ADR-003 A10-06：特定历史质量异常的 Warning 不阻止播放；
- D01-58/59：完整历史重建遇到第一个无法执行的 Command，立即停止，不继续后续重建。

V0.2 必须明确诊断级别与执行结果的区分，不得写成“所有警告都不阻断”或“任何异常都阻断”。

**依据：** ADR-003 A10-04~07；D01-11~28、D01-55~64、D01-73。

---

## 8. World State、Checkpoint 与历史重建

### 8.1 World State

World State 是某一 Map / Branch 在指定 Historical Time 下，按其有效历史操作重建得到的完整合成状态。它不是作者直接维护的第二套历史，也不应承载 UI 布局或诊断信息。

### 8.2 Checkpoint

已确认：
- Checkpoint 按 Branch 归属；
- 保存指定 Branch 在指定历史时间点的完整合成 World State；
- 足以独立恢复该时间点状态并继续执行后续 Command；
- 不保存 Command、State Change、顺序约束、可重新计算警告/错误、可重新发现隐式依赖、UI 状态；
- Checkpoint 是派生缓存，不是权威历史；
- 使用前校验完整继承路径版本及数据完整性；
- 原子发布、完成标记、MD5 校验职责不同；
- 祖先历史变化导致受影响路径上的 Checkpoint 逻辑失效；数据损坏则是单个 Checkpoint 的问题，不自动传播；
- 后代 Branch 可以逐层复用祖先有效 Checkpoint，但恢复后仍继续执行当前 Branch 自己的 Command。

### 8.3 重建触发与失败

- 用户修改多条 Command 后，可以先连续编辑，再主动触发重建。
- 用户主动重建时，系统确定最早受影响时间，尽量复用仍有效的结果。
- 完整历史重建 Fail-Fast。
- 用户修复后，从最早受影响位置重新执行。
- 系统不能自动改写作者 Command、自动补充未设计的历史行为或自动接受替代路线。
- Checkpoint 损坏引发的缓存自愈属于恢复可靠性行为，不等同于用户修改历史后的主动重建。

### 8.4 V0.1 需要修正

V0.1 把 World State 纯函数重建作为核心方向仍可保留，但必须将权威输入改为 Command 历史及适用规则；Checkpoint 不应仅写成一般 memoization；D01-143 已明确其完整保存内容和排除项。

**依据：** D01-58~69、D01-72、D01-77~100、D01-101~143。

---

## 9. 地图整合（History Path Materialization）

地图整合不是 Branch Merge，也不是运行时双向同步。

已确认规则（D01-74/75）：
- 假设 B 从 A 的 1200 年分叉，整合生成 C；
- C 采用整合时 A 最新的、截至 1200 年（≤1200）的有效历史；
- 接入 B 在 1200 年之后（>1200）的自有 Command；
- 不接入 A 在 1200 年之后的 Command；
- B 在恰好 1200 年的 Command 不纳入 C；
- C 生成独立完整历史，不再动态继承 A 或 B；
- C 中 Command 使用新 ID，重映射显式依赖与顺序约束；
- 保留来源追溯信息，但来源 ID 不构成运行时依赖；
- 隐式依赖与派生 State Change 在 C 中重新发现、验证或重建；
- C 必须独立进行完整历史验证。

V0.2 可将此列为领域级“历史路径固化”操作，但不应把它纳入 Branch 重建算法或引入 Merge / Conflict Resolution。

**依据：** D01-74/75；ADR-001 §16；ADR-002 §3.4。

---

## 10. 对 P2-01 V0.1 的逐项处置建议

| V0.1 内容 | 审计结论 | V0.2 处置 |
|---|---|---|
| §0 State Change 驱动总纲 | 已被 D01 Command 权威模型取代 | 重写 |
| §1 十个领域实体 | 有效概念与过期假设混杂 | 重新组织 |
| Map / Object / Initial State | 基本概念可保留 | 保留并补足身份、存在状态边界 |
| History 统一实体 | ADR-002 已冻结 | 保留并扩展到 Shared Object History 抽象 |
| Event 归 History | 归属原则保留 | 补充其叙事独立性 |
| StateChange 归 History | 归属/来源可保留，权威性必须改写 | 作为派生结果表达 |
| `HistoricalTime=(calendar_id, ordinal)` | 与 D01-37~49 冲突 | 替换为内部累计时间坐标 |
| StateChange 固定 `(time, sequence)` 排序 | 不足以表达显式顺序约束 | 改为时间 + 依赖 + 显式约束的执行序列 |
| `change_type.apply` 纯函数 | 可作为引擎实现约束参考，不是完整领域结论 | 移至 Engine Design |
| 写入时强制对象存在性校验 | 与 ADR-003 A10-09 冲突 | 按命令类型区分结构校验与历史质量警告 |
| Branch `parent_history + parent_at` | 方向正确 | 保留并补足闭区间继承、严格晚于写入边界 |
| Branch 有子分支时禁止删除 | 仅为 V0.1 建议，未见 ADR 明确确认 | 保留为待裁决/推荐规则，不标为冻结 |
| World State 纯函数重建 | 原则可保留 | 权威输入改为 Command 历史 |
| Checkpoint 仅 memoization | 已被 D01 大幅具体化 | 按 D01-65~69、72、77~143 重写 |
| Shared / Local History | 旧 A10 仅列为歧义，后由 ADR-003 正式裁决 | 完整纳入领域模型 |
| destroy 后再变更错误 | 已被 ADR-003 V1.2 A10-09 取代 | 删除旧规则，改为执行、不复活、持续警告 |
| Storage | 不属于 P2-01 领域语义范围 | 独立 P2-09 审计 |

---

## 11. 尚未解决但不阻塞 V0.2 的事项

以下事项必须在 V0.2 中明确登记，不得假装已经裁决：

### 11.1 领域语义待确认/待细化

1. **尚未创建对象的后续 Command**：ADR-003 明确已删除对象的规则，但尚未完整裁定“对象尚未创建”的所有播放行为。
2. **删除对象后的属性数据**：已确认不得复活、允许命令执行并持续警告；具体属性数据如何保留、查询和呈现未裁决。
3. **对象删除/恢复/重新创建状态机**：是否存在 `restore` 语义、恢复与新建的身份关系未裁决。
4. **Command 与 Event 的完整关联基数及生命周期**：不得固定一对一；保留开放设计。
5. **Transaction Time 与 Valid Time**：系统何时获知/记录某条 Command，与世界中何时生效，完整关系未裁决。
6. **持续 Command 的执行进度模型**：暂停规则已确认，具体阶段生成算法、执行依据与算法升级兼容机制未完成。
7. **Branch 删除存在后代时的处理**：ADR-001/005 确认 Branch 可删除，但现有冻结文本未明确后代约束。V0.1 的“有子分支禁止删除”只是提案建议，不能冒充冻结规则。
8. **共享历史与 Branch 动态继承的全部组合细节**：基本语义已确认，但 Shared Object History 在多层 Branch、引用集合变化、整合后的具体适用范围仍需补足。

### 11.2 技术设计待完成

- 同一执行序列的默认排序算法；
- 隐式依赖发现算法；
- Order Constraint 的冲突检测算法；
- Command 执行器、阶段性 State Change 的生成与可复现机制；
- 算法版本/执行依据兼容策略；
- Checkpoint 文件格式、存储布局与任务持久化；
- Branch / Shared Object / Map 的具体持久化结构；
- 警告与错误的数据结构、状态机和 UI。

### 11.3 可采用的推荐默认项（仍需明确标记为推荐）

为推进 V0.2，建议暂采用以下写作基线，而非声称用户已逐项确认：
- 删除有后代的 Branch 时，默认拒绝直接删除，要求先处理后代，避免产生悬空继承引用；不采用未经确认的级联删除。
- History 作为统一逻辑抽象；具体持久化实现可分表/分文件，不要求物理同构。
- 显式 Order Constraint 是权威用户约束；隐式 Dependency 每次重建可重新发现。
- 对象尚未创建的引用行为不沿用“已删除对象”的特例，单独登记并保持未裁决。
- 持续 Command 的执行结果必须可确定性重建；算法升级不得静默改变既有历史结果。

这些推荐项进入 V0.2 时应标注 `Recommended / Pending Confirmation`。

---

## 12. 领域语义审计结论

本轮可以认为**核心领域语义审计已达到可以启动 V0.2 重构的程度**。当前不存在需要推翻 ADR-001、ADR-002、ADR-003、ADR-005 的理由；需要做的是让 P2-01 新提案正确吸收这些冻结裁决与 D01 阶段性确认。

V0.2 应按以下顺序组织：

1. Scope、权威层级与核心不变量；
2. Map、Object Identity、Initial State；
3. History 与历史归属；
4. Command 与执行依据；
5. Event；
6. State Change 与派生结果；
7. Historical Time 与 Calendar；
8. Shared Object History 与 Map/Branch Local History；
9. Branch 动态继承；
10. Command Ordering、Dependency、Warning 与 Error；
11. World State Reconstruction；
12. Checkpoint 与重建边界；
13. History Path Materialization；
14. 未决事项与后续 P2 工作边界。

**下一步建议：据此编写《P2-01 — Domain Model Proposal V0.2》。**

V0.2 仍应保持 `Proposed / Review`，并在文档首页声明：其内容是当前架构讨论基线的整合提案，不自动取代已冻结 ADR，也不代表 Phase 2 整体冻结或开发授权。