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
/// 结构写操作（InsertProject / CreateMapStructure / CreateBranchStructure）与 Object
/// 写路径（Reserve/Bind/Insert/Definition CAS/注册/Initial State CAS）各自构成一个
/// 原子事务，失败整体回滚，不留半成品（事务边界归实现方，AF-02 端口契约；
/// CD-02-A §13.2 写路径事务表）。所有复合写在实现方的统一连接协调器临界区内
/// 完成事务内前置状态重读校验（CD-01-F1 条件 1 响应，CD-02-A §13.3）。
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

    /// <summary>列出指定 Project 的全部 Map（按创建时间、ID 稳定排序）。</summary>
    IReadOnlyList<MapRecord> ListMaps(Guid projectId);

    /// <summary>
    /// 事务：一次写入 Map 创建计划的全部结构（Map、根 History、default Calendar、
    /// day-zero Configuration）；任一失败整体回滚（P2-09-11 §4.1 不变量）。
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
    /// →Inserted + definition 首行 + 可选 Map 注册 + 可选 Initial State 成员（含基线
    /// 建立与指纹重算）。事务内重读校验前置状态；失败整体回滚、无半成品。
    /// </summary>
    void InsertObject(ObjectInsertPlan plan);

    /// <summary>
    /// 事务：Definition CAS 编辑——事务内比对 expected hash 与当前行（不匹配抛
    /// <see cref="CoreDomainConflictException"/>），重写 payload/hash/version。
    /// 无 revision 表（P2-09-16 §8）。
    /// </summary>
    void EditObjectDefinition(ObjectDefinitionEditPlan plan);

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
    /// 事务：取消注册——该 Map 上存在 Initial State 成员行时拒绝（P3⇒P2 不变量，
    /// 须先经 Initial State 编辑移除成员）；无成员行则单行删除。
    /// </summary>
    void UnregisterMapObject(Guid projectId, Guid mapId, Guid objectIdentityId);

    // ---- Map Initial State（P3 存在层；CD-02-A §10） ----

    /// <summary>查找 Map 的 Initial State 基线；不存在返回 null（每 Map 至多一行）。</summary>
    MapInitialStateRecord? FindMapInitialState(Guid projectId, Guid mapId);

    /// <summary>列出基线的全部成员行（按 identity 稳定排序）。</summary>
    IReadOnlyList<InitialObjectRecord> ListInitialObjects(Guid projectId, Guid mapId);

    /// <summary>
    /// 事务：Initial State 编辑（CAS 全量重写）——基线存在时 expected fingerprint 必须
    /// 匹配当前值（不匹配抛 <see cref="CoreDomainConflictException"/>）；不存在时仅
    /// expected 为 null 可建立。成员全量替换 + 指纹由 Domain 重算写入。成员必须有
    /// 对应注册行（P3⇒P2）且身份 active；校验在事务内完成。
    /// </summary>
    void EditMapInitialState(EditInitialStatePlan plan);

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

    /// <summary>事务：更新完整性扫描状态（corrupt/missing 状态阻止新建正式引用）。</summary>
    void UpdateAssetIntegrityState(Guid projectId, Guid assetId, AssetIntegrityState integrityState, long nowUtcMs);

    /// <summary>列出未被任何 typed link 引用的 Asset ID（孤儿对账；只读，不自动删除）。</summary>
    IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId);

    // ---- Map Source Field Baseline（ME-05-01；Map-wide and retroactive） ----

    /// <summary>读取 Map + field_key 的唯一正式 Field Dataset；不存在返回 null。</summary>
    FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey);

    /// <summary>读取 Field Dataset 当前修订；Dataset 不存在或尚无修订时返回 null。</summary>
    FieldRevisionRecord? FindCurrentFieldRevision(Guid projectId, Guid mapId, string fieldKey);

    /// <summary>按稳定 ID 读取不可变 Field Revision；不存在或跨 Project 返回 null。</summary>
    FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldRevisionId);

    /// <summary>按 Dataset 读取全部不可变修订，按 revision_number 升序。</summary>
    IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId);

    /// <summary>
    /// 原子发布：创建/复用 Dataset，验证 expected-current CAS，在同一事务插入不可变
    /// Revision 并推进同 Dataset current pointer。Asset 必须已 Published + Verified。
    /// 首次发布 expected=null；后续发布必须传入当前 revision ID。失败不留半成品。
    /// </summary>
    FieldRevisionRecord PublishFieldRevision(FieldRevisionPublishPlan plan);

    // ---- Definition Asset typed link（core_object_definition_asset；P2-09-19 §9） ----

    /// <summary>按 (owner, asset, role) 查找 definition Asset link；不存在返回 null。</summary>
    ObjectDefinitionAssetLinkRecord? FindDefinitionAssetLink(Guid projectId, Guid objectIdentityId, Guid assetId, string role);

    /// <summary>列出 Object 的全部 definition Asset links（按 asset_id/role 稳定排序）。</summary>
    IReadOnlyList<ObjectDefinitionAssetLinkRecord> ListDefinitionAssetLinks(Guid projectId, Guid objectIdentityId);

    /// <summary>事务：建立 definition Asset link（owner 存在、Asset 同 Project 且完整性可用、role 为已声明 geometry role）。</summary>
    void LinkDefinitionAsset(LinkDefinitionAssetPlan plan);

    /// <summary>事务：移除 definition Asset link（不存在则冲突）。</summary>
    void UnlinkDefinitionAsset(Guid projectId, Guid objectIdentityId, Guid assetId, string role);
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
