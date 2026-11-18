# World View — Branch 历史分叉规则裁决 V1.0（A5 正式化）

- **文档状态**：Accepted / Frozen
- **版本**：V1.0
- **日期**：2026-11-18
- **裁决来源**：2026-11-18 人工确认（P2-01 提案歧义项 A5 正式化；时间约束此前已在 ADR-003 讨论中确认，本文件给出完整规则集）
- **性质**：架构裁决（ADR）
- **适用范围**：Branch 的创建、继承、分叉时间窗、删除与废弃
- **上游文档**：[ADR-001](ADR-001-map-history-and-branch-model.md)、[ADR-002](ADR-002-unified-history-entity.md)、[ADR-003](ADR-003-shared-local-history-command-playback.md)、[ADR-004](ADR-004-batch-repair-undo-redo.md)、[Architecture Baseline V1.0](../architecture-baseline-v1.0.md)、[P2-01 Domain Model Proposal V0.1](../phase2-p2-01-domain-model-proposal-v0.1.md)
- **后续实施依据**：是
- **是否允许实现阶段重新解释本模型**：否；如发现冲突，应提交新的 ADR，不得自行改变本裁决
- **任务边界声明**：本文件为已确认裁决的**正式化记录**，不是重新设计，不实施代码、不修改数据库 Schema、不设计 API。

---

# 1. 裁决摘要

> **Branch 自有 State Change 的有效时间必须严格晚于分叉时间：`effective_time > parent_at`。**
>
> **Branch 是世界历史的分叉，不是传统版本管理分支。**

---

# 2. 分叉时间约束

```text
Branch 自有 State Change：  effective_time > parent_at
```

- **不得等于或早于**分叉时间。
- 推论：想改写分叉点当年（含）以前的历史，正确动作是在**更早时点创建分支**。
- 该约束为写入时校验项（P2-01 §5.2）。

---

# 3. Branch 规则（已确认）

1. Branch 可以在**任意历史时间点**创建。
2. Branch **继承分叉点之前的父历史**。
3. Branch **不复制祖先历史**。
4. 父历史在**分叉点之前**的修改，对 Branch **可见**。
5. 父历史在**分叉点之后**的变化，**不影响** Branch。
6. Branch 自己的 State Change **不影响父历史**。
7. Branch 自有 State Change **不得等于或早于分叉时间**。
8. Branch **可以删除**。
9. Branch 也可以标记为**废弃**；废弃后**保留查看能力**，并**允许恢复**。

---

# 4. 与既有裁决的对应

| 规则 | 既有权威依据 |
| --- | --- |
| 1 任意时点创建 | ADR-001 §15、INV-06；P2-01 §5（`parent_at ≥ map epoch`） |
| 2 继承分叉点前父历史 | ADR-001 §12 / INV-07 |
| 3 不复制祖先历史 | ADR-001 §13 / INV-08；ADR-002 §3.2 |
| 4 分叉点前父修改可见 | ADR-001 §14 / INV-09 |
| 5 分叉点后父变化不影响 | ADR-001 §15 / INV-10 |
| 6 自有变更不影响父 | ADR-001 §19；P2-01 §2.1（历史归属）/ HSE-07 |
| 7 时间约束 | 本裁决 §2；P2-01 §5.2 |
| 8 可删除 | ADR-001 §18.2 / INV-12 / HSE-09 |
| 9 可废弃可恢复 | ADR-001 §18.3 / INV-12 / HSE-10 |

---

# 5. 一致性检查结果

| 对照对象 | 结果 |
| --- | --- |
| ADR-001（分支模型同源） | ✅ 一致，本文件是 §11–19 的规则化重述，无改写 |
| ADR-002（统一 History） | ✅ 一致：Branch 拥有自己的 History；时间窗定义在统一结构之上 |
| ADR-003 V1.1（A10-08 单线播放） | ✅ 一致：Branch 须先被选定 → `t ≤ parent_at` 委托父链重建 → `t > parent_at` 应用自有命令，单序列单一状态 |
| ADR-004（Undo / Redo） | ✅ 一致：对父历史的 Undo 按 INV-09 传播语义影响 Branch；"分支存在是否构成 Undo 冲突因素"仍为待设计项（ADR-004 §8-4） |
| Baseline INV-06 ~ INV-12 | ✅ 一致 |
| P2-01 提案 §5 | ✅ 一致（本文为其 §5.2 的裁决正式化） |

**结论：无冲突。本文件不改变任何既有裁决条文。**

---

# 6. 边界

- Branch 是**世界历史的分叉**，不是版本管理分支（否决项 1–4，见 [rejected-designs.md](rejected-designs.md)）。
- 统一 History 不意味着合并（ADR-002 §3.4）。
- 认知层表达（资料来源、备选路线、权威与猜测）**不得建模为 Branch**（ADR-002 §5）。

---

# 7. 冻结结论

> **分支从任意历史时点分叉，继承此前、独立此后；自有变更必须严格晚于分叉点；可以删除，可以废弃并恢复。它是另一条世界历史，不是版本管理的分支。**
