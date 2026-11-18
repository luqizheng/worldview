# World View Rebuild — Architecture Baseline V1.0

- **项目**：World View
- **阶段**：Rebuild
- **版本**：V1.0
- **日期**：2026-11-18
- **状态**：Architecture Baseline
- **性质**：全新实现基线
- **旧系统关系**：旧系统仅作为参考，不作为新实现基础
- **实施原则**：先架构、后数据模型、后核心引擎、后 UI

---

# 1. 重建决定

World View 从现有代码基础上重新实现。

现有系统代码目前存在较高复杂度与历史设计累积问题，因此：

> **不在旧代码上继续修补。**

新系统采用独立的数据模型、领域模型、服务层和 UI 实现。

旧系统只作为：

- 历史设计参考；
- 已有功能参考；
- 数据迁移研究材料；
- UI / 交互参考。

旧代码中的业务模型、数据库 Schema、API、Service、版本体系等：

> **均不得默认继承。**

是否重新实现某项能力，由新架构单独决定。

---

# 2. 产品核心

World View 是一个：

> **可以随历史时间演进，并允许从任意历史时点产生不同历史分支的世界地图创作系统。**

核心不是：

> 版本管理。

也不是：

> Git。

而是：

> **历史世界状态的创建、变化、播放与分叉。**

---

# 3. 核心概念

新系统第一层只建立以下核心概念：

```text
Map
World State
Historical Time
Event
State Change
Timeline
Branch
Object
```

其中：

```text
Map
 ├── Objects
 └── History
      ├── Event
      └── State Change
```

Branch 建立在 Map History 之上。

---

# 4. Map

Map 是 World View 最核心的创作对象。

一张 Map 表示：

> **一个可以随历史时间发展的世界地图。**

例如：

> 魔法王国 · 1000 年

地图可以包含：

- Terrain
- Boundary
- POI
- Character
- Unit
- Track
- 其他未来世界对象

---

# 5. Map 初始状态

创建地图时，用户建立一个初始世界状态。

例如：

```text
Map:
魔法王国

Initial Time:
1000

Initial State:
- 山脉
- 河流
- 国境
- 城市
- POI
- 人物
- Unit
```

这个初始状态就是历史的起点。

不额外创建：

- World Version 0
- Release 0
- Revision 0

产品层面只需要：

> **地图的初始历史状态。**

---

# 6. Historical Time

World View 的核心时间是：

> **世界历史时间（Valid Time）**

例如：

```text
1000
1050
1100
1200
1300
```

它描述：

> 世界中的事情什么时候发生，以及世界状态什么时候生效。

地图播放使用 Historical Time。

---

# 7. Event

Event 表达：

> **发生了什么。**

例如：

```text
1200
魔法大战
```

Event 可以包含：

- 时间；
- 地点；
- 参与者；
- 描述；
- 相关对象；
- 结果；
- 相关 State Change。

Event 是世界历史的叙事表达。

---

# 8. State Change

State Change 表达：

> **世界状态发生了什么变化。**

例如：

```text
1200
山脉 → 平地
```

```text
1200
国境 A → 国境 B
```

```text
1200
POI 名称：
白石城 → 圣都
```

```text
1200
国家：
王国 A → 灭亡
```

State Change 是历史地图状态变化的核心机制。

---

# 9. Event 与 State Change

Event 和 State Change 是两个不同概念。

## Event

回答：

> **发生了什么？**

## State Change

回答：

> **世界变成什么样？**

二者可以关联。

例如：

```text
Event:
魔法大战

        ↓

State Change:
山脉被夷平

State Change:
国境线改变

State Change:
皇都改名
```

一个 Event 可以产生多个 State Change。

---

# 10. State Change 不要求 Event

State Change 可以独立存在。

例如：

```text
1650
城市改名：
白石城 → 圣都
```

不需要创建一个：

> 城市改名 Event

只有当作者希望描述：

> 国王发布法令，宣布城市改名。

才需要建立 Event。

因此：

> **Event 不是 State Change 的父对象。**

---

# 11. State Change 时间

State Change 支持：

### 11.1 生效时间

例如：

```text
1940 → 国境 B
```

系统可以根据前后变化推导状态区间。

例如：

```text
1935–1940 → 国境 A
1940–1950 → 国境 B
1950–∞    → 国境 C
```

### 11.2 有效区间

需要时可以明确：

```text
1000–1940
山脉
```

```text
1940–∞
平地
```

因此：

> **时间点是默认表达，有效区间用于需要明确区间的场景。**

---

# 12. World State

World State 是：

> **某个 Map 在某个 Historical Time 下的最终世界状态。**

概念上：

```text
World State =
Initial State
+
所有截至指定时间有效的 State Change
```

例如：

```text
Map = 魔法王国
Time = 1200
```

系统计算：

```text
1000 初始状态
+
1050 State Change
+
1100 State Change
+
1200 State Change
=
1200 World State
```

---

# 13. 时间播放

地图播放的本质是：

> **连续改变 Historical Time，并重新计算 / 应用对应 World State。**

例如：

```text
1000
 ↓
1050
 ↓
1100
 ↓
1150
 ↓
1200
```

用户可以看到：

- 地形变化；
- 国境线变化；
- POI 状态变化；
- 人物变化；
- Unit 移动；
- 其他历史变化。

---

# 14. Branch

Branch 表示：

> **从某个历史时点开始，创造另一条历史。**

例如：

```text
1000 → 1050 → 1100 → 1200
                         │
                         ├── 主线
                         │
                         └── 分支 A
```

分支不是软件开发分支。

它代表：

> **另一条世界历史。**

---

# 15. Branch 创建

用户可以在任意历史时间创建 Branch。

例如：

```text
播放到：

1200

用户：

Create Branch
```

产生：

```text
Branch A
parent_map = Main Map
parent_at = 1200
```

---

# 16. Branch 不复制祖先历史

Branch 不复制：

```text
1000
1050
1100
1200
```

这些历史仍然来自父 Map。

Branch 只保存：

```text
parent_map
parent_at
own_history
```

例如：

```text
Main Map
1000 → 1050 → 1100 → 1200
                         │
                         └── Branch A
                              1230
                              1280
```

---

# 17. Branch 播放

播放 Branch A：

```text
Parent History:
1000 → 1050 → 1100 → 1200

+

Branch History:
1200 → 1230 → 1280
```

得到：

```text
1000 → 1050 → 1100 → 1200 → 1230 → 1280
```

因此 Branch 是一条完整可播放的历史。

---

# 18. 父历史传播

Branch 创建后：

> **分叉点之前的父历史仍然属于父 Map。**

因此父 Map 修改祖先历史：

```text
1100
旧：国王 A
新：国王 B
```

Branch A 播放到 1100 时：

> 也显示国王 B。

原因：

> Branch A 没有复制 1100 的历史。

---

# 19. 分叉之后独立

分叉点之后：

```text
Main:
1200 → 1250 → 1300

Branch:
1200 → 1230 → 1280
```

两者完全独立。

Main 的 1250 State Change 不影响 Branch。

Branch 的 1230 State Change 不影响 Main。

---

# 20. Merge

World View：

> **禁止 Merge。**

不实现：

- Merge；
- Merge Conflict；
- Conflict Resolution；
- Rebase；
- Cherry-pick；
- Branch reconciliation。

原因：

不同分支代表不同的创作历史。

例如：

```text
历史 A：
王国统一

历史 B：
王国灭亡
```

两者不存在合理的“合并”。

---

# 21. Branch 删除

用户可以删除 Branch。

删除 Branch：

- 删除该 Branch 自己的历史；
- 不影响父历史；
- 不影响其他 Branch。

---

# 22. Branch 废弃

用户可以把 Branch 标记为：

> **废弃**

废弃：

- 不删除数据；
- 保留完整历史；
- 可以查看；
- 可以作为参考；
- 默认不作为当前主要创作方向；
- 可以恢复。

---

# 23. Copy Map

Copy Map 与 Branch 是两个不同操作。

## Copy Map

用户复制一张地图。

目的可以是：

- 备份；
- 尝试新的创作方向；
- 完全独立创作。

Copy Map 不等于 Branch。

---

# 24. Shared Objects

多个 Map 可以引用同一个对象。

例如：

```text
Map A
  ↓
POI: 白石城

Map B
  ↓
POI: 白石城
```

两个 Map 引用同一个 POI。

如果共享 POI 被修改：

> 所有引用它的地图都能看到修改。

---

# 25. 对象独立化

如果用户希望某张地图中的 POI 独立发展：

```text
Copy POI
   ↓
New POI
   ↓
Map 使用 New POI
```

然后删除原来的引用。

这样：

> 新 POI 与原 POI 不再共享后续变化。

不需要对象 Merge。

---

# 26. Shared Object History

共享对象可以拥有自己的历史。

例如：

```text
POI:
白石城

1600：
白石城

1700：
圣都
```

这是共享对象本身的历史。

---

# 27. Map-local History

Map 可以对共享对象建立局部历史。

例如：

共享 POI：

```text
1600：
白石城

1700：
圣都
```

Map B：

```text
1650：
魔法帝都
```

Map B 可以拥有：

> 自己的局部历史设定。

局部历史：

- 属于当前 Map；
- 不修改共享对象；
- 不跨 Map 引用。

---

# 28. Shared History + Local History

最终状态计算可以理解为：

```text
Shared Object State
        ↓
Map Local State Change
        ↓
Historical Time
        ↓
Final Map State
```

地图可以自由创作与共享历史不同的设定。

系统可以表达这种差异，但不能因为冲突而阻止创作。

---

# 29. 不建立跨地图 Local History 引用

Map A 的局部历史：

> 只属于 Map A。

Map B 如果需要类似内容：

> 用户自行复制相关对象 / 内容。

不建立：

```text
Map B
  ↓
引用 Map A Local History
  ↓
自动同步
```

这种复杂依赖。

---

# 30. 核心领域模型

第一版核心领域模型：

```text
Map
 ├── Initial State
 ├── Objects
 ├── Timeline
 │    ├── Event
 │    └── State Change
 │
 └── Branch
      ├── Parent Map
      ├── Parent At
      └── Own History
```

---

# 31. 第一阶段对象

第一阶段优先支持：

```text
POI
Terrain
Boundary
Event
State Change
Map
Branch
Timeline
```

后续可以接入：

```text
Character
Unit
Track
其他 World Object
```

原则：

> 新对象必须能够明确表达自己的历史状态。

---

# 32. 核心计算能力

新系统最重要的领域能力不是 CRUD，而是：

> **Historical World State Reconstruction**

输入：

```text
Map
+
Historical Time
```

输出：

```text
World State
```

对于 Branch：

```text
Branch
+
Historical Time
```

系统：

1. 找到父 Map；
2. 找到分叉时间；
3. 重建父 Map 到分叉点的状态；
4. 应用 Branch 自己的历史；
5. 得到最终 World State。

---

# 33. 最小核心闭环

第一阶段只需要证明：

```text
Create Map
    ↓
Initial State
    ↓
Create State Change
    ↓
Move Timeline
    ↓
Playback
    ↓
Create Branch
    ↓
Branch Playback
```

如果这个闭环成立：

> World View 的核心架构就成立。

---

# 34. 不在第一阶段实现的内容

以下能力暂不进入核心实现：

- Merge；
- Conflict Resolution；
- World Version；
- Object Version；
- Release；
- Revision Approval；
- 复杂权限；
- 多用户协作；
- 在线同步；
- Git 集成；
- 自动历史推断；
- 复杂模拟；
- 多分支合并。

---

# 35. Storage 原则

存储层必须支持：

1. Map；
2. Object；
3. Historical Time；
4. Event；
5. State Change；
6. Branch；
7. Shared Object；
8. Map-local State Change。

同时必须支持：

> **不复制祖先历史的 Branch。**

因此 Branch 必须可以通过：

```text
parent_map
parent_at
```

表达祖先关系。

---

# 36. 实现原则

## 36.1 领域模型优先

先建立：

```text
Map
Event
State Change
World State
Branch
```

再设计 API。

---

## 36.2 历史引擎优先

不要先做大量 CRUD。

第一优先级是：

> **给定 Map + Time，正确得到 World State。**

---

## 36.3 UI 不先决定领域模型

UI 必须建立在：

> Historical World State

之上。

不能为了 UI 方便修改领域模型。

---

# 37. 旧系统处理原则

旧系统：

> **不作为新系统代码基础。**

旧系统只用于：

- 查找已有能力；
- 参考交互；
- 参考地图组件；
- 参考数据格式；
- 发现过去已经解决的问题；
- 研究历史数据是否值得迁移。

旧代码不得因为“已经存在”而自动进入新架构。

---

# 38. 旧功能迁移原则

旧系统功能进入新系统时必须重新判断：

### KEEP

概念与新架构完全一致。

### ADAPT

功能有价值，但需要重新接入新领域模型。

### DROP

与新架构冲突或已经不需要。

### REBUILD

功能仍需要，但旧实现质量不足。

最终以新架构为准，而不是以旧代码为准。

---

# 39. 当前明确废弃的旧方向

新系统不采用：

```text
World Version
Object Version
Version Merge
Merge Conflict
Conflict Resolution
Revision Chain
Release Version
```

如果旧系统存在这些结构：

> 新系统不继承。

---

# 40. Architecture Boundary

本 V1.0 只冻结：

> **World View 的核心世界历史模型。**

以下内容暂不冻结：

- 最终数据库技术；
- API 具体路径；
- UI 最终视觉；
- 地图渲染技术；
- Terrain 底层存储格式；
- Geometry 引擎；
- 搜索实现；
- 文件格式；
- 性能优化方案。

这些应在后续实施设计阶段分别决定。

---

# 41. 架构不变量

实现过程中必须保持以下不变量：

### INV-01

Map 是核心世界创作对象。

### INV-02

Historical Time 是地图历史播放的核心时间。

### INV-03

Event 描述发生的事情。

### INV-04

State Change 描述世界状态变化。

### INV-05

State Change 不要求必须存在 Event。

### INV-06

Branch 从历史时间点产生。

### INV-07

Branch 继承分叉点以前的父历史。

### INV-08

Branch 不复制祖先历史。

### INV-09

父历史修改可以传播到 Branch。

### INV-10

分叉点以后父 Map 与 Branch 独立。

### INV-11

Branch 不允许 Merge。

### INV-12

用户可以删除或废弃 Branch。

### INV-13

Copy Map 与 Branch 是不同语义。

### INV-14

共享对象可以被多个 Map 引用。

### INV-15

Map 可以拥有局部历史。

### INV-16

Map-local History 不建立跨地图自动引用。

---

# 42. 最终模型

World View 的核心可以压缩成一句话：

> **Map 从一个初始世界状态开始，通过 Event 与 State Change 随历史时间演进；用户可以在任意历史时点让世界分叉，分支继承此前历史并独立发展，但不同历史永远不合并。**

核心结构：

```text
                         ┌───────────────┐
                         │     Map       │
                         │ Initial State │
                         └───────┬───────┘
                                 │
                                 ▼
                         ┌───────────────┐
                         │    Timeline   │
                         ├───────────────┤
                         │ Event         │
                         │ State Change  │
                         └───────┬───────┘
                                 │
                                 ▼
                         ┌───────────────┐
                         │  World State  │
                         │   at Time T   │
                         └───────┬───────┘
                                 │
                          Create Branch
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                    ▼                         ▼
                 Main Map                 Branch A
                    │                         │
                    ▼                         ▼
                 History                  History
                    │                         │
                    └──────────┐   ┌──────────┘
                               │   │
                               ▼   ▼
                          永久独立演进

                     × NO MERGE
```

---

# 43. Rebuild 下一阶段

Architecture Baseline 冻结后，下一阶段不是继续增加产品概念。

应进入：

> **Phase 2 — Domain Model & Historical State Engine**

优先完成：

1. Map Domain Model
2. Object Model
3. Historical Time Model
4. Event Model
5. State Change Model
6. World State Reconstruction
7. Branch Model
8. Branch State Reconstruction
9. 最小测试数据
10. 核心领域测试

完成后，再进入地图 UI 和完整对象体系。

---

# 44. 最终裁决

**World View Rebuild 从本文件开始。**

旧系统：

> Reference Only

新系统：

> New Architecture

核心原则：

> **先建立一个正确的“历史世界”，再把地图编辑器、播放、POI、Terrain、Boundary、Unit、Track 等能力接进去。**

不再围绕旧代码进行修补。

不再围绕 Git 设计版本管理。

不实现 Merge。

不解决不存在的 Merge Conflict。

**只解决 World View 真正需要解决的问题：**

> **世界如何从过去走到未来，以及如何从某个历史时点走向另一条历史。**
