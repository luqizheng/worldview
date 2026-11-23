# 003 — Field Payload Import Adapter

**日期**：2026-11-23
**分支**：`feat/field-package-reader-writer`（PR #17 head，stacked on PR #11）
**状态**：Windows 实施完成，549/549 通过；macOS 待 GitHub Actions

## 1. 任务目标

实现 Field Package（交换格式）到内部 Field Dataset / Revision（领域存储模型）的导入适配层：先验证后持久化、失败不产生正式 Revision、中途失败有临时文件清理策略、两种存储路径各有明确失败处理、不静默覆盖 immutable artifact、错误分类清晰。

## 2. 实际完成内容

新增 `FieldPackageImporter`（`WorldView.Application.Fields`）。`ImportAsync(packageStream, plan)` 的编排顺序：

1. **格式级验证**：`FieldPackageReader.Open`（二进制结构、Manifest、Directory、Payload 布局全部校验）；流要求可读可 seek。
2. **归属预检**：Task 1 的 `FieldPackageOwnerContextValidator.Validate`（Descriptor 注册/语义、Map Space、Spatial Reference 绑定、precision profile、vertical reference 对目标 Project/Map 的权威行）。
3. **全量 tile 校验**：逐 tile `ReadTile`（SHA-256 + mask 值域），腐坏 tile 在发布前即拒绝——Reader 的 tile hash 是按读校验的，导入作为正式发布闸门必须此刻全验。
4. **持久化**，按 plan.StorageKind 显式选择（无隐式大小阈值）：
   - **SqliteBlob**：整包字节读入内存 → SHA-256 → `FieldDatasetService.PublishRevision`（payload BLOB 行 + 不可变 Revision + Dataset CAS 同一 SQLite 事务；失败整体回滚，无任何残留行）。
   - **ManagedFile**：`FieldPayloadPublisher.PublishManagedFileRevisionAsync`（字节流经 staging→flush→hash 验证→原子重命名进入内容寻址存储，再走同一发布事务）。
5. ContentFingerprint = 整包字节 SHA-256；ManifestJson = canonical manifest（交换/诊断副本，typed 语义以 `Metadata` 投影持久化）；payload format `application/vnd.worldview.field-payload` v1；FieldDatasetId/FieldRevisionId 未指定时由导入器分配新 GUID。

错误分类（调用方可按异常类型+Code 分支）：

| 阶段 | 异常 | 示例 |
|---|---|---|
| 二进制格式 | `FieldPackageValidationException` + `WVPK_*` 码 | magic/version/directory/tile hash/mask |
| 归属预检 | `FieldPackageValidationException` + `WVPK_OWNER_*`/`WVPK_DESCRIPTOR_MISMATCH` | 跨 Map Space 引用、未注册 descriptor |
| 发布冲突（CAS/身份） | `CoreDomainConflictException` / `CoreDomainDataException` | stale CAS pin、dataset identity 不符 |
| I/O | `IOException`/`InvalidDataException` 原样传播 | staging 写失败、既有文件 hash 冲突 |
| 输入错误 | `ArgumentException` | 非 seek 流、未知 storage kind |

失败策略（对应任务要求 5–8）：

- 验证阶段（1–3）失败：零持久化副作用（测试断言无 Dataset/Payload 行、store 无文件、staging 为空）。
- SqliteBlob 发布失败：单事务回滚，无 Revision/BLOB 行/文件。
- ManagedFile staging 中途失败：staging 文件由 `FieldPayloadFileStore.PutAsync` 的 finally 即时删除；残留 staging 由项目打开时的 `CleanupStagingFiles` 恢复兜底。
- ManagedFile DB 发布失败：已发布文件**故意保留为 orphan**（SQLite 不可能回滚文件系统），由 `FieldPayloadIntegrityReconciler.CollectOrphanFilesAsync` 经新鲜 DB 引用扫描后安全回收；正式引用的文件永不误删（测试固化）。
- 不静默覆盖：内容寻址路径下既有同 hash 文件只验证不复写（`FieldPayloadFileStore` 既有行为，测试以重复导入同字节包固化）。

## 3. 修改文件清单

| 文件 | 变更 |
|---|---|
| `WorldView/src/WorldView.Application/Fields/FieldPackageImporter.cs` | 新增：导入适配器、`ImportFieldPackagePlan`、`FieldPackageImportResult` |
| `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/FieldPackageImportTests.cs` | 新增：11 个真实 SQLite + 真实文件系统测试 |
| `docs/development/local-progress/field-package/003-payload-import.md` | 本报告 |

## 4. 重要实现决策

1. **存储路径由调用方显式指定**（`ImportFieldPackagePlan.StorageKind` 必填）：存储基线 §2.1 明确"不在本文档设定统一阈值，须先做代表性负载测量"，因此导入器不发明自动 BLOB/文件切换阈值。
2. **领域模型与包布局隔离**：包字节作为不透明已验证 payload 存储（BLOB 或内容寻址文件），语义仅以 typed `FieldRevisionMetadata` 投影持久化；`ManifestJson` 保留为 canonical 交换/诊断副本（既有发布契约）。领域类型不引用任何 WVFLDPK 布局类型。
3. **导入即发布闸门**：全量 tile 校验放在发布前（V1 仅 codec 0，stored==decoded，代价为一次有界顺序读；上限由 Reader policy 承担，Task 3 再补 conformance）。
4. **复用既有发布编排**：ManagedFile 路径直接复用 `FieldPayloadPublisher`（其 orphan/恢复语义已在 PR #11 的恢复测试与项目打开恢复链路中验证），导入器不自建第二条发布路径。
5. **验证先于准备**：pre-tile/owner 校验全部发生在任何 staging/内存装载之前；BLOB 路径的字节装载也在校验之后。

## 5. Windows 编译结果

- `dotnet build -c Debug`：成功，0 错误。
- `dotnet build -c Release`：成功，0 错误。

## 6. Windows 单元测试结果

- 新增 `FieldPackageImportTests` 11 用例全部通过。
- 全解决方案 Debug：**549/549**；Release：**549/549**。

覆盖点：BLOB 全链路（revision/dataset/current 指针/BLOB 字节回读一致）、ManagedFile 全链路（内容寻址文件 + `FieldPayloadReader` 存储无关读取一致）、破坏 magic 无副作用、tile 腐坏发布前拒绝（零副作用）、Map Space 不匹配 staging 前拒绝（零副作用）、BLOB CAS 失败无第二 revision、ManagedFile CAS 失败 orphan 保留且对账回收（引用文件保留）、既有 dataset 错误身份冲突无部分写入、顺序 revision 世系（revision_number 2、parent 指针）、staging 中途 I/O 失败即时清理、非法输入（未知 storage kind / 非 seek 流）拒绝。

## 7. 执行的测试命令

```text
dotnet build -c Debug --nologo
dotnet test WorldView.sln -c Debug --nologo     # 549/549
dotnet build -c Release --nologo -v q
dotnet test WorldView.sln -c Release --nologo   # 549/549
# 单类过滤：
dotnet tests/WorldView.Infrastructure.Tests/bin/Debug/net11.0/WorldView.Infrastructure.Tests.dll \
  -class "WorldView.Infrastructure.Tests.Persistence.CoreDomain.FieldPackageImportTests"
```

## 8. 未解决问题

1. **导入产物无 UI/Application 入口**：与 CD-01 备注"Map/Branch 创建服务无 UI 入口"同状态——导入器作为 Application 服务就绪，Desktop 接线（文件选择对话框、进度/错误呈现）属后续 UI 阶段。
2. **ManagedFile 路径的双重整包哈希**：digest 先整包算一次（发布 plan 需要 ContentFingerprint），`PutAsync` 拷贝时再算一次。正确性优先的 V1 实现；如大包导入出现实测瓶颈，可在 `IFieldPayloadFileStore` 增加外部传入 digest 的变体端口，属后续优化（未测瓶颈前不做）。
3. **BLOB 路径整包驻留内存**：调用方显式选择 BLOB 即接受该代价；存储基线禁止设统一阈值，未来由代表性负载测量决定是否给出推荐切换点。

## 9. 需要 GitHub Actions 验证的内容

- macOS Debug/Release 构建 + 全部 549 测试（含 11 个导入测试；真实 SQLite 与 `FieldPayloadFileStore` 的 File.Move/WriteThrough/reparse-point 检查均为平台相关路径，需 macOS 实测）。
- 内容寻址文件在 macOS 文件系统上的原子 rename 与 staging 清理行为。

## 10. Git commit SHA

见本报告提交后的 `feat/field-package-reader-writer` 分支顶端（提交信息 `feat(fields): add field package import adapter`）。

## 11. 与原设计不一致的地方

1. 任务要求"导入失败不能产生正式发布的 Revision"在 ManagedFile 路径的语义取舍：DB 发布失败时已发布文件保留为 orphan（不删除）——这是既有发布协议的既定策略（防误删共享内容），导入器遵循之；"不产生正式 Revision"始终成立（orphan 无 DB 引用）。
2. 未提供"打开+导入一步式"的 UI 级 API：导入器要求调用方持有包流；预检（Task 1）与导入解耦，UI 可先 Validate 再 Import 以获得纯预检反馈。两阶段组合是编排建议而非独立实现。
