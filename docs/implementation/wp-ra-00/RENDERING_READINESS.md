# WP-RA-00 — WP-40 完整只读地图渲染就绪度（RENDERING_READINESS）

基线：`20c6fcca`。分级：✅ 已实现且有测试证据 · 🟡 部分实现 · 📄 仅设计文档 · ❌ 尚未设计 · ⛔ 被正式领域能力阻塞。

依据：`docs/map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md`（§18 PRA 序列）、`docs/map-editor/implementation-roadmap-v1.md`（WP-40 序列 1–8）、ME-07。

---

## 1. WP-40 逐项分级（roadmap §5 WP-40 序列 1–8 + 架构能力面）

| # | 能力项 | 状态 | 依据与缺口 |
|---|---|---|---|
| 1 | PRA-03 投影适配器、坐标变换、空间索引、视口剔除 | ❌（投影/索引/剔除）+ 📄（合同） | `PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md` §4 定义了 typed 坐标空间与投影服务合同；代码为零。M1 视口变换合同（Profile §3.3）未实现（WP-30C 缺失）。**部分被阻塞**：真实 ProjectionDefinition 权威不存在（`NotConfigured` 占位），但 Local Cartesian 视口变换不被阻塞 |
| 2 | 渲染项/场景图层组合与可见性排序 | 🟡 | `SceneItems.cs`/`SceneLayers.cs` 只有图层骨架 + 空白背景项；无几何绘制项、符号项、文本项 schema。装配器排序权威已实现（背景 Order 0 + 显式可选层） |
| 3 | PRA-04 文本整形与标签布局 | 📄 | HarfBuzz 单一权威、禁手搭 GlyphRun（TV-01 证据约束 §8.3/§16）已定；代码为零 |
| 4 | 符号、样式、装饰、尺度表达 | 📄 + ⛔ | 样式只有 token 占位（`PresentationTokens.BuiltinNeutralStyle`）；装饰系统合同 §9 已定；**被阻塞**：Map extent/bounds 不存在（比例尺无地距可依） |
| 5 | PRA-05 LOD、批渲染、缓存失效 | 📄 | TV-01 性能证据（50K 图元 ≈92ms/帧）已记录为警示；代码为零 |
| 6 | PRA-07 栅格/Field Package 可视化 | 🟡（前置已备） | 前置"parser/writer 与 Field Dataset 读取路径验证"已满足（`Cd02FieldDatasetPersistenceTests` 43 项 + FieldPackage 系列测试）；可视化本身零代码。地图内容投影依赖 Field Revision 的空间语义读取（当前只有原始行） |
| 7 | PRA-06 确定性动画呈现 | ⛔ | 明确前置"权威 Historical Time / Unit movement 输入可用"——Command/Event/移动描述符均不存在。**全阻塞** |
| 8 | Export/Layout | ⏳ 依赖场景语义稳定 | 不阻塞、也不被阻塞；按合同排在最后 |

### 横切面补充

| 能力面 | 状态 | 说明 |
|---|---|---|
| 数据读取边界（Application 只读投影） | ❌ | 当前 `IMapQueryService` 仅 `MapSummary`；无地图内容投影端口 |
| Presentation Context | ✅（M1）/ 🟡（完整） | 上下文/指纹/发布机制完成；`WorldStateRevision.Evaluated` 与 `ProjectionDefinition.Configured` 分支存在但无真实数据源 |
| 几何 Presentation | ❌ | Domain 侧几何经 Asset 附着于 Object Definition（已持久化、已测试），Presentation 侧无对应绘制项与转换 |
| Hit Testing / 空间查询 | 📄 | PRA 合同 §5.2/5.3 已定；代码为零 |
| 大场景性能 | 📄 | 只有 TV-01 警示数据；无生产基线 |
| 资源生命周期 | ✅（M1 空集）/ ❌（真实） | M1 零长期 Skia 资源；真实资源缓存/设备重建合同 §11.3 未实现 |
| Windows/macOS 行为 | ⛔ 验收缺位 | CI 4/4 通过但为 headless；Phase 3A 人工验收清单未执行记录 |

## 2. 阻塞项汇总（按严重度）

| 级别 | 阻塞项 | 阻塞哪些 WP-40 目标 | 解除条件 |
|---|---|---|---|
| **P0** | 场景请求管线未接线（`RequestSceneAsync` 生产零调用） | 一切端到端渲染 | 桌面工作区打开流程接入 Context 捕获 + 场景请求（见 NEXT_IMPLEMENTATION_SLICE 切片 A） |
| **P0** | 按 Map/Branch/HistoricalTime 的地图内容读取投影不存在 | 渲染任何正式内容 | 新建 Application/CoreDomain 读投影服务；**time-zero 子集不被阻塞** |
| **P1** | Command/Event/Checkpoint 历史重建缺失 | 任意历史时间渲染、PRA-06 动画 | 按 ADR-002/003 实现 Command/Event 持久化与重建读模型（正式领域工程，体量大，单列） |
| **P1** | Map extent/bounds 未设计 | 地图框、默认相机、比例尺、装饰 | 独立 ADR（PRA-01 Profile §5.2 明令 M1 不加，需显式解禁） |
| **P2** | 样式/符号/字体系统未实现 | 视觉质量、标签 | M1 token 基线可先支持单一样式渲染 |
| **P2** | WP-30C pan/zoom/DPI 变换缺失 | 视口导航、M1 十二项验收 | 小切片，不依赖新领域能力 |

## 3. 判定

**WP-40 不能以"完整只读渲染"整体启动。** 两个 P0 中：管线接线是纯集成工作（低风险、有现成测试保护）；地图内容读取投影是正式领域能力工程。二者合并为 WP-40 第一切片（见 `NEXT_IMPLEMENTATION_SLICE.md`），其余各项按依赖逐步解锁。
