# World View — 技术设计提案索引

- **状态**：全部文件为 **Proposed / 待人工确认**。确认前不得据此实现。
- **性质**：栈中立技术设计。不含语言/框架决定；存储引擎以关系模型表述，
  具体引擎（建议 SQLite）随主栈裁决一并确认。
- **与冻结文档的关系**：只做"冻结架构之下的技术设计"，不得与
  ADR-001~005 / Baseline INV-01~16 / P2-01 冲突；如发现冲突，提新 ADR。

| # | 文档 | 内容 | 对应既定待设计项 |
| --- | --- | --- | --- |
| TD-01 | [01-storage-persistence.md](01-storage-persistence.md) | 持久化模型、表结构、迁移、事务边界（P2-09 方向） | P2-09；P2-01 §9-2 |
| TD-02 | [02-task-engine.md](02-task-engine.md) | 任务引擎：任务模型、生命周期、组件职责、暂停/取消/失败/恢复、执行记录 | sprint Phase 1 |
| TD-03 | [03-generation-candidate-changeset-pipeline.md](03-generation-candidate-changeset-pipeline.md) | 生成 Spec/Session/Task/Candidate、验证、Change Set、应用服务层与 API/MCP 形态 | sprint Phase 2~5；Baseline AI 约束 |
