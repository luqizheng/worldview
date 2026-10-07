# 示例工程：艾瑟兰大陆

> **重要声明（必读）**：本示例工程的 **人物 / 事件文件**（`characters/*.yaml`）属于**数据契约样例**，
> MVP 阶段**不加载到 UI**，不在主界面 / 图层面板 / 城市卡片中出现。
> 人物编辑面板 / 关系网 UI / 时间轴联动将在 **M5 完整人物登记系统**中落地。

## 工程结构

```
examples/sample_world/
├── project.yaml                          # 工程元数据 + map_index + character_index
├── maps/
│   ├── world/main.yaml                  # 艾瑟兰大陆（64×48，世界地图）
│   ├── world/main.terrain.yaml         # world 地形（grid 编码，3072 cells）
│   ├── city/port_aurora.yaml           # 极光港（64×64，城市地图骨架）
│   └── city/port_aurora.terrain.yaml   # 城市地形（sparse，全平原）
└── characters/
    ├── _templates.yaml                 # 属性模板集合（数据契约样例）
    └── lin_yu.yaml                     # 林语（数据契约样例，M5 落地）
```

## 加载方式

1. 启动 Tauri 桌面应用（待 MSVC 安装：`pnpm tauri dev`）；
2. 「打开示例工程」选本目录 `examples/sample_world/`；
3. 看到世界地图渲染，「极光港」城市标记可见，点击即可下钻。

## MVP 验收点对照

| Spec §7 demo | 验收内容 | 期望 |
|---|---|---|
| D1 | 启动 + 打开示例工程 + 世界地图渲染 | 画布显示 64×48 网格 + 山地 / 河流 / 森林色块 + 极光港标记 |
| D2 | 滚轮缩放 / 拖拽 / F / G | 视口响应正确 |
| D7 | 关闭应用 → 重开 → 修改保留 | save_project 落盘正确（YAML 地形 sparse 编码） |
| D8 | 删 port_aurora.yaml → 校验报错 | 错误对话框弹出，写盘取消 |

## 数据契约遵循

- 字段定义以 `docs/数据模型规格_v0.1.md` 为唯一真源；
- 不使用 `encoding: chunked`（MVP 不引二进制分块）；
- `terrain.height` 字段省略（height_data MVP 不读写）；
- `character_index` 仅作数据契约样例，前端不订阅。