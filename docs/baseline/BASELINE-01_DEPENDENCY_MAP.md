# BASELINE-01 模块依赖与开发依赖图

- **基线**：origin/master `6ec34ea`

## 1. 运行时模块依赖（已验证的引用方向）

架构守卫测试 `WorldView.Presentation.Tests/Architecture/PresentationBoundaryTests.cs` 锁定：Presentation 公共 API 只允许依赖 Application，不得暴露 Infrastructure/UI 类型。

```
                    ┌─────────────────────────────┐
                    │   WorldView.Desktop         │
                    │  (Avalonia 12.1.6 + Skia)   │
                    │  Views / ViewModels /       │
                    │  Rendering (MapViewport-    │
                    │  Control/Binding/Renderer)  │
                    └──────┬──────────────┬───────┘
                           │              │
              (VM 用例编排) │              │ (呈现管线：Context→Request→
                           ▼              ▼  Prepare→Publish→Capture→Render)
        ┌──────────────────┐    ┌──────────────────────────┐
        │ WorldView.       │    │ WorldView.Presentation   │
        │ Application      │◄───│ (SceneSnapshot/Publisher/│
        │ UseCases/Sessions│    │  Coordinator/Assembler/  │
        │ Packaging/Fields │    │  Identity/Projection)    │
        └───────┬──────────┘    └──────────────────────────┘
                │ (Ports: IPackageDatabase, IAssetStore,
                │  ICoreDomainDatabase, IMapCreationService,
                │  IFieldPayloadFileStore …)
                ▼
        ┌──────────────────┐         ┌────────────────────┐
        │ WorldView.       │ ──────► │ WorldView.Domain   │
        │ Infrastructure   │ (实现端口)│ (零依赖领域模型)    │
        │ SQLite + EF Core │         │ Maps/Objects/      │
        │ 11 + Asset Store │         │ Registry/Geometry/ │
        └──────────────────┘         │ Assets/Fields/Time │
                                     └────────────────────┘
```

关键端口契约（Application 声明，Infrastructure 实现）：
- `IPackageDatabase` / `ISqliteConnectionProvider` ← `SqlitePackageDatabase` / `SqliteConnectionAccess`（单物理连接 + Monitor 临界区）
- `ICoreDomainDatabase` ← `SqliteCoreDomainDatabase`（EF Core 11，短生命周期 DbContext，`contextOwnsConnection:false`）
- `IAssetStore` / `IAssetStoreFactory` ← `FileAssetStore` / `FieldPayloadFileStore`
- `IUiDispatcher` ← `AvaloniaUiDispatcher`（R1 整改引入，Application 不依赖 Avalonia）

桌面呈现链路（PR #40 接通后的真实生产调用路径）：

```
ProjectWorkspaceViewModel（打开工作区）
  → PresentationContextFactory.FromValidatedWorkspace → 构建 PresentationContext
  → MapViewportBinding.RequestSceneAsync（请求身份 + _stateGate 原子判定）
  → ViewportPresentationRuntime（每视口私有 Coordinator+Publisher+Generation）
  → PresentationRequestCoordinator（latest-request-wins；DefaultBlankScenePreparationAsync 硬编码空白场景 ← 当前唯一断点环）
  → SceneSnapshotPublisher.TryPublish（六步校验 + Interlocked 原子发布）
  → MapViewportControl.Render → binding.CaptureFrame()（ADR-11 唯一捕获点）
  → MapDrawOperation（ISkiaSharpApiLeaseFeature lease）→ MapSceneRenderer.Draw（当前仅 Clear 背景）
  语义切换：MapWorkspaceSession.ContextSwitched → 重发请求（快照原子清空）
```

**断点标注**：链路中"数据→场景项→变换→Skia 图元"四环不存在；`DefaultBlankScenePreparationAsync` 是唯一内容来源且恒为空白。

## 2. 数据流依赖

```
.wvproject/ 目录包
 ├─ manifest.json ── ProjectPackageValidator（fail-closed 分阶段校验）
 ├─ project.sqlite ─ SqliteConnectionAccess（单连接）
 │    ├─ 包库表（bootstrap）
 │    └─ Core Domain 16+ 表（EF Core Migrations ×5）
 │         ├─ core_project/core_map/core_history/core_branch/core_calendar*
 │         ├─ core_object_type/core_object/core_object_definition/
 │         │   core_object_id_allocator/core_object_id_reservation
 │         ├─ core_map_object_ref/core_map_initial_state/core_map_initial_object
 │         ├─ core_asset/core_object_definition_asset
 │         ├─ core_map_field_dataset/core_map_field_revision
 │         └─ core_project_settings_* / core_map_settings_*
 ├─ Assets/sha256/ ──── FileAssetStore（内容寻址）        ⚠ 与下行双轨并存
 ├─ FieldPayloads/sha256/ ─ FieldPayloadFileStore          ⚠ 命名双轨（GAP G-11）
 ├─ Staging/ ─────────── staging 清理 + 孤儿收集
 └─ Backups/
```

Field Package（`.wvfld`，WVFLDPK 1.0）为独立交换通道：`FieldPackageWriter/Reader/IntegrityVerifier`（Application）↔ golden fixtures（docs/architecture/core-domain/fixtures/ 3 个）。

## 3. 开发依赖图（下一阶段工作的依赖关系）

```
【已完成门】
WP-RA-00 审计(PR#39) ──► WP-RA-02-A 切片A(PR#40 ✅) ──► ROADMAP(plan.md PR#42 ✅)
                                                      │
【在途门】                                             ▼
PR #41 Spatial Chunk & LOD 契约 ──评审(WP-RA-03)──► PRA-05 批渲染/Chunk/LOD
（BLOCKED：未评审未合并；PRA-05 的硬设计输入）

【主线串行链（渲染内容通路）】
WP-30C 视口导航(pan/zoom/DPI/输入)
   │  前置：管线已接线(✅)；无前置阻塞
   ▼
WP-40-B1 time-zero 内容投影 + 最小几何渲染
   │  前置：① WP-30C（相机变换真实化）② 几何读取投影（Core Domain 读路径）
   │        ③ 场景项类型体系（几何项+样式）④ Skia 图元绘制
   ▼
PRA-03 投影/裁剪/交互 ──► PRA-04 文本/标签 ──► PRA-05 Chunk/LOD/Batch（依赖 PR#41）
```

并行泳道（对齐 plan.md §7.2，门槛独立满足）：

| 泳道 | 工作包 | 与主线关系 |
|---|---|---|
| Lane A Core Domain | CMD-01 Command/Event/Checkpoint 引擎设计+实现；Relationship 模型；Map extent/bounds ADR | 可与 Lane B 并行；是"任意历史时间渲染"的硬前置，但不阻塞 time-zero 渲染 |
| Lane B 渲染器基础 | WP-30C → WP-40-B1 → PRA-03 | 主线 |
| Lane C UX/UI | UI-04C 独立工作区窗口；图层面板/属性检查器设计（Phase 1 UX 旅程先行） | UI-04C 可与 WP-30C 并行（窗口壳 vs 视口内容）；面板 UI 需先有 Phase 1 旅程设计 |
| Lane D Field/地形 | TERRAIN-01（Field Dataset 已 IMPLEMENTED，可直接消费） | 独立 |
| Lane E 验证基础设施 | HEADLESS-01 Avalonia Headless UI 测试；SMOKE-01 打包+GUI 冒烟；macOS 本机验收 | 可立即并行，与一切无耦合 |

强制串行点：
1. **PR #41 评审裁决 → 合并 → PRA-05 开工**（plan.md:568 明确）。
2. **WP-30C → WP-40-B1**：内容投影依赖相机变换真实化，否则渲染器无坐标可用。
3. **接口冻结后并行、集成串行**：场景项类型体系（SceneItem 子类 schema）必须在 WP-40-B1 开工前冻结，因为它同时被 Lane A（几何读取投影产出物）与 Lane B（渲染消费物）共享——这是最重要的公共接口冻结点。
4. 命令引擎（CMD-01）落地后，编辑器垂直切片（EDITOR-01）才能消费；undo/redo（ADR-004）依赖命令回放（ADR-003）。

## 4. PR 与分支状态图

```
master @ 6ec34ea
  ├── PR #40 feat/wp-ra-02-a-desktop-presentation-integration  ✅ merged（含 R1/R2/R3 整改+独立复核 92db5c8）
  ├── PR #42 docs/world-view-development-roadmap               ✅ merged（plan.md）
  ├── PR #41 docs/wp-ra-03-spatial-chunk-lod-contract          ⛔ OPEN（2 commits ahead：契约 576 行 + ME-07 回链；待评审）
  └── 本地分支 feat/wp-ui-04b-map-session-lifecycle @ 0479ff5   已全部并入 master（无独有提交，可清理）
```

其他远程分支（均已合并或陈旧）：feat/efcore-v1-*, feat/jcs-canonical-json, feat/me05-*, feat/ui-02-*, feat/ui-foundation-shell, feat/wp-30b-*, fix/wp-30b-p1-review-fixes 等，`git branch -r` 全列表见审计过程；建议归并后清理（不属本审计执行范围）。
