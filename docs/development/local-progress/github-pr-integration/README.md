# GitHub PR Integration — 任务索引

本目录统一记录 WorldView 本地开发任务、测试结果与遗留问题。任务报告随代码一起提交。

| 任务 | 标题 | 状态 | 报告 |
|---|---|---|---|
| Task 002 | EF Core V1 Asset / Geometry Invariant Port | 已完成（本地 Windows Debug/Release + 四平台 CI 全部通过；PR #22 Draft） | [002-efcore-v1-asset-geometry-invariant-port.md](002-efcore-v1-asset-geometry-invariant-port.md) |

## 约定

- 报告命名：`<三位任务号>-<短横线标题>.md`。
- 每个报告必须记录：迁移的领域规则、旧 PR 对应关系、改动文件、Migration、测试覆盖、Windows/macOS 结果、已知问题、未决产品决策与后续建议。
- 所有持久化集成测试使用真实 SQLite；不使用 InMemory Provider 替代。
- 报告中的 CI 结果必须对应新提交 SHA，不得引用旧 SHA。
