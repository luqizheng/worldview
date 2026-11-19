# P2-01-D01 讨论记录 — 总索引（README）

**项目：** World View
**阶段：** Phase 2
**议题：** P2-01 Domain Model
**讨论记录：** P2-01-D01 — Command、State Change 与历史重建机制讨论记录
**当前版本：** V0.5（结构版本 V0.5-split-1，2026-11-19 由单文件拆分而来）
**当前状态：** Proposed / Discussion —— 待正式审查与冻结，**未冻结，不构成开发实施授权**
**当前记录范围：** D01-01 至 D01-143
**元数据与修订历史：** [00-document-metadata.md](00-document-metadata.md)
**原始单文件归档：** [archive/phase2-p2-01-d01-discussion-record-v0.5.md](archive/phase2-p2-01-d01-discussion-record-v0.5.md)

---

## 1. 文件目录

| 编号 | 文件 | 原章节 | 主要内容 | 对应 D01 |
|---|---|---|---|---|
| README | [README.md](README.md) | — | 总索引（本文件）：文件目录、D01 编号索引、专题关联、状态说明 | — |
| 00 | [00-document-metadata.md](00-document-metadata.md) | 首页元数据 | 版本信息、修订历史 V0.1~V0.5（唯一维护处）、拆分记录、使用规则与冻结边界 | — |
| 01 | [01-document-purpose-and-rules.md](01-document-purpose-and-rules.md) | 第 1 章 | 文档目的与使用规则、裁决状态、核心产品定位 | — |
| 02 | [02-command-and-state-change.md](02-command-and-state-change.md) | 第 2 章 | Command / State Change / World State 基本关系与生成方式 | D01-01~04 |
| 03 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 第 3 章 | 执行条件失效与恢复、旧警告、修改已有 Command、Branch 影响 | D01-05~10 |
| 04 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 第 4 章 | 依赖关系、路线确定与修改、历史时间修改、删除与插入 | D01-11~16 |
| 05 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 第 5 章 | 同一历史时间顺序规则、时间精度、顺序约束冲突与循环 | D01-17~28 |
| 06 | [06-combination-map.md](06-combination-map.md) | 第 6 章 | 组合地图机械规则（整合语义已被 D01-74 修正，章内已标注） | D01-29~36 |
| 07 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 第 7 章 | 内部时间模型、第 0 天、日历显示与改革 | D01-37~49 |
| 08 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 第 8 章 | 持续 Command 暂停、执行障碍、路线修复、修复建议范围 | D01-50~57 |
| 09 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 第 9 章 | 编译器式 Fail-Fast 重建、播放规则、重建状态三态 | D01-58~64 |
| 10 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | 第 10 章 | World State 缓存与 Checkpoint 基础规则（派生缓存定位） | D01-65~69 |
| 11 | [11-creation-project-and-published-work.md](11-creation-project-and-published-work.md) | 第 11 章 | 创作工程与发布作品分离 | — |
| 12 | [12-unified-architecture-understanding.md](12-unified-architecture-understanding.md) | 第 12 章 | 统一架构认识摘要（12.1 权威/派生表、工作流程、12.5~12.7 增补摘要） | — |
| 13 | [13-known-pending-review-items.md](13-known-pending-review-items.md) | 第 13 章 | 已知待审查事项登记（13.1~13.8，含 D01-77 未决项） | — |
| 14 | [14-conflict-handling-with-existing-documents.md](14-conflict-handling-with-existing-documents.md) | 第 14 章 | 与现有文档（P2-01 V0.1、ADR 等）的冲突核对清单 | — |
| 15 | [15-current-interim-conclusions.md](15-current-interim-conclusions.md) | 第 15 章 | 当前阶段性结论汇总（含 V0.4 / V0.5 增补块） | — |
| 16 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | 第 16 章 | Shared Object、Branch 动态继承、Checkpoint 归属、地图整合（含 16.8 冲突检查、16.9 遗留） | D01-70~75 |
| 17 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 第 17 章 | 继承路径重建、Checkpoint 管理、失效传播、编辑权限（含 17.26 汇总表、17.27 冲突检查） | D01-76~100 |
| 18 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 第 18 章 | Checkpoint 专题：继承、有效性、损坏检测、批量重建、单机应用、完成标记与 MD5、保存内容（含 18.44/18.45） | D01-101~143 |
| 归档 | [archive/phase2-p2-01-d01-discussion-record-v0.5.md](archive/phase2-p2-01-d01-discussion-record-v0.5.md) | 全文 | V0.5 单文件逐字节归档（拆分前原样） | D01-01~143 |

---

## 2. D01 裁决编号索引

**分类说明（先读）：**

- 下表 143 条全部为**用户已确认的阶段性裁决**（用户选择 / 用户明确确认 / 用户补充机制 / 用户澄清），正文权威位置在"所在文件"列指向的章节文件内，表中文本仅为标题摘要。
- **待审查事项**（非裁决）集中登记于：第 13 章（13.1~13.8）、16.9、18.45；见下方"待审查事项与助手核查位置索引"。
- **助手核查意见**（结论供用户审查，本身不构成裁决）：16.8 冲突检查表、17.27 冲突检查表、18.44 冲突检查表、第 12/15 章的汇总表述。
- **不得仅按编号连续性推断裁决内容**——特殊条目见"备注"列及文末特殊说明。

| D01 编号 | 原章节 | 所在文件 | 条目标题 | 备注 |
|---|---|---|---|---|
<!-- D01-INDEX-ROWS -->
| D01-01 | 2.1 | [02-command-and-state-change.md](02-command-and-state-change.md) | 基本关系 |  |
| D01-02 | 2.2 | [02-command-and-state-change.md](02-command-and-state-change.md) | State Change 的生成方式 |  |
| D01-03 | 2.3 | [02-command-and-state-change.md](02-command-and-state-change.md) | 持续 Command |  |
| D01-04 | 2.4 | [02-command-and-state-change.md](02-command-and-state-change.md) | 派生结果重建一致性 |  |
| D01-05 | 3.1 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 执行条件失效 |  |
| D01-06 | 3.2 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 执行条件恢复 |  |
| D01-07 | 3.3 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 历史修改后的旧警告 |  |
| D01-08 | 3.4 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 修改已有 Command |  |
| D01-09 | 3.5 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | 父地图 Command 修改与 Branch |  |
| D01-10 | 3.6 | [03-command-execution-failure-and-user-intent.md](03-command-execution-failure-and-user-intent.md) | Branch 后续 Command 执行失败 |  |
| D01-11 | 4.1 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | Command 依赖关系 |  |
| D01-12 | 4.2 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 移动路线确定方式 |  |
| D01-13 | 4.3 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 修改已指定路线 |  |
| D01-14 | 4.4 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 修改 Command 历史时间 |  |
| D01-15 | 4.5 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 删除 Command |  |
| D01-16 | 4.6 | [04-command-dependencies-and-routes.md](04-command-dependencies-and-routes.md) | 任意历史时间插入 Command |  |
| D01-17 | 5.1 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 同一时间多个 Command 的顺序 |  |
| D01-18 | 5.2 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 自动排序原则 |  |
| D01-19 | 5.3 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 顺序歧义处理 |  |
| D01-20 | 5.4 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 用户指定顺序的保存 |  |
| D01-21 | 5.5 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 时间精度模型 |  |
| D01-22 | 5.6 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 不同时间精度的排序 |  |
| D01-23 | 5.7 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 时间修改导致顺序约束冲突 |  |
| D01-24 | 5.8 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 循环顺序约束 |  |
| D01-25 | 5.9 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 删除顺序约束 |  |
| D01-26 | 5.10 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | Branch 继承顺序约束 |  |
| D01-27 | 5.11 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | 父地图约束与 Branch 自有约束冲突 |  |
| D01-28 | 5.12 | [05-ordering-rules-for-same-historical-time.md](05-ordering-rules-for-same-historical-time.md) | Branch 是否允许覆盖父地图顺序约束 |  |
| D01-29 | 6.1 | [06-combination-map.md](06-combination-map.md) | 组合地图的历史来源 |  |
| D01-30 | 6.2 | [06-combination-map.md](06-combination-map.md) | 父地图分支后继续发展 |  |
| D01-31 | 6.3 | [06-combination-map.md](06-combination-map.md) | 组合地图的历史冲突 |  |
| D01-32 | 6.4 | [06-combination-map.md](06-combination-map.md) | 组合地图是否保留 Branch 关系 |  |
| D01-33 | 6.5 | [06-combination-map.md](06-combination-map.md) | 组合地图中的 Command ID |  |
| D01-34 | 6.6 | [06-combination-map.md](06-combination-map.md) | 组合地图中的顺序约束 ID |  |
| D01-35 | 6.7 | [06-combination-map.md](06-combination-map.md) | 组合时依赖关系的复制 |  |
| D01-36 | 6.8 | [06-combination-map.md](06-combination-map.md) | 组合地图中的执行依据 |  |
| D01-37 | 7.1 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 内部时间模型 |  |
| D01-38 | 7.2 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 地图第 0 天 |  |
| D01-39 | 7.3 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | Branch 时间轴 |  |
| D01-40 | 7.4 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 日历显示规则 |  |
| D01-41 | 7.5 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 用户自定义日历 |  |
| D01-42 | 7.6 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 默认日历按历史时间分段生效 |  |
| D01-43 | 7.7 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 日历改革与 Command |  |
| D01-44 | 7.8 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 删除日历改革 Command |  |
| D01-45 | 7.9 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 日历改革追溯生效 |  |
| D01-46 | 7.10 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 内部时间数值表达 |  |
| D01-47 | 7.11 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | Command 的时间表达 |  |
| D01-48 | 7.12 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 持续 Command 暂停时的结束时间 |  |
| D01-49 | 7.13 | [07-historical-time-and-calendar.md](07-historical-time-and-calendar.md) | 持续 Command 暂停期间的历史时间 |  |
| D01-50 | 8.1 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 暂停时目标对象的状态 |  |
| D01-51 | 8.2 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 单条 Command 遇到执行障碍 |  |
| D01-52 | 8.3 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 恢复后原路线失效 |  |
| D01-53 | 8.4 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 撤销原问题 | 原问题已撤销（不保留原选择概念） |
| D01-54 | 8.5 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 冲突修复后的后续历史 |  |
| D01-55 | 8.6 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 后续 Command 执行失败后的诊断 |  |
| D01-56 | 8.7 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 修复建议的范围 |  |
| D01-57 | 8.8 | [08-durable-commands-pause-and-route-repair.md](08-durable-commands-pause-and-route-repair.md) | 修改 Command 后仍然失败 |  |
| D01-58 | 9.1 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 执行失败后的时间线处理 |  |
| D01-59 | 9.2 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 遇到第一个错误后的检查策略 |  |
| D01-60 | 9.3 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 修复后的重建起点 |  |
| D01-61 | 9.4 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 播放条遇到历史错误 |  |
| D01-62 | 9.5 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | Command 修改后的重建触发 |  |
| D01-63 | 9.6 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 未重建修改期间的播放 |  |
| D01-64 | 9.7 | [09-compiler-style-history-rebuild.md](09-compiler-style-history-rebuild.md) | 重建状态 |  |
| D01-65 | 10.1 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | 重建结果是否保存 | 最终修正选择 B（由 A 改为 B） |
| D01-66 | 10.2 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | Checkpoint 检查点机制 |  |
| D01-67 | 10.3 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | Checkpoint 建立位置 |  |
| D01-68 | 10.4 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | 失效 Checkpoint 的处理 |  |
| D01-69 | 10.5 | [10-world-state-cache-and-checkpoint.md](10-world-state-cache-and-checkpoint.md) | Checkpoint 保存范围 |  |
| D01-70 | 16.1 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | Shared Object 与 Branch Local Command 的关系 |  |
| D01-71 | 16.2 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | Branch 动态继承模型 | 含闭区间边界澄清（≤分叉点，用户裁决） |
| D01-72 | 16.3 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | Branch Checkpoint 归属与保存范围 |  |
| D01-73 | 16.4 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | 删除对象后的局部修改规则适用于 Branch |  |
| D01-74 | 16.5 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | 地图整合采用历史路径固化 |  |
| D01-75 | 16.6 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) | Branch 整合后的独立性 |  |
| D01-76 | 17.1 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Shared Object 与 Branch 历史重建关系 | 含用户后续澄清（与选择共同构成完整裁决） |
| D01-77 | 17.2 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Checkpoint 继承与复用 | 重拟 |
| D01-78 | 17.3 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 活动 Branch 切换 |  |
| D01-79 | 17.4 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 祖先修改后的 Checkpoint 失效传播 |  |
| D01-80 | 17.5 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 祖先修改导致后代 Command 执行失败 |  |
| D01-81 | 17.6 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 重建失败与继承阻断的区别 |  |
| D01-82 | 17.7 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 祖先修复后的自动恢复 | 与 D01-94 合并理解已明确（V0.4.1） |
| D01-83 | 17.8 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 失败 Branch 对其他 Branch 的影响范围 |  |
| D01-84 | 17.9 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 多 Branch 重建调度 | 与 D01-94 触发/调度边界已明确（V0.4.1） |
| D01-85 | 17.10 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 重建期间祖先 Command 再次修改 |  |
| D01-86 | 17.11 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Branch 继承路径版本 |  |
| D01-87 | 17.12 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 重建任务版本校验 |  |
| D01-88 | 17.13 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Checkpoint 版本有效性 |  |
| D01-89 | 17.14 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Checkpoint 数量和保存位置 |  |
| D01-90 | 17.15 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 祖先 Checkpoint 淘汰策略 |  |
| D01-91 | 17.16 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 正在使用的 Checkpoint |  |
| D01-92 | 17.17 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | Checkpoint 逻辑失效与物理淘汰 | 与 D01-68 生命周期关系已明确（V0.4.1） |
| D01-93 | 17.18 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 旧重建任务撤销 |  |
| D01-94 | 17.19 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 全局失效、活动路径优先重建 | 用户补充确认机制（非 A/B/C 选择题） |
| D01-95 | 17.20 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 切换到非活动 Branch |  |
| D01-96 | 17.21 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 活动 Branch 重建期间的编辑权限 |  |
| D01-97 | 17.22 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 重建失败后的修复模式 |  |
| D01-98 | 17.23 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 失败点之前的 Command 修改范围 |  |
| D01-99 | 17.24 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 修复期间失败点之后的 Command 编辑权限 |  |
| D01-100 | 17.25 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) | 继承阻断期间后代 Branch 的修复权限 |  |
| D01-101 | 18.1 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | Checkpoint 基本定位 |  |
| D01-102 | 18.2 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | Checkpoint 唯一性 |  |
| D01-103 | 18.3 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | Checkpoint 按 Branch 归属 |  |
| D01-104 | 18.4 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 后代复用祖先分叉点 Checkpoint |  |
| D01-105 | 18.5 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 恢复后继续执行自有 Command |  |
| D01-106 | 18.6 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 完整继承路径版本校验 |  |
| D01-107 | 18.7 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 祖先历史变化的失效传播 |  |
| D01-108 | 18.8 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 活动路径优先重建 |  |
| D01-109 | 18.9 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 创建的唯一性约束 |  |
| D01-110 | 18.10 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 同一 Checkpoint 单一有效生成任务 |  |
| D01-111 | 18.11 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 生成失败不发布 |  |
| D01-112 | 18.12 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 生成中版本变化即失效 |  |
| D01-113 | 18.13 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 发布前完整版本校验 |  |
| D01-114 | 18.14 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 原子发布与不完整数据 |  |
| D01-115 | 18.15 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 恢复前完整性检查 |  |
| D01-116 | 18.16 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 单个损坏只影响自身 |  |
| D01-117 | 18.17 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 祖先损坏不连带后代失效 |  |
| D01-118 | 18.18 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 活动 Branch 损坏自动恢复 |  |
| D01-119 | 18.19 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 非活动 Branch 损坏按需处理 |  |
| D01-120 | 18.20 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 不建复杂损坏历史记录 |  |
| D01-121 | 18.21 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 通知时机 |  |
| D01-122 | 18.22 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 损坏处理小结 | 小结性条目（归纳 115~121，未引入新机制） |
| D01-123 | 18.23 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 不提供单个 Checkpoint 的手动删除 |  |
| D01-124 | 18.24 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 用户可要求重新创建全部 Checkpoint |  |
| D01-125 | 18.25 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 全部重建的覆盖范围 |  |
| D01-126 | 18.26 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 祖先到后代顺序执行 |  |
| D01-127 | 18.27 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量重建期间的地图级锁定 |  |
| D01-128 | 18.28 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量重建模式一：继续/修复重建 |  |
| D01-129 | 18.29 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量重建模式二：强制全部重建 |  |
| D01-130 | 18.30 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 两种模式的边界 |  |
| D01-131 | 18.31 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量重建失败的处理 |  |
| D01-132 | 18.32 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量任务取消 |  |
| D01-133 | 18.33 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量任务再次启动 |  |
| D01-134 | 18.34 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量重建期间祖先历史再次变化 |  |
| D01-135 | 18.35 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量任务目标范围启动时固定 |  |
| D01-136 | 18.36 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 不得通过切换 Branch 绕过锁定 |  |
| D01-137 | 18.37 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 批量任务进度与错误可查看 |  |
| D01-138 | 18.38 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 单机应用定位与关闭行为 |  |
| D01-139 | 18.39 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 轻量级任务持久化与完成标记 |  |
| D01-140 | 18.40 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 启动时的校验规则 |  |
| D01-141 | 18.41 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | 完成标记的职责 |  |
| D01-142 | 18.42 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | MD5 校验 |  |
| D01-143 | 18.43 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) | Checkpoint 保存内容与边界 |  |

### 特殊条目与整体性说明

- **D01-125~137（批量重建与地图级锁定）**：该区间内"规则 → 编号"的逐号映射系 V0.5 落档时按任务给定顺序整理（助手执行），**尚未经用户逐号核对**，已登记为待修复事项（见第 4 节）。本次拆分原样保留全部内容，未做任何调整。
- **D01-77 为重拟条目**：旧议题已撤回，撤回内容不构成裁决、不得恢复；本条以重拟后议题为准（见 [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) 17.2）。其示例的精确恢复策略仍未决（第 13 章 13.7 第 8 项）。
- **D01-94 / D01-138 不是 A/B/C 选择题**（D01-94 为用户补充确认的完整机制；D01-101~143 全轮未采用选择题形式，以"用户确认"记录）。
- **D01-29 已被 D01-74 修正、D01-30 部分修正**（第 6 章内原文保留并标注，不得恢复旧规则）；D01-31~36 兼容规则继续适用。
- **D01-53 为用户撤销条目**：撤销原问题，不保留"105 天确认"等原概念。
- **D01-65 为最终修正条目**：由选择 A 改为 B（允许缓存，缓存非权威）。
- **D01-68 / D01-82 / D01-84 / D01-92** 与相邻裁决的关系已在 V0.4.1 明确（生命周期阶段 / 触发与调度两个概念），细节见对应文件衔接说明。
- **D01-143** 为 Checkpoint 保存内容与边界的完整确认，不得删减（保存完整合成 World State + 不保存清单）。

### 待审查事项与助手核查位置索引

| 位置 | 类别 | 内容 | 所在文件 |
|---|---|---|---|
| 13.1~13.8 | 待审查事项 | 各专题机制方向已定、细节待技术设计或待用户裁决的事项登记（含 13.7 第 8 项 D01-77 未决） | [13-known-pending-review-items.md](13-known-pending-review-items.md) |
| 16.8 | 助手核查 | D01-70~75 与既有记录及已冻结 ADR 的一致性核查表 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) |
| 16.9 | 待审查事项 | D01-70~75 轮遗留事项 | [16-follow-up-review-d01-70-75.md](16-follow-up-review-d01-70-75.md) |
| 17.26 | 裁决汇总（用户确认） | 重建结果状态与 Branch 编辑权限两维度总结表（17.26.1 / 17.26.2） | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) |
| 17.27 | 助手核查 | D01-76~100 与既有记录的一致性核查表 | [17-follow-up-discussion-d01-76-100.md](17-follow-up-discussion-d01-76-100.md) |
| 18.44 | 助手核查 | D01-101~143 与既有记录及已冻结 ADR 的一致性核查表 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) |
| 18.45 | 待审查事项 | Checkpoint 专题遗留的待技术设计事项 | [18-checkpoint-topic-d01-101-143.md](18-checkpoint-topic-d01-101-143.md) |
| 第 14 章 | 核对要求 | 修订 P2-01 前必须逐项核对的冲突清单（含 Storage 模型适配） | [14-conflict-handling-with-existing-documents.md](14-conflict-handling-with-existing-documents.md) |
| 第 15 章 | 阶段性结论（含汇总表述） | 结论汇总与 V0.4/V0.5 增补块 | [15-current-interim-conclusions.md](15-current-interim-conclusions.md) |

---

## 3. 专题关联索引

> 本节只负责跨文件导航，不建立任何新的架构结论。同一专题散布于多个章节时按历史演进顺序列出。

| 专题 | 相关文件与章节 |
|---|---|
| Command 与 State Change | [02](02-command-and-state-change.md)（D01-01~04 基本关系与生成方式）；定位汇总见 [12](12-unified-architecture-understanding.md) 12.1 |
| World State | [09](09-compiler-style-history-rebuild.md)（D01-58~64 重建与播放）；[10](10-world-state-cache-and-checkpoint.md)（D01-65~69 缓存）；[18](18-checkpoint-topic-d01-101-143.md) D01-143（保存内容边界） |
| Branch 与历史继承 | [16](16-follow-up-review-d01-70-75.md)（D01-71 闭区间动态继承、D01-73 删除后局部修改）；[17](17-follow-up-discussion-d01-76-100.md)（D01-76/78/86 继承路径重建与版本）；摘要见 [12](12-unified-architecture-understanding.md) 12.5 |
| Shared Object | [16](16-follow-up-review-d01-70-75.md) 16.1（D01-70）；[17](17-follow-up-discussion-d01-76-100.md) 17.1（D01-76 澄清）；[18](18-checkpoint-topic-d01-101-143.md) D01-143（Checkpoint 中的有效状态） |
| 历史重建 | [09](09-compiler-style-history-rebuild.md)（Fail-Fast、播放/重建起点）；[17](17-follow-up-discussion-d01-76-100.md)（D01-82/83/94 恢复传播与调度）；[18](18-checkpoint-topic-d01-101-143.md)（D01-108/118/119 自动与按需重建） |
| Checkpoint | [10](10-world-state-cache-and-checkpoint.md)（D01-65~69 基础）；[16](16-follow-up-review-d01-70-75.md) 16.3（D01-72 归属与保存范围）；[17](17-follow-up-discussion-d01-76-100.md)（D01-77/88/89~92 复用、版本、引用与淘汰）；[18](18-checkpoint-topic-d01-101-143.md)（D01-101~143 全专题） |
| 重建失败与继承阻断 | [17](17-follow-up-discussion-d01-76-100.md)（D01-81/83 区分、D01-96~100 编辑权限、17.26 汇总表）；[18](18-checkpoint-topic-d01-101-143.md) D01-131（批量场景沿用） |
| 地图级批量重建 | [18](18-checkpoint-topic-d01-101-143.md)（D01-123~137：全部重建、祖先到后代顺序、地图级锁定、两种模式、范围固定） |
| 单机应用关闭与重启恢复 | [18](18-checkpoint-topic-d01-101-143.md)（D01-138~140 单机定位与启动校验；D01-141/142 完成标记与 MD5） |

---

## 4. 待修复与遗留问题清单

以下问题在拆分时已存在，**本次拆分未做任何修正**，原样保留并登记：

1. **D01-125~137 编号与内容映射待用户核对**（登记待修复）：区间内逐号映射系 V0.5 落档时按任务给定顺序整理（助手执行），未经用户逐号确认。修正须待用户核对后另行执行，不得与结构调整混同。
2. **D01-77 示例的精确恢复策略未决**（13.7 第 8 项）：后代 Checkpoint（如 B@1500）本身有效时可否不经祖先起点回放直接恢复，尚未裁决。
3. **Checkpoint 专题技术设计事项**（13.8，7 项）：完成标记格式、md5.txt 布局、任务持久化结构、批量重建进度呈现、模式选择交互、Checkpoint 内部格式、批量与日常重建衔接。
4. **原单文件保留位置待决**：`docs/phase2-p2-01-d01-discussion-record-v0.1.md`（V0.5 快照）按任务要求原样保留；此后修订发生在本目录，原文件将逐渐过时，是否移除或加"已拆分"标注由用户决定。
5. **复合章节引用未链接化**：形如"第 13.1 / 13.5 节"的复合引用及裸编号引用（如"见 17.19"、"9.7"）保持纯文本，未转换为链接（避免误伤版本号等相似文本）；可经本 README 的章节映射人工定位。

---

## 5. 文档状态说明

1. **README 是导航入口，不是新的裁决权威**；各专题文件保存对应章节的原始讨论内容。
2. **如果 README 摘要与专题原文不一致，以专题原文为准**，并登记索引错误（登记位置：本节及 00 元数据文件）。
3. 所有拆分文件共同构成完整的 P2-01-D01 讨论记录；每条 D01 裁决只有一个权威正文位置。
4. 拆分为纯文档结构调整（结构版本 V0.5-split-1），**不是架构裁决更新**；不修改代码，不新增架构裁决。
5. 全部文件状态为 **Proposed / Discussion**：未冻结、未签发 ADR、不构成开发实施授权；正式冻结须待 D01 整体正式审查并由用户明确批准。
