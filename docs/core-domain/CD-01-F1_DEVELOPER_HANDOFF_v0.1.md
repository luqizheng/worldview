# CD-01-F1 — Developer Handoff V0.1

- **Date:** 2026-11-22
- **Companion:** [CD-01-F1 Verification Report V0.1](CD-01-F1_VERIFICATION_REPORT_v0.1.md)
- **Commits:** 修复 `4b0c322`（基线 `0d13b17`）

---

## 1. 最终架构：统一连接访问协调器

```
ProjectSession
 └── Database = SqlitePackageDatabase
      └── SqliteConnectionAccess（唯一协调器：1 连接 = 1 gate = 1 disposed 状态）
           ├── SqlitePackageDatabase 全部操作（元数据/ledger/quick_check/迁移事务/Dispose）
           ├── SqliteCoreDomainDatabase（任意数量实例——共享同一协调器）
           │    └── InSnapshotScope：多步读的原子快照（Monitor 同线程可重入）
           └── 测试数据注入（InternalsVisibleTo）
```

不变式：

1. **任何**对底层 `SqliteConnection` 的使用都在 `SqliteConnectionAccess.Execute` 的临界区内**完整**执行（含事务 Begin/Commit/Rollback 与读取器物化）。
2. 一个物理连接只有一个协调器；绕过它直接持有连接 = 放弃协调 = 禁止。
3. Dispose 与在飞操作安全汇合：在飞的完成，之后的入口拒绝（`ObjectDisposedException`，ObjectName=`SqliteConnectionAccess`）。

## 2. 运行约束（红线）

| 约束 | 说明 |
|---|---|
| **新适配器必须经协调器** | 拿 `IPackageDatabase` → 构造 `SqliteCoreDomainDatabase(database)`（构造器已内建拒绝替身）；需要原始 SQL（仅测试/工具）→ `((ISqliteConnectionProvider)database).GetAccess().Execute(...)`。**绝不缓存 `GetAccess()` 返回连接以外的裸连接**（新接口已不提供裸连接）。 |
| **事务不得越出临界区** | `SqliteTransaction` 的生命周期必须整体在一个 `Execute` 回调内。禁止把事务对象存字段/跨 await 持有。 |
| **多步复合写** | V1 append-only 下 `MapService`/`BranchService` 的读-校验-写可直接用；一旦引入删除/更新/改名，必须包入 `InSnapshotScope` 或专用事务边界（见 Verification Report §15 条件 1）。 |
| **长事务** | 协调器串行——长事务阻塞同会话所有数据库操作（含 Dispose）。批量任务按任务粒度拆事务。 |
| **连接策略不变** | Pool=False、`foreign_keys=ON`、`busy_timeout=5000`、journal 默认（WAL 属 open gate，勿自行开启）。 |
| **同文件多连接** | 允许（如 Create 流程短连接），由 SQLite 文件锁 + ledger PK fail-closed 兜底；会话内不要主动开第二连接。 |

## 3. 关键实现位置

| 内容 | 文件 |
|---|---|
| 协调器本体（Open/Execute/DisposeAsync） | `WorldView/src/WorldView.Infrastructure/Persistence/SqliteConnectionAccess.cs` |
| 包数据库接入协调器 + 迁移事务入临界区 | `WorldView/src/WorldView.Infrastructure/Persistence/SqlitePackageDatabase.cs` |
| Core DB 委托协调器 + `InSnapshotScope` | `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.cs` |
| 内部接口 `ISqliteConnectionProvider.GetAccess()` | 同上（文件顶部） |
| Workspace 解析原子快照 | `CoreDomainAdapters.cs` → `CoreDomainWorkspaceContextResolver.ResolveAsync` |
| 世系初始一致性校验 | `WorldView/src/WorldView.Domain/Maps/BranchLineage.cs` → `BranchLineageResolver.TryResolve` 开头 |
| 并发/事务/Dispose/迁移测试 | `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/PersistenceConcurrencyTests.cs`（16 条） |
| 隔离/世系/Schema 负向测试 | `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/PersistenceDomainIntegrityTests.cs`（15 条） |

## 4. 行为变化速览（对 CD-01）

- 并发读写同一 `ProjectSession.Database`：从"潜伏竞争（双写 ≈50% 失败）"变为串行安全（ Barrier 对齐测试 172/172 通过）。
- `ExecuteMigrationAsync`：事务从锁外改为整体入协调器——同库并发操作不再可能切入迁移事务。
- 会话关闭后调用适配器：得到**协调器入口**的 `ObjectDisposedException`（ObjectName=`SqliteConnectionAccess`），不再是原生 `SQLitePCL.sqlite3` 失控异常。上层捕获请按 `ObjectDisposedException` 分类处理。
- `BranchLineageResolver`：新增初始 Branch 自有历史校验（缺失/非 branch_owned → 拒绝）。此前只有外部植入数据可触发，正常服务路径行为不变。
- 公开 API 零变化：`IPackageDatabase`/`ICoreDomainDatabase`/适配器构造签名/组合根接线均不动。

## 5. 测试编写指引

- 并发测试用 `Barrier` 对齐起跑、循环内捕获异常后断言"结果类别"（成功 / 定义异常），不依赖 Sleep。
- 验证"无部分写入"的惯用法：同快照内断言成对计数（如 branch_owned history 数 == branch 数）。
- 植入腐败数据两条通道：协调器通道（FK 开）造"服务不可达但 schema 合法"的行；`Foreign Keys=False` 裸连接造悬空行（模拟外部工具）。
- 原始插值字符串（`$"""`）内不能内联连续大括号字面量——用常量（见 `JsonEmpty`）。

## 6. 后续开发注意事项

1. **PRA-01 不受影响也不被提前实施**：渲染/回放层仍只经 Domain 服务；Renderer 永不触数据库（AF-01 §7.2 红线延续）。
2. **P2-09-12 Writer Coordinator 正式化**时，`SqliteConnectionAccess` 即进程内实现基线；将其纳入该证明文档的引用实现。
3. 后台任务（自动保存、批量修复 Undo/Redo）接入时：走既有端口 + 协调器即可获得串行化；评估其事务粒度避免长临界区。
4. 新增迁移照旧：写进 `CoreDomainSchema.Migrations`（依赖序），checksum 自动 SHA-256；并发/回滚语义已由本任务测试锁定。
5. 失败迁移的修复路径已被验证：ledger/无残留、连接可继续用、正式迁移可随后补齐——排障时优先核对 ledger 而非猜文件状态。
