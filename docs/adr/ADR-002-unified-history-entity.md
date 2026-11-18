# World View — 统一 History 实体架构裁决 V1.0

- **文档状态**：Accepted / Frozen
- **版本**：V1.0
- **日期**：2026-11-18
- **适用范围**：领域模型中的历史容器结构（History）、Map 与 Branch 的历史归属、Event / State Change 的归属
- **性质**：架构裁决（ADR）
- **上游文档**：[ADR-001 地图历史与分支模型](ADR-001-map-history-and-branch-model.md)、[Architecture Baseline V1.0](../architecture-baseline-v1.0.md)、[P2-01 Domain Model Proposal V0.1](../phase2-p2-01-domain-model-proposal-v0.1.md)（歧义项 A1）
- **后续实施依据**：是
- **是否允许实现阶段重新解释本模型**：否；如发现冲突，应提交新的 ADR，不得自行改变本模型

---

# 1. 裁决摘要

P2-01 提案歧义项 **A1 正式确认**：

> **Map 与 Branch 统一使用同一种 History 实体参与历史重建。**

Map 拥有一条根 History；Branch 拥有自己的 History，并通过 `parent History + parent_at` 引用父历史，**不复制**。

# 2. 统一结构

```text
Map
 └── Root History
      ├── Event
      ├── State Change
      │
      ├── Branch A
      │    └── Own History
      │         ├── Event
      │         └── State Change
      │
      └── Branch B
           └── Own History
                ├── Event
                └── State Change
```

---

# 3. 四个结构边界

## 3.1 Map 拥有根 History

地图的初始状态和后续历史变化都属于这条根历史。

## 3.2 Branch 拥有自己的 History

分支不复制父历史，而是**引用父 History 和分叉时间**（`parent_history + parent_at`）。

## 3.3 Event 与 State Change 都归属于 History

因此**分支可以拥有与父地图完全不同的事件和状态变化**（与 ADR-001 §26 架空分支的要求一致）。

## 3.4 History 统一不意味着历史合并

不引入 Merge、冲突解决、Rebase 等概念。Merge 禁令维持 ADR-001 §16 不变。

---

# 4. 裁决理由

- **重建算法统一**：Map 和 Branch 不需要维护两套不同的历史计算机制。
- **分支继承自然**：Branch 通过父 History 和分叉时间继承历史，不需要复制历史数据。
- **历史归属清晰**：每条 State Change 只属于一个 History，分支自己的变化不会污染父历史。
- **后续扩展简单**：多层分支（Branch of Branch）仍然是同一种结构，不需要为它设计另一套模型。

---

# 5. 关键边界：History 不是认知时间的替代品

> **History 是世界创作历史的容器，不是系统认知时间（Transaction Time）的替代品。**

同一段世界历史可能存在：

- 不同资料的认知差异；
- 路线 A / 路线 B 的备选说法；
- 权威记载与猜测并存。

这些**认知层面**的表达需求：

> **不能因为采用了统一 History，就被自动解释为 Branch。**

Branch 的唯一语义是"另一条世界历史"（ADR-001 §11.1）。认知表达问题（资料来源、可信度、备选说法标注等）属于**另一个需要单独保留的领域问题**：

- 本裁决**不解决**它；
- 本裁决**不排除**它；
- 它**不得反向侵入** History / Branch 的结构定义。

未来若引入认知层（资料来源、可信度标注、备选说法等），必须以独立机制建模，并另立 ADR。

---

# 6. 裁决范围

| 事项 | 裁定 |
| --- | --- |
| 统一 History 实体 | **确认** |
| Map 根 History | 确认 |
| Branch 自有 History + parent / parent_at | 确认 |
| Event / State Change 归属 History | 确认 |
| 引入 Merge | 否（维持 ADR-001 禁令） |
| Shared Object / Map-local 合成规则（P2-01 提案 A10） | **本裁决不涉及**；P2-08 专门设计，必要时另立新 ADR |
| 认知时间 / 资料认知表达 | 本裁决不涉及；留作独立领域问题，未来另立 ADR |

---

# 7. 冻结结论

> **Map 和 Branch 通过同一种 History 实体拥有各自的历史；每条历史变更只属于一个 History；分支引用父历史而不复制；统一 History 永远不等于合并历史。**
>
> **History 只承载世界创作历史；认知层面的表达（资料、可信度、备选说法）不属于本结构。**
