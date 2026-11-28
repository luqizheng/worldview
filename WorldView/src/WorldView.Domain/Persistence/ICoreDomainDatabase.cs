using System;
using System.Collections.Generic;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Persistence;

/// <summary>
/// Core Domain 持久化端口（Domain 拥有；由 Infrastructure 以 SQLite 实现）。
/// 结构写操作（InsertProject / CreateMapStructure / CreateBranchStructure）、Object
/// 写路径（Reserve/Bind/Insert/Definition CAS/注册）与 Initial State 修订写路径
/// （AppendRevision/MoveCursor/SelectRedoChild）各自构成一个原子事务，失败整体回滚，
/// 不留半成品（事务边界归实现方，AF-02 端口契约；CD-02-A §13.2 写路径事务表；
/// FOUNDATION-01B §6）。所有复合写在实现方的统一连接协调器临界区内完成事务内
/// 前置状态重读校验（CD-01-F1 条件 1 响应，CD-02-A §13.3）。
/// 读操作按 Project 范围隔离；行数据损坏时实现方抛 <see cref="CoreDomainDataException"/>
/// （fail-closed，不静默修补）；前置状态/CAS 不匹配抛 <see cref="CoreDomainConflictException"/>。
/// </summary>
public interface ICoreDomainDatabase
{
    /// <summary>按 ID 查找 Project；不存在返回 null。</summary>
    ProjectRecord? FindProject(Guid projectId);

    /// <summary>读取 core_project 全部原始行（身份校验规则归 ProjectRules）。</summary>
    IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows();

    /// <summary>
    /// 事务：插入 Project 身份行 + 十内置 Object Type 种子行 + 各可分配类型的
    /// allocator 行（OD-4 建议案：Project 创建事务内播种；P2-09-18 §4.2）。
    /// 已存在任何 Project 行时抛异常并回滚（一库一项目）。
    /// </summary>
    void InsertProject(ProjectCreationPlan plan);

    /// <summary>查找属于指定 Project 的 Map；不存在或跨 Project 返回 null。</summary>
    MapRecord? FindMap(Guid projectId, Guid mapId);

    /// <summary>查找同一 Project 内不可变的 Spatial Reference 定义。</summary>
    SpatialReferenceDefinitionRecord? FindSpatialReferenceDefinition(Guid projectId, string spatialReferenceId);

    /// <summary>插入不可变 Spatial Reference 定义；重复身份或跨 Project 引用必须拒绝。</summary>
    void InsertSpatialReferenceDefinition(SpatialReferenceDefinitionRecord definition);

    /// <summary>为尚未绑定的 Map 建立初始 Spatial Reference；不提供原位替换路径。</summary>
    MapRecord BindMapSpatialReference(Guid projectId, Guid mapId, string spatialReferenceId, long nowUtcMs);

    /// <summary>列出指定 Project 的全部 Map（按创建时间、ID 稳定排序）。</summary>
    IReadOnlyList<MapRecord> ListMaps(Guid projectId);

    /// <summary>
    /// 事务：一次写入 Map 创建计划的全部结构（不可变默认 Spatial Reference 定义及 Map 绑定、
    /// Map、根 History、default Calendar、day-zero Configuration）；任一失败整体回滚。
    /// </summary>
    void CreateMapStructure(MapCreationPlan plan);

    /// <summary>按 ID 查找 History；不存在返回 null。</summary>
    HistoryRecord? FindHistory(Guid projectId, Guid historyId);

    /// <summary>解析 Map 的根 History（history_kind=map_root 且 owner_map_id=map）；缺失返回 null。</summary>
    HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId);

    /// <summary>按 ID 查找 Branch；不存在返回 null。</summary>
    BranchRecord? FindBranch(Guid projectId, Guid branchId);

    /// <summary>按自有历史查找 Branch（世系上溯用）；无对应 Branch 返回 null。</summary>
    BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId);

    /// <summary>事务：一次写入 Branch 自有 History 与 Branch 行；失败整体回滚。</summary>
    void CreateBranchStructure(BranchCreationPlan plan);

    // ---- Object Type（Project 级配置权威；CD-02-A §7） ----

    /// <summary>列出 Project 的全部 Object Type 行（按 type_key 稳定排序）。</summary>
    IReadOnlyList<ObjectTypeRecord> ListObjectTypes(Guid projectId);

    /// <summary>按类型行 ID 查找 Object Type；不存在或跨 Project 返回 null。</summary>
    ObjectTypeRecord? FindObjectType(Guid projectId, Guid objectTypeId);

    /// <summary>按稳定 type_key 查找 Object Type；不存在返回 null。</summary>
    ObjectTypeRecord? FindObjectTypeByTypeKey(Guid projectId, string typeKey);

    // ---- Object ID Reservation（P2-09-04 状态机） ----

    /// <summary>按 ID 查找 Reservation；不存在或跨 Project 返回 null。</summary>
    ReservationRecord? FindReservation(Guid projectId, Guid reservationId);

    /// <summary>
    /// 事务：分配 Object ID——allocator 读改写（next_sequence 前进，永不回退）与
    /// reservation 插入同一事务。提供 request_key 时幂等：同 (project, request_key)
    /// 已存在则返回既有分配（RecoveringByRequestKey=true），不新消耗 ID（TX-21/22）。
    /// 类型不存在/不可分配抛冲突；前置校验在事务内完成。
    /// </summary>
    ReservationAllocation ReserveObjectId(ObjectIdReservationRequest request);

    /// <summary>
    /// 事务：Reserved→Bound——插入 core_object draft 行（含 pin）+ reservation 状态
    /// 转换 + identity 回写。事务内重读 reservation 校验前置状态（不依赖调用方传值）；
    /// 非 Reserved 抛 <see cref="CoreDomainConflictException"/>；失败整体回滚。
    /// </summary>
    ReservationRecord BindReservation(ObjectBindRequest request);

    /// <summary>
    /// 事务：放弃 Reservation——Reserved/Bound→Abandoned。Bound 后放弃保留 identity
    /// 行为 abandoned 墓碑；ID 永不复用。事务内重读校验前置状态。
    /// </summary>
    ReservationRecord AbandonReservation(Guid projectId, Guid reservationId, string? abandonReason, long nowUtcMs);

    // ---- Object identity / Definition（P1 存在层 + 权威层③） ----

    /// <summary>按身份键查找 Object；不存在或跨 Project 返回 null。读侧对未知 pin fail-closed。</summary>
    ObjectRecord? FindObject(Guid projectId, Guid objectIdentityId);

    /// <summary>读取当前定义行；不存在返回 null。读侧校验 hash 与 payload 一致（fail-closed）。</summary>
    ObjectDefinitionRecord? ReadObjectDefinition(Guid projectId, Guid objectIdentityId);

    /// <summary>
    /// 事务：正式创建（Bound→Inserted，TX-55）——identity draft→active + reservation
    /// →Inserted + definition 首行 + 可选 Map 注册 + 可选 Initial State 成员 + 同事务
    /// immutable child revision 追加（事务内重读 cursor 校验 expected active revision +
    /// cursor version，按提交行重算指纹/操作描述；<see cref="ObjectInsertPlan.InitialRevisionAppend"/>
    /// 为 InitialMembership 必需上下文）。事务内重读校验前置状态；失败整体回滚、无半成品。
    /// </summary>
    void InsertObject(ObjectInsertPlan plan);

    /// <summary>
    /// 事务：Definition CAS 编辑——事务内比对 expected hash 与当前行（不匹配抛
    /// <see cref="CoreDomainConflictException"/>），重写 payload/hash/version。
    /// 无 revision 表（P2-09-16 §8）。
    /// </summary>
    void EditObjectDefinition(ObjectDefinitionEditPlan plan);

    /// <summary>
    /// 单一事务：原子发布 Definition 与从 payload 派生的完整 Geometry Asset typed link set。
    /// 事务内重读 Object 生命周期、Definition CAS、core_asset hash 与 integrity state，
    /// 并以完整集合一致性校验后才写入；失败整体回滚，不允许部分成功。
    /// </summary>
    void PublishObjectDefinitionWithGeometryAssets(ObjectDefinitionPublicationPlan plan);

    // ---- Map 注册（P2 存在层） ----

    /// <summary>查找注册行；不存在返回 null。</summary>
    MapObjectRefRecord? FindMapObjectRef(Guid projectId, Guid mapId, Guid objectIdentityId);

    /// <summary>列出 Map 的全部注册行（按 identity 稳定排序）。</summary>
    IReadOnlyList<MapObjectRefRecord> ListMapObjectRefs(Guid projectId, Guid mapId);

    /// <summary>
    /// 事务：注册 Object 到 Map——事务内重读身份行校验（active、scope 规则：
    /// map_local 仅 owner Map），单行插入。重复注册抛冲突。
    /// </summary>
    void RegisterMapObject(MapObjectRegistrationPlan plan);

    /// <summary>
    /// 事务：取消注册——该 Map 任一 revision 快照仍含该身份成员时拒绝（P3⇒P2 不变量；
    /// revision 树不可变、成员行复合 FK 指向注册行，永久保留）；无成员引用则单行删除。
    /// </summary>
    void UnregisterMapObject(Guid projectId, Guid mapId, Guid objectIdentityId);

    // ---- Map Initial State 修订（P3 存在层；CD-02-A §10 + FOUNDATION-01B §3/§6/§10） ----
    // baseline identity（core_map_initial_state）与 revision 树（immutable 完整快照）
    // 由 Map 创建事务建立，无 lazy 建立路径；cursor 是唯一可变权威行。全部 cursor
    // mutation 校验 expected active revision + cursor version（stale 拒绝，无部分写）。

    /// <summary>查找 Map 的 Initial State baseline 身份；不存在返回 null（每 Map 至多一行）。</summary>
    MapInitialStateBaselineRecord? FindMapInitialStateBaseline(Guid projectId, Guid mapId);

    /// <summary>按 revision 身份读取不可变快照（revision 元数据 + 全部成员行）；不存在返回 null。</summary>
    MapInitialStateRevisionSnapshot? FindMapInitialStateRevisionSnapshot(Guid projectId, Guid mapId, Guid revisionId);

    /// <summary>列出 baseline 全部 revision 元数据（revision_number 升序；历史展示/校验用）。</summary>
    IReadOnlyList<MapInitialStateRevisionRecord> ListMapInitialStateRevisions(Guid projectId, Guid mapId);

    /// <summary>
    /// 读取活动状态（活动 revision 快照 + cursor + 活动 revision 的直接子 revision 列表）
    /// 作为单一逻辑读（连接协调器临界区内一致快照）；baseline/cursor 缺失返回 null。
    /// </summary>
    MapInitialStateRevisionState? FindMapInitialStateRevisionState(Guid projectId, Guid mapId);

    /// <summary>
    /// 事务：追加 immutable child revision（完整快照成员集）——事务内重读 cursor 校验
    /// expected active revision + cursor version（不匹配抛 <see cref="CoreDomainConflictException"/>），
    /// 重读父快照并按提交行重算指纹/操作描述（与计划不一致 fail-closed），同一事务写入
    /// 新 revision 行、全部成员行与 cursor 前移（清除 redo 选择、version+1）；失败整体回滚。
    /// revision_number 取该 baseline 现有最大值 +1（单调递增）；旧 revision 与后代原样保留。
    /// </summary>
    void AppendMapInitialStateRevision(AppendMapInitialStateRevisionPlan plan);

    /// <summary>
    /// 事务：cursor 移动——Undo 移到 parent（selected redo child 指向刚离开的 revision），
    /// Redo 移到直接子（清除 redo 选择）。事务内重读校验 expected active revision +
    /// cursor version 与 parent/direct-child 关系；root 不可 Undo；失败整体回滚、无 revision 写入。
    /// </summary>
    void MoveMapInitialStateCursor(MoveMapInitialStateCursorPlan plan);

    /// <summary>
    /// 事务：持久化显式 redo 分支选择（不动 active revision）——目标必须是活动 revision
    /// 的直接子且同 baseline；事务内重读校验 CAS；cursor version+1；失败整体回滚。
    /// </summary>
    void SelectMapInitialStateRedoChild(SelectMapInitialStateRedoChildPlan plan);

    // ---- Core Asset 权威元数据（core_asset；P2-09-11 §4.6） ----

    /// <summary>按 Asset ID 查找；不存在或跨 Project 返回 null。</summary>
    AssetRecord? FindAsset(Guid projectId, Guid assetId);

    /// <summary>按内容 hash 查找（内容寻址幂等依据）。</summary>
    AssetRecord? FindAssetByContentHash(Guid projectId, string hashAlgorithm, string contentHash);

    /// <summary>列出 Project 的全部 Asset 元数据（按 asset_id 稳定排序）。</summary>
    IReadOnlyList<AssetRecord> ListAssets(Guid projectId);

    /// <summary>
    /// 事务：登记已发布 Asset 的权威元数据（字节层已发布并 hash 验证之后）。
    /// 同 (hash_algorithm, content_hash) 已存在 → 幂等返回既有行（内容寻址）。
    /// </summary>
    AssetRecord RegisterAsset(RegisterAssetPlan plan);

    /// <summary>记录字节扫描的失败结果；只允许 Corrupt/Missing，不能通过此接口写入 Verified。</summary>
    void RecordAssetIntegrityFailure(Guid projectId, Guid assetId, AssetIntegrityState integrityState, long nowUtcMs);

    /// <summary>
    /// 事务：写入 Verified——仅接受经真实字节流测量得到的 <see cref="AssetByteDigest"/>
    /// （不透明凭证：只能由 Domain 的 internal <see cref="AssetByteDigest.MeasureAsync"/>
    /// 在完整读取字节后产生，创建能力经 InternalsVisibleTo 授权给 Application 验证
    /// 路径（Task 002-B），普通调用方与任何 public 工厂均不可达；Task 002-A）。
    /// 实现方必须在同一写事务内重读
    /// 当前 Asset 元数据，要求 digest 的 hash 与 byte length 和当前 content_hash /
    /// byte_length 完全一致（Asset 身份由 Project+Asset 键界定）；不一致抛冲突。
    /// </summary>
    void MarkAssetVerified(Guid projectId, Guid assetId, AssetByteDigest digest, long nowUtcMs);

    /// <summary>列出未被任何 typed link 引用的 Asset ID（孤儿对账；只读，不自动删除）。</summary>
    IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId);

    // ---- Definition Asset typed link（core_object_definition_asset；P2-09-19 §9） ----

    /// <summary>按 (owner, asset, role) 查找 definition Asset link；不存在返回 null。</summary>
    ObjectDefinitionAssetLinkRecord? FindDefinitionAssetLink(Guid projectId, Guid objectIdentityId, Guid assetId, string role);

    /// <summary>列出 Object 的全部 definition Asset links（按 asset_id/role 稳定排序）。</summary>
    IReadOnlyList<ObjectDefinitionAssetLinkRecord> ListDefinitionAssetLinks(Guid projectId, Guid objectIdentityId);

    /// <summary>事务：建立 definition Asset link（owner 存在、Asset 同 Project 且完整性可用、role 为已声明 geometry role）。</summary>
    void LinkDefinitionAsset(LinkDefinitionAssetPlan plan);

    /// <summary>事务：移除 definition Asset link（不存在则冲突）。</summary>
    void UnlinkDefinitionAsset(Guid projectId, Guid objectIdentityId, Guid assetId, string role);

    // ---- Map Source Field Dataset / immutable Field Revision (ME-05-01) ----

    /// <summary>按 Map + stable field key 查找唯一 Map-owned Field Dataset。</summary>
    FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey);

    /// <summary>按 Dataset / Revision ID 查找不可变已发布 revision。</summary>
    FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldDatasetId, Guid fieldRevisionId);

    /// <summary>列出 Dataset 的全部已发布 revisions（revision_number 升序）。</summary>
    IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId);

    /// <summary>
    /// 原子发布 Field Revision：验证当前 revision CAS，插入不可变 revision 并推进 Dataset
    /// current pointer。首次发布在同一事务建立 Dataset；Field Payload 元数据与字节必须在同一事务完成登记。
    /// </summary>
    FieldRevisionRecord PublishFieldRevision(PublishFieldRevisionPlan plan);

    /// <summary>Find immutable payload metadata by its owning Field Revision.</summary>
    FieldPayloadRecord? FindFieldPayload(Guid projectId, Guid fieldRevisionId);

    /// <summary>List all payload metadata for one Project in stable identity order.</summary>
    IReadOnlyList<FieldPayloadRecord> ListFieldPayloads(Guid projectId);

    /// <summary>
    /// Update the integrity state after an authoritative byte verification pass.
    /// Callers may set Verified only after validating the complete payload bytes.
    /// </summary>
    void SetFieldPayloadIntegrityState(Guid projectId, Guid fieldPayloadId, FieldPayloadIntegrityState state);

    /// <summary>Read exact raw bytes for a SQLite BLOB payload; ManagedFile payloads are rejected.</summary>
    byte[]? ReadFieldPayloadBlob(Guid projectId, Guid fieldRevisionId);
}

/// <summary>Core Domain 数据损坏（行不可解析、不变量破坏、结构缺失）：fail-closed，不静默修补。</summary>
public sealed class CoreDomainDataException : Exception
{
    public CoreDomainDataException(string message)
        : base(message)
    {
    }

    public CoreDomainDataException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Core Domain 领域冲突（前置状态不满足、CAS 不匹配、重复分配/注册等）：
/// 语义化拒绝（区别于数据损坏与输入校验错误）；调用方可据此提示用户而非报 bug。
/// </summary>
public sealed class CoreDomainConflictException : Exception
{
    public CoreDomainConflictException(string message)
        : base(message)
    {
    }

    public CoreDomainConflictException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
