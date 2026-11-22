<!-- P2-01-D01-NAV-START -->
> **P2-01-D01 拆分文件** ｜ 原章节：第 10 章　World State 缓存与 Checkpoint ｜ 索引：[README.md](README.md) ｜ 元数据：[00-document-metadata.md](00-document-metadata.md) ｜ 状态：Proposed / Discussion（未冻结，不构成实施授权）
<!-- P2-01-D01-NAV-END -->

# 10. World State 缓存与 Checkpoint

> **补充（2026-11-19，D01-72）**：Checkpoint **按 Branch 归属**，保存该 Branch 指定历史时间点的完整合成 World State（祖先继承状态 + Shared Object 在该 Branch 的有效状态 + 局部对象 + 自有 Command 执行结果）；祖先历史修改使受影响 Checkpoint 失效并重新生成。见[第 16 章](16-follow-up-review-d01-70-75.md) D01-72。本章 D01-65~69"派生缓存、非权威"的定位不变。

## 10.1 D01-65：重建结果是否保存

**最终修正选择：B。**

此前选择 A，后来用户明确要求改为 B。

最终阶段性规则：

- Command 是唯一权威历史数据。
- World State 重建结果允许保存为缓存。
- 播放时优先读取可用缓存，提高性能。
- 缓存不是权威数据。
- 缓存可以删除和重新生成。
- Command 或初始状态发生变化时，相关缓存失效。
- 缓存丢失不影响历史数据完整性。

明确边界：

**保存计算结果，不等于保存第二套权威历史。**

## 10.2 D01-66：Checkpoint 检查点机制

阶段性选择：B。

允许建立可重建的 Checkpoint，加速历史定位和播放。

规则：

1. 系统可以在历史时间线上建立检查点。
2. 用户跳转到某个历史时间时，从最近的有效检查点继续计算。
3. Command 是权威历史数据。
4. Checkpoint 是派生缓存，不是独立历史。
5. Command 修改后，受影响的后续 Checkpoint 失效。
6. Checkpoint 丢失时可以重新计算。
7. Checkpoint 不得改变历史重建结果。

## 10.3 D01-67：Checkpoint 建立位置

阶段性选择：A。

由系统自动决定 Checkpoint 的建立位置。

系统根据：

- 历史长度。
- Command 数量。
- 计算成本。

判断检查点位置。

不要求用户手动管理检查点。

## 10.4 D01-68：失效 Checkpoint 的处理

阶段性选择：A。

Command 修改导致后续 Checkpoint 失效时：

1. 立即删除所有受影响的 Checkpoint。
2. 保留不受影响的有效 Checkpoint。
3. 用户主动重新重建时，系统重新生成需要的 Checkpoint。
4. 不保留失效 Checkpoint，避免误用旧结果。

## 10.5 D01-69：Checkpoint 保存范围

阶段性选择：A。

Checkpoint 保存指定历史时间点的完整 World State。

包括该地图在该时间点的完整合成状态。

例如包含：

- Shared Object 状态。
- Map-local Object 状态。
- Branch 继承的对象状态。
- 其他构成该地图 World State 的必要状态。

读取时直接恢复完整状态，不需要每次重新合成父地图、共享对象和地图局部状态。

---

