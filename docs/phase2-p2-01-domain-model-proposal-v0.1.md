# P2-01 — Domain Model Proposal V0.1

- **项目**：World View
- **阶段**：Phase 2 / P2-01
- **版本**：V0.1
- **日期**：2026-11-18
- **状态**：**Proposed / 审查基本完成**（A1 → ADR-002；A10 → ADR-003 V1.2；A5 → ADR-005；A11/A12 已按 ADR-003 V1.2 同步解除张力）
- **上游文档**：
  - [Architecture Baseline V1.0](architecture-baseline-v1.0.md)（§30 核心领域模型、§41 INV-01~16）
  - [ADR-001 地图历史与分支模型](adr/ADR-001-map-history-and-branch-model.md)（已冻结）
  - [ADR-002 统一 History 实体](adr/ADR-002-unified-history-entity.md)（已冻结，裁决本文歧义项 A1）
  - [ADR-003 共享历史与 Map-local 命令播放模型](adr/ADR-003-shared-local-history-command-playback.md)（已冻结 **V1.2**，裁决本文歧义项 A10）
  - [ADR-005 Branch 历史分叉规则](adr/ADR-005-branch-fork-rules.md)（已冻结，A5 正式化）
  - [明确否决设计登记表](adr/rejected-designs.md)（2026-11-18 合并登记）
  - [Phase 2 — Domain Model & Historical State Engine V1.0](phase2-domain-model-historical-state-engine-v1.0.md)
- **范围**：仅领域模型。**不设计数据库、不设计 API、不做 UI、不迁移旧代码。**
- **实施顺序**：Domain → Historical Engine → Storage → API → UI
- **本文覆盖**：Phase 2 实施顺序中的 P2-01（Domain Object Model）、P2-02（Historical Time）、P2-03（Event）、P2-04（State Change）、P2-05（World State Reconstruction）、P2-06（Branch）、P2-07（Branch Reconstruction）的**领域语义层**。Shared/Local History（P2-08）与 Persistence（P2-09）不在本文解决，但本文的模型不得阻塞它们。

---

# 0. 提案总纲

一句话模型：

> **Map 拥有对象集合与一条根历史；历史由按 Historical Time 排序的 State Change 构成；World State 是 `初始状态 + 截至 T 的全部 State Change` 的纯函数计算结果；Branch 是一条以 `parent + parent_at` 声明的派生历史，永久分叉、永不合并。**

与已冻结裁决的关系：

- 不引入 WorldStateVersion / ObjectVersion / Revision / Commit（Baseline §39、Phase 2 §2）；
- 不引入 Merge / Conflict / Rebase（ADR-001 §16）；
- Branch 不复制祖先历史（INV-08）；
- Event 与 State Change 解耦，State Change 可独立存在（INV-05）。

---

# 1. Domain Entity 有哪些

第一版领域实体共 10 个：

| # | Entity | 种类 | 职责 |
| --- | --- | --- | --- |
| 1 | **Map** | 聚合根 | 创作容器。拥有对象注册表、初始状态、根历史、分支集合 |
| 2 | **Object** | 实体（有稳定 Identity） | 被历史改变的世界对象。只承载 Identity + 类型，**不承载状态** |
| 3 | **ObjectState** | 值对象 | 某对象在某个时刻的属性集 + 几何。永远是**计算结果**，不存储 |
| 4 | **InitialState** | 值集合（Map 的一部分） | Map 元年（epoch）时刻的对象状态全集，是历史的起点 |
| 5 | **History** | 实体 | 历史容器：一条有序的 State Change 集合（可含关联 Event）。Map 的根历史与 Branch 的自有历史是**同一种实体** |
| 6 | **StateChange** | 实体 | 历史的最小变更单元。唯一合法的状态改变机制 |
| 7 | **ChangeType** | 注册表项 | 语义变更类型：类别 + payload 模式 + 纯函数 `apply`。开放注册，可扩展 |
| 8 | **Event** | 实体 | 叙事记录（"发生了什么"）。不直接改状态 |
| 9 | **Branch** | 实体 | 分叉声明：`parent_history + parent_at` + 自有 History |
| 10 | **HistoricalTime / Calendar** | 值对象 | 可全序比较的历史时间；Calendar 定义序的语义 |

**明确不建立的实体**（对应 Baseline §39 / Phase 2 §2）：

```text
WorldStateVersion   ✗
WorldStateRevision  ✗
WorldStateCommit    ✗
ObjectVersion       ✗
Merge / Conflict    ✗
Release / Approval  ✗
```

**关于命名**：Baseline 中 Map 下的历史容器出现两个名字——`History`（§3）与 `Timeline`（§30、§42）。本提案统一用 **History** 作为领域实体名；**Timeline 视为 History 的播放投影（视图概念）**，不单独建实体。

**关于 History 的边界（已冻结，ADR-002 §5）**：History 是**世界创作历史**的容器，不是系统认知时间（Transaction Time）的替代品。同一段历史的资料认知差异、备选路线（A/B）、权威与猜测等认知表达问题，**不得建模为 Branch**，须以独立机制另行设计。

**关于 Object 的字段模式**：第一阶段 ObjectState 用"命名字段字典"（name/type/location/...），不建严格的 per-type 字段 Schema 注册表；strict schema 留到对象体系扩展（Character/Unit）时再定。这不阻塞引擎正确性。

---

# 2. Entity 之间是什么关系

```text
Map 1 ─── 1 History (root)
Map 1 ─── N Object            （注册表：对象归属哪张地图）
Map 1 ─── N Branch
Branch 1 ── 1 History (own)
Branch N ── 1 History (parent)   携带 parent_at

History 1 ── N StateChange     （变更的历史归属，见 §18 语义）
History 1 ── N Event           （叙事同样按历史归属）

StateChange N ── 1 Object      （target，按 ObjectId 引用；可指向祖先地图注册表中的对象）
StateChange 0..1 ── 1 Event    （可选叙事关联，方向：change → event）
ChangeType 1 ── N StateChange  （类型注册表）
```

四条关键关系规则：

## 2.1 历史归属（ownership）

每条 State Change **必须属于且只属于一个 History**：要么是某 Map 的根历史，要么是某 Branch 的自有历史。这直接落实 Phase 2 §18"分叉后的 State Change 必须有明确的历史归属"，也是 HSE-07（Branch Change 不影响父 Map）的结构保证。

## 2.2 对象引用与解析（reference ≠ ownership）

StateChange.target 是 **ObjectId 引用**，不要求 target 与 change 同属一个注册表。Branch 的变更可以指向父 Map 注册的对象（对象继承，Phase 2 §20）。解析规则：沿 parent 链向上查找对象注册表。这个"归属与引用分离"的设计同时为 P2-08 Shared Object 留出空间（见 §7-A10）。

## 2.3 Branch 的父是 History，不是 Map

Branch.parent 指向 **History**（根历史或另一 Branch 的自有历史），而不是直接指向 Map。这样 Map 与 Branch 在重建算法里是同构的，Branch-of-Branch 无需特判。Map 只是通过"拥有根历史"参与这条链。

## 2.4 Event 按历史归属

Event 与 State Change 一样挂在 History 上。因此**分支可以拥有自己的 Event**（ADR-001 §26 架空分支要求"Event、State Change 可以完全不同"）。Event 与 State Change 之间无父子结构（Baseline §10：Event 不是 State Change 的父对象），只有可选的引用。

---

# 3. Historical Time 怎么表达

## 3.1 结构

```text
HistoricalTime = (calendar : CalendarId, ordinal : Ordinal)
```

- `Ordinal` 第一版实现为**有符号整数 tick**。注意：tick 的含义由 Calendar 声明，引擎**不把时间当作"年"**。公历纪年日历可以声明 tick = 年；未来"王朝 17 年""第三纪元 250 年"由不同 Calendar 表达，或同一 Calendar 内含 Era 分段。
- 引擎对时间只要求四种能力：`compare`（T1 < T2 / = / >）、`min`、`max`、以及按序遍历。**不含任何格式化/显示职责**（那是 Calendar 的展示层，延后）。

## 3.2 Calendar

```text
Calendar:
  calendar_id
  epoch 语义、tick 粒度、序的定义
  （显示/换算延后）
```

- **一张 Map 拥有一个主 Calendar**，Branch 原样继承父链的 Calendar，不得更换。
- 原因：`t ≤ parent_at` 这类跨历史比较（HSE-04）要求父子在同一时间轴上。跨 Calendar 换算第一阶段明确**不做**（见 §7-A9）。

## 3.3 边界语义（必须冻结的细节）

| 语义 | 裁定 |
| --- | --- |
| State Change 生效是**闭式**的：`reconstruct(T)` 应用所有 `time ≤ T` 的变更 | Test 02（1100 的变更，查询 1100 → 新状态） |
| 查询 `T < epoch` | 返回错误（历史从 Initial State 开始） |
| `T = epoch` | 返回 Initial State 本身 |
| 同一 `(history, time)` 多变更 | 按 `sequence` 升序（Phase 2 §13） |
| Branch 自有变更的时间窗 | **严格大于** parent_at（见 §5.2） |

## 3.4 有效区间（Baseline §8.2 / §11）

默认模型是**生效时间点**；对象状态的"有效区间"（如 `1940–1950 国境 B`）是引擎可**派生**的视图（本变更生效到下一变更为止）。作者显式书写区间的语法（如 `until_time`）延后，且落地方式需要一个小裁决（§7-A4）。P2-01 只冻结：**区间不进入第一版引擎的输入模型，派生视图不影响重建算法。**

---

# 4. State Change 怎么表达

## 4.1 字段

```text
StateChange:
  change_id      : ChangeId          # 不可变、全局唯一
  history_id     : HistoryId         # 归属（根历史 或 branch 自有历史）
  target         : ObjectId          # 被改变的对象
  effective_time : HistoricalTime    # 生效时间（闭式）
  sequence       : int               # 同一 (history_id, effective_time) 内的稳定顺序
  change_type    : ChangeTypeId      # 语义类型（注册制）
  payload        : 按 change_type 的模式约束的结构化数据
  event_id       : EventId?          # 可选：关联叙事事件
```

排序键：`(effective_time, sequence)`，在一个 History 内全序、确定（HSE-02/03）。`sequence` 在 `(history_id, effective_time)` 内唯一。

## 4.2 ChangeType：注册制语义类型（Phase 2 §8/§9 的落地）

Phase 2 §9 给出两大基本类别；§8 又要求"可解释的领域变化"。落地方式：

```text
ChangeType = { type_id, category, payload_schema, apply(state, payload) → state }
```

- `category ∈ { Property, Geometry, Lifecycle }`
- `apply` 是**纯函数**：无时钟、无随机、无外部 IO。违反纯度即破坏 HSE-01。

第一批注册四个类型：

| type_id | category | payload | 对应示例 |
| --- | --- | --- | --- |
| `property.set` | Property | `{ field, value }` | POI.name → 圣都；Character.status → dead |
| `geometry.replace` | Geometry | `{ geometry }` | 山脉 → 平地；国境 A → 国境 B |
| `object.create` | Lifecycle | `{ object_type, initial_state }` | 王国建立；新建 POI |
| `object.destroy` | Lifecycle | `{}`（可选 reason） | 王国灭亡（Baseline §8 示例） |

说明：

- `Lifecycle` 是本提案在 Phase 2 §9 两大类之外**新增的第三类**，理由：§8 示例明确包含"Kingdom destroyed"，且对象必须在历史中途可创建。是否进第一批实现见 §7-A2。
- 类型注册表**开放**：未来 `boundary.transfer`、`kingdom.vassalize` 等语义类型按同一模式注册，不改引擎结构。
- `property.set` 是通用兜底，但它是注册表中的普通一员、不是唯一机制——这就守住了 Phase 2 §8"不采用操作日志作为唯一模型"的边界。

## 4.3 写入时校验（领域规则，非存储规则）

- target 对象在变更所属历史链上可解析，且在生效时刻已存在（对 `object.create` 例外）；
- `epoch ≤ effective_time`；若归属 Branch，则 `effective_time > parent_at`（§5.2）；
- sequence 在 `(history_id, effective_time)` 内唯一；
- payload 符合 change_type 模式；
- 不得对已 destroy 的对象再写变更（§7-A12）。

> ✅ **2026-11-18 同步（ADR-003 V1.2 / A10-09）**：本节"对象可解析且已存在""不得对已 destroy 对象再写变更"两条已按裁决调整为**警告级检查**——针对已删除（或尚未创建）对象的命令**允许写入、允许进入播放序列、不自动复活对象**，由 App 产生持续警告（详见 [ADR-003](adr/ADR-003-shared-local-history-command-playback.md) A10-09）。**时间窗与 payload 结构校验保留写入级，不受影响。**
---

# 5. Branch 怎么表达

## 5.1 结构

```text
Branch:
  branch_id         : BranchId
  name              : string
  parent_history_id : HistoryId      # 指向任意 History（根历史或另一 Branch 的自有历史）
  parent_at         : HistoricalTime # 分叉时刻
  status            : active | deprecated
  history_id        : HistoryId      # 自有历史
```

- `deleted` 不是状态：删除是移除 Branch 及其自有历史的**操作**（HSE-09），废弃才是状态（HSE-10，仅标记、不动数据）。
- 分叉时**零复制**：不复制祖先 Event/State Change（INV-08），不复制对象注册表（Phase 2 §20）。Branch 只新增上面这组声明。

## 5.2 时间窗不变量（本提案的关键裁定）

```text
Branch 自有变更的有效时间 必须 满足：  parent_at < effective_time
```

即：分叉点本身（含）以前完全属于父历史。推论：

- `reconstruct(branch, t)`，当 `t ≤ parent_at` 时**恒等于** `reconstruct(parent, t)`（HSE-04 由构造直接保证）；
- 用户想"改写 1100 年的历史"→ 正确动作是在 **1100 建分支**（ADR-001 的模型本来就是这样），而不是在 parent_at=1200 的分支上写一条 1100 的变更；
- 该约束在写入时强制（§4.3），跨父子的变更序列因此永不交错，确定性成立。

## 5.3 Branch 的其他语义

| 语义 | 裁定 |
| --- | --- |
| Branch of Branch | 允许（树形，禁止环）。parent 可指向另一 Branch 的 History |
| 删除 Branch | 删自有历史，父历史与其他分支不受影响（HSE-09）。**有子分支时禁止删除**，须先处理子分支（§7-A6） |
| 废弃 Branch | 仅 status 标记，数据不动，可恢复（HSE-10，Baseline §22） |
| Copy Object | 新 ObjectId + 在分支自有历史写一条 `object.create`（payload = 源对象在复制时刻的快照）。此后两者完全独立（HSE-08）。"复制时刻"即该 create 变更的 effective_time |
| Copy Map | 与 Branch 完全不同的操作（INV-13）：对 Map 聚合做深复制，生成一张完全独立的新 Map（含全部历史）。属 P2-08 之后的操作，本模型不阻塞它 |
| Merge | **不存在**。模型中没有任何指向 Merge 的结构（ADR-001 §16/§28） |

---

# 6. World State 如何重建

## 6.1 算法

```text
reconstruct(history, T) → WorldState:

  if history 是根历史 (Map):
      ws ← map.initial_state                    # epoch 时刻对象全集
      for sc in history.state_changes
           where sc.effective_time ≤ T          # 闭式
           ordered by (effective_time, sequence):
          ws ← sc.change_type.apply(ws, sc)     # 纯函数
      return ws

  if history 是 Branch:
      if T ≤ branch.parent_at:
          return reconstruct(parent_history, T)         # 直接委托父链
      ws ← reconstruct(parent_history, parent_at)       # 重建到分叉点
      for sc in branch.state_changes
           where parent_at < sc.effective_time ≤ T
           ordered by (effective_time, sequence):
          ws ← sc.change_type.apply(ws, sc)
      return ws
```

## 6.2 派生查询

```text
getWorldState(map_or_branch, T)  =  reconstruct(history, T)

getObjectState(map_or_branch, object_id, T)
  = reconstruct(history, T) 中投影到该对象
   （可按对象短路计算，纯优化，不改语义）

WorldState 值结构:
  { at: HistoricalTime,
    objects: { ObjectId → ObjectState { exists, type, fields, geometry? } } }
```

- 对象在 T 尚未 create / 已 destroy → `exists = false`，这是**合法答案**，不是错误。
- 分支深度 = 递归深度，链上逐级切片，任一级的"分叉点以前"永远等于父链（HSE-04/05/06）。

## 6.3 为什么不需要版本实体

WorldState 是 `(history 内容, T)` 的**纯函数**：

- 相同输入 → 相同输出（HSE-01，由纯 apply + 全序排序保证）；
- 父历史被修改 → Branch 重算自然反映（HSE-06，因为 Branch 只引用父链）；
- 需要性能缓存时，可以对 `(history 内容指纹, T)` 做 memoization——**这是实现优化，不产生任何新的领域概念**（不出现 Snapshot/Version 实体）。缓存策略延后到引擎实现阶段，不属于本提案。

## 6.4 不变量对照

| 不变量 | 由什么保证 |
| --- | --- |
| HSE-01 确定性 | 纯 apply + `(time, sequence)` 全序 |
| HSE-02/03 顺序应用 | 排序键 + sequence 唯一性 |
| HSE-04 分叉点前等价父 Map | `t ≤ parent_at` 直接委托父重建 |
| HSE-05 分叉点后只应用自己 | 变更按 history_id 归属 + 时间窗过滤 |
| HSE-06 父修改传播 | Branch 只引用父链，不复制 |
| HSE-07 Branch 不影响父 | 变更归属 History，结构上隔离 |
| HSE-08 Copy 后独立 | 新 ObjectId + 独立 create 变更 |
| HSE-09/10 删除/废弃 Branch | 删除仅移除自有历史；废弃仅 status |

---

# 7. 哪些地方存在架构歧义

以下歧义**不阻塞本提案的方向**，但需要在进入实现前后逐一裁定。每条给出本提案的建议，供审查时拍板：

| # | 歧义 | 现状/张力 | 建议 |
| --- | --- | --- | --- |
| **A1** | **History 统一实体 vs Map/Branch 各自持有变更** | Baseline §30 把 Event/SC 画在 Map 的 Timeline 下、Branch 另有 Own History；未明说二者是否同构 | ✅ **已裁决（2026-11-18）**：统一 History 实体（根 + 分支同构），重建算法只有一份；History ≠ 认知时间容器。冻结于 [ADR-002](adr/ADR-002-unified-history-entity.md) |
| **A2** | Lifecycle（create/destroy）是否进第一批实现 | Phase 2 §9 只列两大类，但 §8 示例含"Kingdom destroyed"；Test 01–07 又不需要它 | 类型注册表中保留四类；第一批引擎实现 `property.set` + `geometry.replace`（对应 Test 01–07），`object.create/destroy` 在 P2-04 随 2 个新增测试一起实现 |
| **A3** | `property.set` 兜底与语义类型的边界 | 兜底太强会退化回操作日志（Phase 2 §8 明令禁止的"唯一模型"） | 兜底保留但仅作过渡；UI/API 层优先提供语义类型。何时禁止裸 `property.set` 需未来裁决 |
| **A4** | 显式有效区间的落地方式 | Baseline §8.2/ADR-001 §8.2 冻结"支持区间"，但引擎侧有三种做法：派生视图 / 变更带 `until_time` / 脱糖为成对变更 | P2-01 只做生效时间点 + 派生区间视图；显式书写区间延后，落地时补一条小裁决 |
| **A5** | 分支变更能否落在 `t = parent_at` | 若允许，父链在 parent_at 的变更与分支变更排序未定义 | ✅ **已裁决（2026-11-18）**：**禁止**，`effective_time > parent_at`（§5.2）；完整分叉规则（九条）冻结于 [ADR-005](adr/ADR-005-branch-fork-rules.md)。想改分叉点当年的历史 → 把分叉点前移 |
| **A6** | Branch of Branch；删除有子分支的 Branch | ADR-001 未覆盖多层分叉与删除传播 | 允许多层（树形）；删除有子分支的 Branch 先禁止（须先删子分支）。备选：级联删除（更危险，不推荐） |
| **A7** | Event 的归属与关联方向 | Event 归 Map（Baseline §30）还是归 History（分支要有自己的 Event——ADR-001 §26 要求）？链接谁指向谁 | Event 归 History；方向为 `change.event_id → event`（可空），Event 的变更清单反向查询得出 |
| **A8** | sequence 的分配权威 | 用户显式编号 vs 系统分配；修改历史时能否重排 | 系统分配（同时间桶内追加式）；第一阶段不支持重排。重排语义未来需要裁决 |
| **A9** | 跨 Calendar / 多历法并存 | "魔法纪元 1000"与"第三纪元 250"是不同 Calendar 还是同一 Calendar 的 Era 分段；跨历法换算无全局时轴 | 一 Map 一主 Calendar，分支继承，跨历法换算不做；Era 分段方式留给 Calendar 设计（P2-02 实现时定） |
| **A10** | **Shared Object / Map-local 的状态合成顺序（P2-08）** | Baseline §26–29：共享对象有自身历史，Map 又有局部历史，两者在变更序列中如何排序、同一时刻谁赢 | ✅ **已裁决（2026-11-18）**：不采用状态覆盖/固定优先级/多结果合成，采用**命令驱动播放模型**（单条命令序列、App 默认排序+用户可调、警告不阻止播放、警告独立生命周期），冻结于 [ADR-003](adr/ADR-003-shared-local-history-command-playback.md)。排序算法 / 警告机制 / Command 模型等技术设计留待 P2-08 |
| **A11** | 校验时机 | 写入时校验（对象存在性、时间窗、payload）vs 应用时校验 | ✅ **已裁决（2026-11-18，ADR-003 V1.2 / A10-09）**：对象存在性 / 生命周期引用为**警告级**（命令可存在、可执行、不复活对象）；时间窗与 payload 结构校验保留写入级 |
| **A12** | destroy 之后的再变更 | 对已销毁对象写变更是错误还是允许"重建" | ✅ **已裁决（2026-11-18，ADR-003 V1.2 / A10-09）**：命令**允许执行 + 不自动复活对象 + 持续警告**；"命令执行 / 对象存在 / 警告"三维独立。删除 / 恢复 / 重新创建的完整状态机（含是否引入 `object.restore`）仍待技术设计（ADR-003 §3-13） |

**审查进度（2026-11-18）**：三个优先项全部裁决完成——A1 → [ADR-002](adr/ADR-002-unified-history-entity.md)；A5 → [ADR-005](adr/ADR-005-branch-fork-rules.md)；A10 → [ADR-003 V1.2](adr/ADR-003-shared-local-history-command-playback.md)（命令驱动播放模型 + A10-09 删除后修改语义）。A11/A12 原与 ADR-003 的张力已按 V1.2 裁决解除并同步；其余各项（A2–A4、A6–A9）如无异议即按建议执行。

---

# 8. 与实施顺序的对应

```text
P2-01  Domain Object Model      ← 本文 §1/§2
P2-02  Historical Time          ← 本文 §3
P2-03  Event                    ← 本文 §2.4 + §4.1(event_id)
P2-04  State Change             ← 本文 §4
P2-05  World State Reconstruction ← 本文 §6
P2-06  Branch                   ← 本文 §5
P2-07  Branch Reconstruction    ← 本文 §6.1
P2-08  Shared / Local History   ← 播放原则已裁决（ADR-003）；排序算法/警告机制/Command 模型等技术设计待做
P2-09  Persistence              ← 本文明确不做（Domain → Engine 之后）
P2-10  Test Matrix              ← Phase 2 §24/§25 的 HSE-01~10 + Test 01~07
```

**下一步**：本提案审查通过后，按上述顺序进入 Historical Engine 实现（先 P2-01/02/04/05 的最小闭环，Test 01–04 先行，再 Branch Test 05–07）。存储（P2-09）在引擎通过测试矩阵之前不启动。

---

# 9. 尚待技术设计问题汇总（2026-11-18 登记）

以下为跨裁决汇总的技术设计待办。**登记不等于裁决**；各项在完成技术设计并经人工确认前，不得当作已确认方案（各项原始登记位置见括号）：

| # | 待设计问题 | 原始登记 |
| --- | --- | --- |
| 1 | Command 的完整领域模型（含与 StateChange 的对应关系） | ADR-003 §3-1 |
| 2 | History 的内部组织和持久化结构 | ADR-003 §3-2 |
| 3 | 自动排序算法 | ADR-003 §3-3 |
| 4 | 同一历史时间内命令的排序与依赖规则 | ADR-003 §3-4 |
| 5 | 用户指定顺序的表达、保存和维护机制 | ADR-003 §3-6 / A10-04 |
| 6 | 新命令与既有人工顺序的交互规则 | ADR-003 §3-6 |
| 7 | 历史异常检测规则及警告状态机 | ADR-003 §3-7 |
| 8 | 警告与 Undo / Redo 的关联处理 | ADR-004 §8-7 |
| 9 | Undo / Redo 的具体数据结构和事务边界 | ADR-004 §8-1 / 3 / 9 |
| 10 | 历史重建的缓存、失效和重新计算机制 | 本次新增登记（对应本文 §6.3 的缓存延后原则） |
| 11 | 删除后修改命令在内部状态重建算法中的具体实现方式 | ADR-003 V1.2 §3-11 |
| 12 | 对已删除对象执行属性修改后，属性数据的保存与呈现方式 | ADR-003 V1.2 §3-12 |
| 13 | 对象删除、恢复、重新创建之间的完整状态机（含是否引入 `object.restore`，需另行裁决） | ADR-003 V1.2 §3-13 |
