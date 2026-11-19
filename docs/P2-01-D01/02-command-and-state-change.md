<!-- P2-01-D01-NAV-START -->
> **P2-01-D01 拆分文件** ｜ 原章节：第 2 章　Command 与 State Change ｜ 索引：[README.md](README.md) ｜ 元数据：[00-document-metadata.md](00-document-metadata.md) ｜ 状态：Proposed / Discussion（未冻结，不构成实施授权）
<!-- P2-01-D01-NAV-END -->

# 2. Command 与 State Change

## 2.1 D01-01：基本关系

阶段性选择：C。

Command 是用户创作行为的权威历史记录。

State Change 表达 Command 所产生的状态变化语义，是历史重建过程中可以解释或产生的派生结果。

World State 是按照历史时间解释并执行 Command 后得到的世界状态。

三者关系：

- Command：作者希望世界发生什么变化。
- State Change：这些操作所表达或产生的状态变化。
- World State：执行历史操作后，在某个时间点得到的世界状态。

不要求 Command 和 State Change 固定一对一对应。

一条 Command 可以：

- 不产生实际状态变化。
- 产生一个状态变化。
- 产生多个阶段性状态变化。

State Change 不自动成为第二套独立的用户创作历史。

## 2.2 D01-02：State Change 的生成方式

阶段性选择：C。

Command 只保存操作本身。

State Change 的语义可以根据 Command 类型解释，也可以由一条 Command 表达多个状态变化。

不要求每条 Command 固定生成一个或固定数量的 State Change。

## 2.3 D01-03：持续 Command

阶段性选择：C。

Command 可以持续一段历史时间。

例如：

`cmd(第一军团, move, 目标位置, duration=3 days)`

系统可以在持续执行过程中产生多个阶段性 State Change。

这些阶段性结果：

- 属于可重建的派生数据。
- 不成为独立的用户历史命令。
- 不改变 Command 作为权威记录的地位。

动画帧和视觉插值不等于 State Change。

必须区分有历史意义的状态变化与纯粹视觉表现。

## 2.4 D01-04：派生结果重建一致性

阶段性选择：C。

Command 是权威记录，同时可以保存必要的执行依据或可验证派生结果。

重建时必须优先保证历史一致性。

不能因为算法升级而悄悄改变既有历史。

算法可以升级，但必须避免旧历史在没有明确处理的情况下产生不同结果。

具体执行上下文、规则版本、算法版本及派生结果的精确保存模型仍待设计。

---

