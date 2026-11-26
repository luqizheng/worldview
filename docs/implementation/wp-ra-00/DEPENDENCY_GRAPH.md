# WP-RA-00 — 实施依赖关系（DEPENDENCY_GRAPH）

基线：`20c6fcca`。节点标注当前状态；箭头为"依赖 →"。

## 1. 主线依赖图

```text
[已完成]                        [待实施]                          [远期]
─────────                      ─────────                        ──────
PRA-01 管线 ✅ ─┐
WP-30B 生命周期 ✅ ─┼─→ A. 端到端接线 ─→ B. time-zero 内容投影 ─→ E. 历史重建(Command/Event) ─→ 动画 PRA-06
                │              │              │
                │              │              └─→ C. WP-30C Pan/Zoom/DPI ─→ M1 十二项验收(双平台)
                │              │
                │              └─→ D. Geometry Presentation ─→ PRA-03 空间服务 ─→ PRA-04 标签 ─→ PRA-05 LOD
                │                                        │
                │                                        └─→ PRA-07 栅格(Field) 
                │
                └─→ WP-50 交互框架(Selection/Inspector/Tool/Transient) ─→ WP-60 专项(需 Command 采纳)
                                                            │
E. 历史重建 ─→ 提交后 Undo / Freshness 真实语义 ─────────────┘
```

## 2. 依赖明细表

| 目标 | 直接前置 | 前置状态 | 备注 |
|---|---|---|---|
| A. 端到端接线（工作区打开→Context→请求→发布→渲染消费） | PRA-01 ✅、WP-30B ✅、ValidatedWorkspaceContext ✅ | 全部就绪 | 唯一缺口是接线本身；需决定渲染器消费快照的最小行为（清屏即可，先让管线真实运行） |
| B. time-zero 地图内容读投影 | A；`ListInitialObjects`/placement payload ✅ | 就绪 | Application 新增只读投影端口；**不依赖** Command/Event |
| C. WP-30C Pan/Zoom/DPI | WP-30B ✅ | 就绪 | map↔viewport 双向变换 + 测试；M1 验收 #7 的前置 |
| D. Geometry Presentation 最小集 | B；Geometry Asset 持久化 ✅ | 就绪 | 点/简单几何 → 场景绘制项 schema（需小幅设计决策，非新领域语义） |
| E. Command/Event/Checkpoint + 历史重建 | ADR-002/003 框架已定 | 未实施 | **同时服务 WP-40 完整目标与 WP-50 Apply；建议作为独立领域工作包** |
| Map extent/bounds ADR | 无 | 未实施 | PRA-01 Profile §5.2 解禁需显式决策；阻塞比例尺/装饰/默认相机 |
| PRA-04 标签 | D + 样式/字体系统 | 未实施 | HarfBuzz 路线合同已定 |
| PRA-06 动画 | E + 移动描述符 | ⛔ 全阻塞 | roadmap 明令最后做 |
| PRA-07 栅格 | Field 读投影（原始行已有） | 部分 | 时间零点栅格可视可能早于矢量标注 |
| WP-50 交互框架 | A（稳定渲染上下文） | 接线后即可 | ME-06 合同完备，无领域依赖 |
| WP-60 各专项 | WP-50 + 各自 Command 采纳 | ⛔ | Road endpoint、Relationship、Site/Group 类型采纳为硬门槛 |

## 3. 关键共享依赖（避免重复投资）

**Command/Event 持久化与历史重建（节点 E）同时是**：
- WP-40"任意历史时间渲染"的唯一解锁项；
- WP-50"Apply 后正式状态变更 + 提交后 Undo"的唯一权威机制；
- 《暗流涅槃》日 tick / 事件中断时间语义的产品级地基。

建议将其定位为独立领域工作包（不在 WP-40 首切片内），避免渲染切片被领域工程拖入过大范围。

## 4. 最短可行路径（用户原则："先让地图从正式数据正确显示，再建安全制作能力"）

```text
A 接线 → C WP-30C(视口导航+M1 取证) → B time-zero 投影 → D 最小几何渲染
        → [此时：Windows/macOS 首批 GUI 验收证据 + 真实内容可见]
        → WP-50 框架(只读) ∥ E 领域工作包(Command/Event)
        → 完整 WP-40(标签/样式/栅格/LOD) 与 WP-50 Apply 分流推进
```
