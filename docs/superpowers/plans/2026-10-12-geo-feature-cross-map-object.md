# Plan: GeoFeature 跨图地形对象落地

> 对应 spec：`docs/superpowers/specs/2026-10-12-geo-feature-cross-map-object.md`
> 本 plan 是 spec 的任务分解；执行前需用户确认 spec 的设计决策。

## 任务分解

### Task 1：数据契约落地（数据模型规格 + 存储规范）

**目标**：把 GeoFeature 字段定义写入《数据模型规格》§12，存储布局写入《存储与目录规范》§12。

**步骤**：

1. 《数据模型规格》新增 §12 GeoFeature（字段表 + YAML 示例），§11 变更记录追加一行
2. 《数据模型规格》§1 工程字段表新增 `geo_feature_index`
3. 《数据模型规格》§2 地图字段表新增 `geo_feature_refs`
4. 《存储与目录规范》§1 工程目录布局补 `geo_features/` 目录
5. 《存储与目录规范》§3 引用规则补 GeoFeature 的 `ref` 约定
6. 《存储与目录规范》§7 校验器补 GeoFeature 断链检测 + geometry CRS 校验
7. 《存储与目录规范》§11.1 `maps/` 目录即清单 补 `geo_features/` 同等约定

**验收**：

- [ ] GeoFeature 字段表完整（id/kind/name/geometry/style_per_scale/attributes/parent_feature_ref/source）
- [ ] YAML 示例可被校验器解析
- [ ] 存储布局图更新

**必读文档**：

- 数据模型规格 §1、§2、§2.10（坐标系统）
- 存储与目录规范 §1、§3、§7、§11.1

---

### Task 2：图层系统对齐（道路层 + 地形层渲染引用）

**目标**：图层系统规格 §2.4 道路层的几何数据改为引用 GeoFeature；§2.1 地形层的河/山脉渲染说明引用 GeoFeature。

**步骤**：

1. 图层系统规格 §2.4 道路层字段表补 `geo_feature_ref`（几何来源）
2. 图层系统规格 §2.1 地形层补说明：河/山脉的矢量渲染来自 GeoFeature，格级 `terrain_data.type` 用于寻路
3. 图层系统规格 §4 图层数据形态收敛 补 GeoFeature 引用约定

**验收**：

- [ ] 道路层不再双写几何（图层只引用，GeoFeature 承载）
- [ ] 地形层的河/山脉渲染路径清晰

**必读文档**：

- 图层系统规格 §2.1、§2.4、§4
- 数据模型规格 §12 GeoFeature

---

### Task 3：WBS 对齐（长期执行计划）

**目标**：长期执行计划 WBS 新增 GeoFeature 相关工作项，标注与现有 W-M1-3/W-M4-1 的依赖。

**步骤**：

1. 长期执行计划 §5 WBS 主表新增 P0-C0d（GeoFeature 数据契约落地）或插入 P1/P2 阶段
2. 更新 W-M1-3（anchor 跨图跳转）的验收列，补「河流/道路跨图一致性」
3. 更新 W-M4-1（城市/街区编辑）的验收列，补「GeoFeature LOD 渲染」
4. §12 变更记录追加一行

**验收**：

- [ ] WBS 新增行的 ST 状态列初始化为 `backlog`
- [ ] 依赖关系标注清晰

**必读文档**：

- 长期执行计划 §5 WBS 主表
- spec：`docs/superpowers/specs/2026-10-12-geo-feature-cross-map-object.md`

---

## 执行顺序

1. Task 1（数据契约）→ 2. Task 2（图层对齐）→ 3. Task 3（WBS 对齐）

Task 1 是前置（Task 2/3 依赖它的字段定义）；Task 2/3 可并行。

## Review Focus

- [ ] Task 1 的字段表是否与 spec 一致？
- [ ] Task 2 是否消除道路层双写？
- [ ] Task 3 的 WBS 行是否标注了依赖？

## 与现有工作的关系

- **不阻塞 P0-C0b**（新栈产物卫生）：GeoFeature 是纯设计变更，不影响当前代码
- **W-M1-3（anchor 跨图跳转）**：Task 1 完成后，河流/道路跨图一致性才有数据契约支撑
- **W-M2-1（高度值读写）**：GeoFeature 的 `mountain_range` 可复用高度数据，无需额外实现
