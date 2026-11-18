# Phase 2 — Domain Model & Historical State Engine V1.0

- **项目**：World View
- **阶段**：Phase 2
- **版本**：V1.0
- **日期**：2026-11-18
- **状态**：设计记录
- **上游文档**：[Architecture Baseline V1.0](architecture-baseline-v1.0.md)、[ADR-001](adr/ADR-001-map-history-and-branch-model.md)
- **性质**：领域模型与历史状态引擎设计

---

# 1. Phase Goal

建立 World View 最核心的领域能力：

```text
Input
Map / Branch
+
Historical Time

        ↓

Historical State Engine

        ↓

Output
World State
```

例如：

- Map = 魔法王国
- Time = 1200

系统必须能够得到：

- Terrain
- Boundary
- POI
- Character
- Unit
- ...

在 1200 年这一时刻应该是什么状态。

---

# 2. 第一原则：World State 是计算结果

这里需要特别明确：

> **World State 不是一个需要用户持续编辑的"版本"。**

它是：

> 根据**历史数据计算**出来的某一时刻世界状态。

例如：

```text
Initial State
    +
State Change @ 1050
    +
State Change @ 1100
    +
State Change @ 1200
    ↓
World State @ 1200
```

因此**不要建立**：

- WorldStateVersion
- WorldStateRevision
- WorldStateCommit

这些概念都不需要。

---

# 3. Domain Model 第一层

核心领域对象：

- Map
- Object
- Event
- State Change
- Branch
- Historical Time
- World State

关系：

```text
Map
 ├── Objects
 ├── Events
 ├── State Changes
 └── Branches
```

---

# 4. Object

Object 是地图中可以被表达、定位、观察或随时间变化的世界对象。

第一阶段：

- POI
- Terrain
- Boundary

后续：

- Character
- Unit
- Track

重要原则：

> **Object 本身不是历史。**
>
> **Object 是被历史改变的对象。**

例如，POI：白石城

```text
1650  白石城
1700  圣都
```

历史变化由 **State Change** 表达，而不是创建：

- POI Version 1
- POI Version 2

---

# 5. Object Identity

每个 Object 有稳定 Identity。

例如：

- POI ID = poi_001

历史发生变化：

```text
1600 → 白石城
1700 → 圣都
```

仍然是：

- poi_001

**不是**：

- poi_001_v1
- poi_001_v2

这样才能保证：

- Event 可以持续引用同一个对象；
- State Change 可以持续引用同一个对象；
- Branch 可以继承对象；
- 地图播放不会产生大量"对象版本"。

---

# 6. Object State

Object 的**当前属性**不等于 Object **Identity**。

例如，POI：

```text
id       = poi_001

name     = 圣都
type     = capital
location = ...
```

这些属性可以随着时间变化。

因此领域上：

```text
Object Identity
        +
Historical State
```

才构成指定时间点的对象状态。

---

# 7. State Change

State Change 是 Phase 2 的核心。

它至少需要表达：

- id
- map
- target_object
- effective_time
- change_type
- payload

概念：

```text
State Change
├── target
├── when
├── what changed
└── new state
```

例如：

```text
target = poi_001
time   = 1700
change = rename
value  = 圣都
```

---

# 8. 不采用"操作日志"作为唯一模型

不要把 State Change 设计成只能表达：

- set field
- delete field
- move field

因为未来世界变化可能非常复杂。

State Change 的领域含义应该是：

> **某对象在某个历史时间发生了一项可解释的状态变化。**

例如：

- POI renamed
- Boundary changed
- Terrain transformed
- Kingdom destroyed
- Character died
- Unit moved

具体变化类型可以逐步扩展。

---

# 9. State Change 的两个基本类型

第一阶段建议只建立两大类：

## 9.1 Property Change

对象属性变化。

例如：

- POI.name
- POI.type
- Character.status

## 9.2 Structural / Geometry Change

对象本体结构变化。

例如：

- Terrain geometry
- Boundary geometry
- POI location

以后再增加更具体的 domain change type。

---

# 10. Event

Event **不直接修改** World State。

Event：

- id
- map
- time
- title
- description
- participants

可以关联：

- State Changes

例如：

- Event：魔法大战
- 1200

State Changes：

- SC-001 山脉 → 平地
- SC-002 Boundary → New Boundary
- SC-003 皇都改名

---

# 11. Event 与 State Change 的执行关系

Event 可以：

- 0 State Change
- 1 State Change
- N State Changes

```text
Event
  ├── State Change
  ├── State Change
  └── State Change
```

但是：

> **State Change 可以没有 Event。**

---

# 12. Historical Time

Phase 2 需要先定义**时间的领域语义**，而不是先定义 UI。

至少需要：

> Historical Time 能够稳定比较：
>
> - T1 < T2
> - T1 = T2
> - T1 > T2

并且可以排序 State Change。

第一阶段**不强制绑定**具体历法 UI。

也就是说，领域引擎**不应该假定**一定是：

- YYYY-MM-DD

世界观可能是：

- 魔法纪元 1000 年
- 王朝 17 年
- 第三纪元 250 年

因此时间应该有：

```text
Calendar / Era
+
Position
```

的抽象。

---

# 13. 同一时间发生多个 State Change

允许。

例如：

```text
1200
SC-001 山脉 → 平地
SC-002 国境变化
SC-003 皇都改名
```

系统必须定义**确定性的应用顺序**。

第一版建议：

> 同一 Historical Time 内必须存在稳定顺序。

可以由 `sequence` 确定：

```text
1200 / 001
1200 / 002
1200 / 003
```

这样重建结果确定。

---

# 14. World State Reconstruction

核心算法概念：

```text
reconstruct(map, time)
```

执行：

1. Load Initial State
2. Find State Changes <= time
3. Sort by Historical Time
4. Sort same-time changes by sequence
5. Apply State Changes
6. Return World State

例如：

```text
Initial:
山脉
国境 A
白石城

1050:
国境 → B

1100:
白石城 → 圣都

1200:
山脉 → 平地
```

查询：World State @ 1200

得到：

```text
平地
国境 B
圣都
```

---

# 15. 查询历史状态

系统必须支持：

```text
getWorldState(map, time)
```

并且：

```text
getObjectState(map, object, time)
```

例如：

```text
getObjectState(poi_001, 1650)
```

返回：白石城

查询：

```text
1700
```

返回：圣都

---

# 16. Branch Reconstruction

Branch 的重建：

```text
reconstruct(branch, time)
```

必须分情况。

## 16.1 time <= parent_at

直接从父 Map 重建：

```text
reconstruct(parent_map, time)
```

## 16.2 time > parent_at

先：

```text
reconstruct(parent_map, parent_at)
```

然后应用：

- branch own State Changes

得到 World State。

---

# 17. Branch 的关键语义

例如：

```text
Main
1000 → 1100 → 1200
                  \
                   Branch A
```

Branch A：

- parent_map = Main
- parent_at = 1200

Branch A 在 1150 年查询：

- Branch A @ 1150

结果应该等于：

- Main @ 1150

Branch A 在 1250 年：

```text
Main @ 1200
+
Branch A Changes 1200–1250
```

---

# 18. Branch 不能修改父历史

Branch：

```text
1200 → 1250
```

如果 Branch 创建 SC-Branch-001：

> 它只能属于 Branch。
>
> **不能写入 Main Map。**

因此：

> **分叉后的 State Change 必须有明确的历史归属。**

---

# 19. 父历史修改传播

父 Map：

```text
1100
King = A
```

修改：

```text
King = B
```

Branch：

- parent_at = 1200

Branch 在 1100 查询：

```text
King = B
```

这是自然结果，因为：

> **Branch 只引用父历史。**

---

# 20. Branch 不复制对象

分支创建时：

- Main Objects

不会复制成：

- Branch Objects

> **Branch 继承对象集合。**

只有用户**明确复制**某个 Object 时，才建立新的 Object Identity。

---

# 21. Copy Object

用户："我不想再共享这个 POI。"

执行：

```text
copyObject()
```

得到：

- poi_001
- poi_002

之后 poi_001 和 poi_002 **完全独立**。

---

# 22. Shared Object 与 Branch

这里必须区分：

| 机制 | 含义 |
| --- | --- |
| Branch inheritance | 自动继承父 Map 的历史 |
| Shared Object | 多个 Map 主动引用同一个对象 |

> **两者不是同一个机制。**

---

# 23. Map Local State Change

如果共享对象 POI-001，Map A 想让它在自己的世界里不同：

```text
Map A
State Change
```

则该变化：

> **只属于 Map A。**
>
> **不修改 Shared Object 的全球状态。**

```text
Shared Object State
        ↓
Map Local State Change
        ↓
Map World State
```

---

# 24. Phase 2 的核心不变量

这一阶段必须测试：

| ID | 不变量 |
| --- | --- |
| HSE-01 | 相同输入一定得到相同 World State |
| HSE-02 | 时间越往后，历史 State Change 按顺序应用 |
| HSE-03 | 同一时间的 State Change 有确定顺序 |
| HSE-04 | Branch 在分叉点以前等价于父 Map |
| HSE-05 | Branch 在分叉点以后只应用自己的变化 |
| HSE-06 | 父历史修改会传播到 Branch |
| HSE-07 | Branch Change 不影响父 Map |
| HSE-08 | Copy Object 后两个对象独立 |
| HSE-09 | 删除 Branch 不影响父 Map |
| HSE-10 | 废弃 Branch 不改变历史数据 |

---

# 25. 第一批测试案例

不要一开始做复杂地图。

先建立最小测试：

## Test 01 — Initial State

- Map
- Time = 1000
- POI = A

结果：A

## Test 02 — State Change

```text
1000 A
1100 B
```

查询：

- 1050 → A
- 1100 → B

## Test 03 — Multiple Changes

```text
1000 A
1100 B
1200 C
```

查询：1150 → B

## Test 04 — Same Time

```text
1200
SC1
SC2
SC3
```

结果必须确定。

## Test 05 — Branch

```text
1000
1100
1200
   └── Branch
       1250
```

Branch 播放正确。

## Test 06 — Parent Modification

修改 1100，Branch 重新查询。

必须看到修改。

## Test 07 — Independent Branch

- Branch 1250：POI = B
- Main 1250：POI = A

两者互不影响。

---

# 26. Phase 2 暂时不要解决的问题

这一阶段暂时不处理：

- 地图渲染优化；
- 大规模 Geometry 性能；
- GPU；
- Terrain 引擎；
- 复杂空间索引；
- UI 动画；
- 协作；
- 云同步；
- Merge；
- 复杂模拟；
- AI 自动生成历史。

这些都建立在 Historical State Engine 正确之后。

---

# 27. Phase 2 实施顺序

严格按以下顺序：

```text
P2-01  Domain Object Model
       ↓
P2-02  Historical Time
       ↓
P2-03  Event
       ↓
P2-04  State Change
       ↓
P2-05  World State Reconstruction
       ↓
P2-06  Branch
       ↓
P2-07  Branch Reconstruction
       ↓
P2-08  Shared / Local History
       ↓
P2-09  Persistence
       ↓
P2-10  Test Matrix
```
