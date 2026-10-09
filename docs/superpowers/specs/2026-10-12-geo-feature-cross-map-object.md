# Spec: GeoFeature 跨图地形对象（方案 D 落地）

> 来源：用户讨论「地图上多地图层级操作」→ 疑问「武汉式城市中间有河，如何在世界图→城市图间保持一致」→ 讨论后选定方案 D「跨图引用对象」。
> 本 spec 是**数据契约变更**的实现计划草案；正式落地需按 AGENTS §4 事实来源优先级评审后写入《数据模型规格》并升版本号。

## 背景

当前数据模型中，地形（`terrain_data`）是**每张地图独立持有**的网格数组。河/道路/山脉这类**跨尺度线性对象**若在多级地图（世界→区域→城市→室内）各画一遍，会产生：

1. 数据重复（河在 3 张图里画 3 次）
2. 一致性漂移（改了城市图的河，世界图不知道）
3. LOD 断裂（河在不同图的宽度/走向无法协调）

`Route`（运输路线，数据模型规格 §9）已有跨图能力（`waypoints` + `child_map_refs`），但它是**运输语义对象**，不是纯地形对象。

## 目标

新增 **GeoFeature（地理特征）**对象：工程级、跨地图、一次定义、多图引用 + LOD 渲染。

## 不在本轮

- 不改 `Route` 的现有语义（运输路线保持独立）
- 不实现渲染/编辑器（仅数据契约 + 存储布局 + 校验规则）
- 不处理时间有效性（`valid_from/valid_to` 留待 M3/M6 与图层系统对齐）

## 设计决策（已定，用户委托 AI 决定）

### D-Geo-1：对象模型

新增 `GeoFeature`，字段草案：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | ✔ | 稳定标识（英文小写下划线），如 `river_yangtze` |
| `kind` | enum | ✔ | `river` / `road` / `mountain_range` / `coastline` / `canal` / `ridge`（开放枚举，v0.1 先支持 `river`/`road`/`mountain_range`） |
| `name` | string | | 显示名 |
| `geometry` | object | ✔ | 几何定义，见下 |
| `style_per_scale` | map<enum, object> | | 各 LOD 层级的渲染样式（`width`/`color`/`stroke`/`fill`），键为地图类型或缩放区间 |
| `attributes` | map<string, any> | | 开放属性（如河的 `flow_direction`、道路的 `road_type`） |
| `parent_feature_ref` | ref | | 父级 GeoFeature（如支流 → 主流），支持树状水系 |
| `source` | enum | | 数据来源：`manual` / `generated` / `imported`（默认 `manual`） |

**geometry 结构**：

```yaml
geometry:
  type: polyline        # polyline | polygon | point
  crs: world            # 坐标系统一用「世界坐标」（数据模型规格 §2.10 四层坐标的第一层）
  vertices:             # 折线/多边形顶点（世界坐标）
    - { x: 120.5, y: 45.2 }
    - { x: 121.0, y: 44.8 }
  lod_simplification:
    method: douglas_peucker   # 简化算法（可配）
    tolerance_per_scale:      # 各层级容差（可配）
      world: 5.0
      region: 2.0
      city: 0.5
```

### D-Geo-2：数据归属（工程级）

`GeoFeature` 为**工程级对象**，不归属任何单张地图。存储布局（存储与目录规范）：

```
world-view-project/
├── project.yaml
├── geo_features/                    # 新增目录
│   ├── river_yangtze.yaml
│   ├── road_silk.yaml
│   └── ...
├── maps/
│   ├── world/main.yaml
│   ├── city/port_aurora.yaml
│   └── ...
```

`project.yaml` 新增 `geo_feature_index`：

```yaml
geo_feature_index:
  - ref: geo_features/river_yangtze.yaml
  - ref: geo_features/road_silk.yaml
```

各地图通过 `Map.geo_feature_refs` 引用（只引用在本图 `extent` 范围内的）：

```yaml
# maps/city/port_aurora.yaml
geo_feature_refs:
  - ref: geo_features/river_yangtze.yaml
    visible: true
    layer_id: layer_terrain    # 映射到本图哪个图层渲染
```

### D-Geo-3：LOD 渲染策略

各地图按自身 `grid.cell_distance` 与地图类型自动选择 LOD 层级：

| 地图类型 | 典型 cell_distance | LOD 层级 | 河渲染 |
|---|---|---|---|
| `world` | 10 km | `world` | 粗线（宽度 2–4 px） |
| `region` | 1 km | `region` | 中线 |
| `city` | 100 m | `city` | 宽面（带河岸） |
| `building_indoor` | 1 m | `indoor` | 不显示（超出范围） |

LOD 层级判定规则：`Map.type` → 映射到 `style_per_scale` 的键（可覆盖）。

**简化算法**：Douglas-Peucker（可配算法与容差，见 geometry.lod_simplification）。

### D-Geo-4：与 Route / 道路层的关系

- **`Route`（运输路线，§9）保持独立**：有运输语义（`waypoints` + 载具 + 时间计算），不合并。
- **道路层（图层系统规格 §2.4）升级**：现有道路层的 `road_type`/`width` 字段保留，但**几何数据改为引用 `GeoFeature`**（`kind: road`），避免道路在图层数据与 GeoFeature 双写。
- **`road_layer_ref`（Map.travel）**：继续引用图层，图层内部引用 GeoFeature 几何。

### D-Geo-5：编辑与撤销

- 编辑 GeoFeature 几何 → 所有引用它的地图**自动反映**（无需同步）
- 撤销/重做：GeoFeature 的 undo **不跨图传播**（每张地图的视图状态独立）；几何修改本身在工程级 undo 栈

## 风险

| 风险 | 概率 | 影响 | 对策 |
|---|---|---|---|
| LOD 简化后河走向在城市图不准 | 中 | 中 | `lod_simplification.tolerance_per_scale` 可调；城市图用 `city` 级容差（0.5） |
| GeoFeature 几何过大（如长江全流域） | 中 | 中 | 支持 `parent_feature_ref` 树状拆分（主干+支流）；或分段存储 |
| 与现有 `terrain_data` 的 `river` 地形类型冲突 | 低 | 低 | `terrain_data` 的 `type: river` 是**格级属性**（地形分类），GeoFeature 是**矢量对象**，二者并存：格级属性用于寻路/移动消耗，矢量对象用于渲染/编辑 |

## 实施步骤

1. **数据契约**（本 spec）→ 评审后写入《数据模型规格》§12 GeoFeature（升 v0.1 补充）
2. **存储布局** → 写入《存储与目录规范》§12 geo_features/ 目录 + `geo_feature_index`
3. **图层系统** → 图层系统规格 §2.4 道路层 + §2.1 地形层的河/山脉渲染改为引用 GeoFeature
4. **校验规则** → 存储与目录规范 §7 校验器补：`geo_feature_refs` 断链检测、geometry CRS 校验
5. **WBS 对齐** → 长期执行计划 WBS 新增 `W-GEO-1`（数据契约落地）+ `W-GEO-2`（LOD 渲染）+ `W-GEO-3`（编辑器集成）

## Review Focus

- [ ] GeoFeature 字段是否覆盖河/道路/山脉三类？
- [ ] 工程级归属（vs 地图级）是否合理？
- [ ] 与 Route/道路层的边界是否清晰？
- [ ] LOD 简化算法选择（Douglas-Peucker）是否够用？

## 与长期执行计划的关系

本 spec 落地后，`W-M1-3`（anchor 跨图跳转）与 `W-M4-1`（城市/街区编辑）的「河流/道路跨图一致性」验收将依赖 GeoFeature；`W-M2-1`（高度值读写）与 GeoFeature 的 `mountain_range` 可共用高度数据。
