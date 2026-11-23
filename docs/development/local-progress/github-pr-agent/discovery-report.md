# Discovery Report — GitHub PR Task 扫描

- **扫描时间**：2026-11-23 15:20 (+08:00)
- **仓库**：`luqizheng/worldview`
- **扫描方式**：GitHub REST API（无 `gh` CLI；以 PR 列表 + body + workflow runs 为主），本地 `git ls-remote` 校验分支 HEAD
- **组织原则**：以**任务**为执行单位，PR 仅作为任务来源与代码交付载体。一个 PR 可拆出多个任务，一个任务可跨 PR 依赖。

## 1. Open PR 总览（12 个）

| PR | 标题 | Draft | Base | Head | Head SHA | 观察到的 CI |
|---|---|---|---|---|---|---|
| #1 | fix(core-domain): harden CD-02 Asset and Geometry integrity | 否 | master | `fix/cd-02-g1-asset-geometry-integrity` | `ab6d00f` | body 记录 4/4 通过（未在本次扫描重验） |
| #2 | fix(assets): require byte-verification evidence for Verified state | 否 | `fix/cd-02-g1-...` | `feat/cd-02-g2-asset-byte-verification` | `cf5dd1a` | body 记录 4/4 通过（未重验） |
| #3 | docs: reconcile provider proof and geometry contracts | 是 | `feat/cd-02-g2-...` | `audit/cd-02-g3-geometry-transaction-gap` | `0fca159` | 文档分支，无独立 CI 证据 |
| #4 | test(persistence): cover SQLite connection policy and bounded busy handling | 是 | `fix/cd-02-g1-...` | `test/p2-09-12-provider-transaction-proof` | `d603afd` | body 记录 386/386 ×4 |
| #5 | fix(geometry): reject empty component and asset identities | 是 | `fix/cd-02-g1-...` | `fix/cd-02-g3-geometry-manifest-identities` | `02299c5` | body 记录 389/389 ×4 |
| #6 | feat(domain): support conditional JSON Schema validation | 是 | `feat/cd-02-g2-...` | `feat/json-schema-conditional-keywords` | `0b1e42d` | body 记录 402/402 ×4 |
| #7 | feat(geometry): publish Building/Cave definition and asset links atomically | 是 | `fix/cd-02-g3-manifest-...` | `feat/cd-02-g3-atomic-geometry-publication` | `a8d31f9` | **无 run**（stacked，base 非 master） |
| #11 | feat(persistence): start EF Core 1.0 fresh-schema cutover | 是 | master | `feat/efcore-v1-complete-model` | `991aa17` | run `45084309115` success |
| #12 | ci: add per-test performance diagnostics | 否 | master | `perf/unit-test-diagnostics` | `dcf12a7` | run `45044873670` success |
| #15 | feat(storage): versioned Project and Map settings documents | 是 | `feat/efcore-v1-complete-model` | `feat/project-map-settings-documents` | `438cb11` | 无直接 run；由临时验证 PR 覆盖 |
| #17 | feat(package): add canonical Field Package manifest validation | 是 | `feat/efcore-v1-complete-model` | `feat/field-package-reader-writer` | `6f91f6f` | 无直接 run（stacked） |
| #21 | ci: validate field package tasks 005-007 on macOS | 否 | master | `feat/field-package-reader-writer` | `6f91f6f` | run `45124453470`：macOS Debug/Release success；Windows 进行中 |

## 2. 任务清单（以任务为单位）

### 2.1 可执行（READY / IN_PROGRESS）

| 任务 | 来源 | 内容 | 状态 |
|---|---|---|---|
| **T-01** 完成 Task 005/006/007 四平台 CI 验证与证据回写 | PR #21 → PR #17 | 在确切 head `6f91f6f` 上取得 Windows/macOS Debug/Release 结果；回写 `007` 报告 §9、`local-progress/README.md`、PR #17 Description（含陈旧 head 修正） | **IN_PROGRESS**（macOS 绿；Windows 运行中） |

### 2.2 被依赖阻塞（BLOCKED）

| 任务 | 来源 | 阻塞原因 |
|---|---|---|
| T-02 G3 原子发布与 G2 证据路径整合 | PR #7 | 依赖 PR #2（G2）与 PR #5；PR #7 body 明确"不得先于 G2 合并"，且要求"rebase 到 G2 前用真实 integrity verification evidence 替换 test-only Verified 写入口"。改 base 属禁止操作，需人工决定整合顺序 |
| T-03 PR #7 的 CI 验证 | PR #7 | head `a8d31f9` 从未触发 CI（base 非 master）。需临时验证 PR 或调整 workflow 触发条件，后者属 CI 配置决策 |
| T-04 PR #15 收尾 | PR #15 | 依赖 PR #11 合并后 retarget 到 master 并重跑 CI；合并动作禁止自动执行 |
| T-05 TX-04 / TX-31 独立读者、进程崩溃恢复、自动重试 | PR #1 / PR #4 | 自动重试需稳定 idempotency key 设计决策；崩溃恢复超出 V1 单机契约，属产品/架构决策 |
| T-06 Field Package 生产参数（精度/量化、Land-Water 类码、codec profile、tile 尺寸） | PR #17 | 项目约束第 9 条明确禁止擅自决定 |
| T-07 Import/Export 的用户可见 UI/Application 工作流 | PR #17 | 依赖 T-06 生产 profile 落地 |
| T-08 代表性大数据集性能/内存基准 | PR #17 | 依赖 T-06；无既定基准环境 |
| T-09 读路径 manifest 密码学绑定（可选 digest 列） | PR #17 / Task 006 §4.3 | 威胁模型决策未确认，任务书明确不作为默认方案 |

### 2.3 等待人工审查（NEEDS_REVIEW，无代码工作可做）

| 任务 | 来源 | 说明 |
|---|---|---|
| T-10 CD-02-G1 硬化 | PR #1 | 非 Draft，CI 已绿，等 review |
| T-11 G2 Verified 证据 | PR #2 | 非 Draft，stacked on #1 |
| T-12 JSON Schema 条件关键字 | PR #6 | Draft，CI 已绿 |
| T-13 Geometry manifest 身份校验 | PR #5 | Draft，CI 已绿；是 #7 的前置 |
| T-14 EF Core 1.0 cutover | PR #11 | Draft，CI 已绿；是 #15/#17 的前置 |
| T-15 Project/Map settings 文档 | PR #15 | Draft，stacked |
| T-16 master CI runner-context 修复 | PR #12 | 非 Draft，CI 绿；**master 分支当前仍红**（push runs `44993903903`/`44993832456`/`44989347995` failure），合并后才会恢复 |
| T-17 G3 文档审计 | PR #3 | Draft，等待架构 review |
| T-18 TX 覆盖率补强 | PR #4 | Draft，CI 已绿 |

### 2.4 需要用户操作（禁止 Agent 自动执行）

| 任务 | 说明 |
|---|---|
| T-19 关闭临时 CI PR #21 | body 注明"DO NOT MERGE，跑完关闭"；Close PR 在禁止清单内 |
| T-20 PR #17 与 #11 的合并顺序 | Merge 在禁止清单内 |

## 3. 任务依赖图

```text
T-14 PR#11 (EF Core 1.0)  ──────────────┬──► T-04 PR#15 收尾
        (human merge)                   └──► T-01 PR#17 收尾 ──► T-06 生产参数 ──► T-07/T-08

T-10 PR#1 (G1) ──► T-11 PR#2 (G2) ──┐
                                     ├──► T-02 PR#7 原子发布整合 ──► T-03 PR#7 CI
T-13 PR#5 (manifest 身份) ───────────┘
                                     └──► T-05 TX 独立读者/崩溃恢复（blocked 设计决策）

T-16 PR#12 (master CI runner) ──► 恢复 master 绿灯（human merge）
T-18 PR#4 (TX 覆盖) ──► 独立分支，无阻塞
```

## 4. 优先级结论

1. **T-01** 是唯一可立即执行且能闭合验收证据的任务 → 本次执行。
2. T-16 价值极高（master CI 仍红），但只能等人工合并 PR #12，不属可执行任务。
3. T-02/T-03 是下一阶段真正的开发任务，但都需要先确定整合顺序（PR #2 合并或明确 rebase 授权）。

## 5. Blocked / 人工决策清单

见 §2.2、§2.4；本次不猜测任何生产参数、不改变 PR base、不合并、不关闭 PR。

## 6. 状态更新（2026-11-23，执行后）

| 任务 | 原状态 | 现状态 | 说明 |
|---|---|---|---|
| T-01 | IN_PROGRESS | COMPLETED | 四平台 CI 全绿（run `45124453470`），文档与 PR #17/#21 描述已回写 |
| T-02 | BLOCKED | COMPLETED | G2 合并入 PR #7 分支（`617d052`），迁移 2 处 test-only 完整性写入（`4ba4191`）；本地 407/407 |
| T-03 | BLOCKED | COMPLETED | PR #7 分支 workflow 触发 `push: **`，push 与 pull_request 两个 run 均 4/4 success |

**经验修正**：仓库内不同分支携带不同版本的 `dotnet-ci.yml`。`feat/field-package-reader-writer` 等分支沿用仅 master 触发的旧版，故需要临时 CI PR；PR #7 分支使用 `push: **` 版本，可自行触发。后续判断某分支是否会获得 CI，必须先读该分支上的 workflow 文件，而不是只看 master。

