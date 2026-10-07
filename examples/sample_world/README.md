# 示例工程：艾瑟兰大陆

> **重要声明（必读）**：本示例工程的 **人物 / 事件文件**（`characters/*.yaml`）属于**数据契约样例**，
> MVP 阶段**不加载到 UI**，不在主界面 / 图层面板 / 城市卡片中出现。
> 人物编辑面板 / 关系网 UI / 时间轴联动将在 **M5 完整人物登记系统**中落地。

## 工程结构

```
examples/sample_world/
├── project.yaml                          # 工程元数据 + map_index + character_index
├── maps/
│   ├── world/world_kael.yaml            # 艾瑟兰大陆（64×48，世界地图）
│   ├── world/world_kael.terrain.yaml    # world 地形（grid 编码，3072 cells）
│   ├── city/city_kael.yaml              # 极光港（64×64，城市地图，2 个建筑占位）
│   └── city/city_kael.terrain.yaml      # 城市地形（sparse，全平原）
├── layers/
│   ├── terrain.yaml                     # 基础地形图层（z_order=0）
│   ├── road.yaml                        # 道路图层（z_order=10）
│   ├── building.yaml                    # 建筑图层（z_order=20）
│   └── marker.yaml                      # 标记图层（z_order=30）
└── characters/
    ├── _templates.yaml                  # 属性模板集合（数据契约样例）
    └── lin_yu.yaml                      # 林语（数据契约样例，M5 落地）
```

## 加载方式

1. 编译 .NET 解决方案：
   ```
   dotnet build WorldView.sln
   ```
2. 启动 Godot 4.7.2 mono 编辑器：
   ```
   "C:\App\Godot_v4.7.2-stable_mono_win64\Godot.exe" --path D:\kudi-project\world-view
   ```
   或在 Godot 编辑器中按 F5 运行主场景 `res://scenes/Main.tscn`。
3. 顶栏「文件 → 打开示例工程」选本目录 `examples/sample_world/`；
4. 看到世界地图渲染，「极光港」城市标记可见，双击即可下钻到 `city_kael`。

## MVP 验收点对照

| Spec §7 demo | 验收内容 | 期望 |
|---|---|---|
| D1 | 启动 + 打开示例工程 + 世界地图渲染 | 画布显示 64×48 网格 + 山地 / 河流 / 森林色块 + 极光港标记 |
| D2 | 工具栏切到「笔刷」+ 选 forest + 画 20 格 | 对应格子变 forest 色（参见 R-BRUSH-02 / R-BRUSH-03） |
| D3 | Ctrl+Z | 刚刷的格子还原为 plain |
| D4 | Ctrl+Shift+Z | 撤销的格子恢复为 forest |
| D5 | 双击极光港标记 | 切到城市地图，顶栏面包屑「世界 / 极光港」 |
| D6 | 工具栏切到「建筑」+ 选 house + 在 buildable 格点击 | 弹「建筑占位」对话框，确认后建筑出现在 at 处 |
| D7 | 「保存」→ 关闭 → 重开 | map.buildings 含新建筑，字段一致 |
| D8 | 手动改 `maps/city_kael.yaml` 的 `grid.width` 为 `-1` → 重启 | 弹错误对话框，显示字段路径 + 错误码 |

## 数据契约遵循

- 字段定义以 `docs/数据模型规格_v0.1.md` 为唯一真源；
- 不使用 `encoding: chunked`（MVP 不引二进制分块）；
- `terrain.height` 字段省略（height_data MVP 不读写）；
- `character_index` 仅作数据契约样例，前端不订阅；
- `ref` 相对工程根，禁止 `..` 与绝对路径；
- `id` 与 `ref` 严格区分（同对象同文件内用 `id`，跨文件用 `ref`）。
