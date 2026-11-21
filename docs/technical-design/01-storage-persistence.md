# TD-01 — 持久化与存储设计提案

- **状态**：Proposed / 待人工确认
- **版本**：V0.1（2026-11-20）
- **上游**：P2-01 提案、ADR-002（统一 History）、ADR-005（分叉规则）、Baseline §35/§40
- **范围**：关系模型、迁移机制、事务边界、仓储语义。**不含**存储引擎最终选型（建议 SQLite，随主栈裁决）。

---

# 1. 设计原则

1. **存储忠实于领域模型**（Baseline §35）：表结构直接映射 P2-01 实体，不发明领域外概念。
2. **无版本实体**：不出现 WorldStateVersion / Revision / Commit / ObjectVersion 表（Baseline §39）。
3. **Branch 零复制**：分支历史只存 `parent_history_id + parent_at`，祖先历史不落地副本（INV-08）。
4. **历史不可变写入**：State Change 一经写入只允许整条删除（随 Branch 删除），不允许原位修改字段后无痕；事件级修正必须留下新记录并保留原记录可查（对齐 ADR-004 精神：操作记录 ≠ 世界版本）。
5. ** provenance 元数据**：变更可携带来源标注（如 changeset_id），这是**溯源字段**，不构成版本体系。

# 2. 表结构（关系模型示意，SQLite 方言）

```sql
-- 迁移 0001：核心历史引擎
CREATE TABLE maps (
  map_id        TEXT PRIMARY KEY,
  name          TEXT NOT NULL,
  description   TEXT NOT NULL DEFAULT '',
  calendar      TEXT NOT NULL,
  epoch_ordinal INTEGER NOT NULL,          -- epoch 的 tick（日历 id 即 maps.calendar）
  root_history_id TEXT NOT NULL,
  created_at    TEXT NOT NULL              -- 系统时间（认知时间），仅记录，不参与重建
);

CREATE TABLE histories (
  history_id  TEXT PRIMARY KEY,
  map_id      TEXT NOT NULL REFERENCES maps(map_id),
  kind        TEXT NOT NULL CHECK (kind IN ('root','branch')),
  calendar    TEXT NOT NULL,
  -- branch 专用列（kind='branch' 时非空；root 时为 NULL）
  branch_id         TEXT NULL UNIQUE,
  branch_name       TEXT NULL,
  parent_history_id TEXT NULL REFERENCES histories(history_id),
  parent_at_ordinal INTEGER NULL,
  status            TEXT NULL CHECK (status IN ('active','deprecated'))
);
CREATE INDEX idx_histories_map ON histories(map_id);
CREATE INDEX idx_histories_parent ON histories(parent_history_id);

CREATE TABLE objects_registry (
  -- 对象注册表：对象归属哪张地图（P2-01 §2 引用≠归属）
  map_id     TEXT NOT NULL REFERENCES maps(map_id),
  object_id  TEXT NOT NULL,
  object_type TEXT NOT NULL,
  PRIMARY KEY (map_id, object_id)
);

CREATE TABLE state_changes (
  change_id      TEXT PRIMARY KEY,
  history_id     TEXT NOT NULL REFERENCES histories(history_id),
  target         TEXT NOT NULL,             -- ObjectId 引用（可指向祖先地图注册表对象）
  time_ordinal   INTEGER NOT NULL,          -- effective_time（同一 history 一个日历）
  sequence       INTEGER NOT NULL,
  change_type    TEXT NOT NULL,
  payload        TEXT NOT NULL,             -- JSON
  event_id       TEXT NULL,
  provenance     TEXT NULL,                 -- JSON：来源（changeset_id 等），非版本概念
  created_at     TEXT NOT NULL,
  UNIQUE (history_id, time_ordinal, sequence),     -- HSE-03 确定性排序键
  UNIQUE (change_id)
);
CREATE INDEX idx_changes_history ON state_changes(history_id, time_ordinal, sequence);
CREATE INDEX idx_changes_event ON state_changes(event_id);

CREATE TABLE events (
  event_id     TEXT PRIMARY KEY,
  history_id   TEXT NOT NULL REFERENCES histories(history_id),
  time_ordinal INTEGER NOT NULL,
  title        TEXT NOT NULL,
  description  TEXT NOT NULL DEFAULT '',
  participants TEXT NOT NULL DEFAULT '[]',  -- JSON
  meta         TEXT NOT NULL DEFAULT '{}',  -- JSON
  created_at   TEXT NOT NULL
);
CREATE INDEX idx_events_history ON events(history_id, time_ordinal);
```

设计说明：

- `HistoricalTime` 存为 `(calendar, ordinal)` 两列或"日历列 + ordinal 列"；因**同一 Map 全链一个日历**（A9），`time_ordinal` 配合 `histories.calendar` 即可比较，跨日历比较在服务层拒绝。
- `objects_registry` 是第一版最小实现；Shared Object 跨图引用（P2-08）落地时在此表上扩展，不阻塞。
- `created_at` 是**认知时间**记录（ADR-003 §3-8：系统记录时间 ≠ 历史生效时间），仅审计用，永不参与重建。

# 3. World State 不落库

World State 是 `(history 内容, T)` 的纯函数结果（P2-01 §6.3）。第一版**完全不缓存**：

- 每次查询即时重算（单机数据量下可接受）；
- 未来性能缓存 / Checkpoint 属**另一专项**（D01 讨论方向，未冻结），落地时必须是"派生缓存 + 完整性校验"形态，不产生权威历史实体。

# 4. 迁移机制

1. 迁移 = 有序编号的 SQL 脚本（`0001_core.sql`、`0002_tasks.sql`…）+ 一张 `schema_migrations(version)` 表。
2. 启动时按序应用未执行的迁移，单迁移单事务，失败即停。
3. 迁移脚本只增不改：已发布的迁移文件不得修改（改 = 新迁移）。

# 5. 事务边界

| 操作 | 边界 |
| --- | --- |
| 建图（map + root history） | 单事务 |
| 建分支（branch history） | 单事务 |
| 追加 State Change / Event | 单事务（写入校验通过后） |
| 删分支（changes + events + history） | 单事务；有子分支先拒绝（服务层前置检查 + 迁移内约束兜底） |
| Change Set Apply（N 条变更落库） | **单事务整体原子**（对应 ADR-004 的原子精神；详见 TD-03） |
| 仓储实现 | 同一事务内共享连接；内存实现以同语义对齐，作测试参照 |

# 6. 语义缺省登记（本设计作出的缺省，需追认）

| # | 缺省 | 依据/影响 |
| --- | --- | --- |
| 1 | Branch 自有 Event 同样适用 `> parent_at` 时间窗 | 从 ADR-005 §2 类推；文档未明文，需追认 |
| 2 | 已删除/未创建对象上的属性命令：字段照常生效、`exists` 不变，对象记录保留 | ADR-003 V1.2 §3-11/12 待设计项的最简缺省（命令执行/存在状态/警告三维独立） |
| 3 | 警告实现为**重建时重算的派生数据**，不落库 | A10-07"仅原始条件消失才重评"的最简实现；警告状态机留待 P2-08 |
| 4 | `sequence` 自动分配 = 同 `(history, time)` 桶内 max+1 | P2-01 §7-A8 建议方向 |
| 5 | 删除 Branch 物理删除其自有变更与事件 | HSE-09；如需"回收站"级安全网，属产品决策 |

# 7. 确认前不得实现的事项

- Shared Object 跨图引用与局部历史合成（P2-08 专项，含 Command 模型与排序算法）。
- Checkpoint / 缓存机制（D01 讨论未冻结，不构成实施授权）。
- Undo / Redo 底层机制（ADR-004 §8 十项待设计）。
