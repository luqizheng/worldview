<!-- P2-01-D01-NAV-START -->
> **P2-01-D01 拆分文件** ｜ 原章节：第 14 章　与现有文档的冲突处理 ｜ 索引：[README.md](README.md) ｜ 元数据：[00-document-metadata.md](00-document-metadata.md) ｜ 状态：Proposed / Discussion（未冻结，不构成实施授权）
<!-- P2-01-D01-NAV-END -->

# 14. 与现有文档的冲突处理

后续修订 P2-01 时，需要特别检查：

1. P2-01 V0.1 中 State Change 独立持久化模型是否仍然适用。
2. A12 中 destroy 后再变更直接报错的旧规则，是否与用户已确认的删除后局部修改持续警告规则冲突。
3. 原有 Branch 重建规则是否需要适配动态继承。
4. 原有持续 Command 的执行模型是否符合暂停期间不消耗行动时间的要求。
5. 原有错误处理机制是否需要改为 Fail-Fast。
6. 原有 World State 纯函数重建模型是否需要明确缓存和 Checkpoint 的非权威地位。
7. 原有时间模型是否与整数天数 + 当天时间偏移一致。
8. 原有 Storage 模型是否需要调整 Command、Order Constraint、Calendar Configuration 和派生缓存的职责边界。
9. 原有 Branch 重建与缓存模型是否需要适配继承路径版本、活动路径优先重建、Checkpoint 引用保护与淘汰机制（D01-84~94）。
10. 原 Storage 模型是否需要适配 Checkpoint 的原子发布、完成标记、`md5.txt` 校验文件与批量重建任务持久化的存储职责（D01-114/138~142）。

特别说明：

本轮讨论并不意味着可以直接删除原 P2-01 文档中的全部设计。必须先逐项核对，确认冲突、保留兼容部分，再修订。

---

