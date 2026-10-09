# Task 3 Report — 失败定性核对（§6.4 引用 + 新增 flaky 定性）

Plan: `docs/superpowers/plans/2026-10-10-p0-c2-acceptance-baseline.md`
档位：轻档 · 时间：2026-10-10 05:40 (+0800)

## 一、§6.4 核对结论

§6.4「B2 剩余基线失败判定表」7 行**全部有效**，与 T2 实测稳定集**逐条同名一一对应**（核对见 T2 报告 A 节）。定性无需修改，T4 基线文档直接引用。

| 判定类别 | 条数 | §6.4 行 |
| --- | --- | --- |
| 测试断言过期 | 2 | 1、2 |
| 产品实现偏离规格 | 2 | 3、4 |
| 测试笔误 | 1 | 5 |
| sample_world 数据违规 | 1 | 6 |
| 产品 bug（一行） | 1 | 7 |

## 二、新增 flaky 集定性（§6.4 未覆盖）

### 已确认机制（1 类）——**产品侧静默吞掉删除失败**

**涉及**：`BackupServiceTests.PreOpenRotation_Keeps_Most_Recent_10`（`Expected 10 / Actual 11`）、`BackupServiceTests.Delete_Removes_Content_And_Manifest_Entry`。

**证据链**：

1. `scripts/ProjectManagement/BackupService.cs:184-194` 的 `TryDeleteDir` **catch 全部异常且静默**（注释自述「清理失败不影响主流程」）。
2. 轮转逻辑（`BackupService.cs:99-113`）顺序为：**先** `TryDeleteDir(rm.ContentRoot)` 逐个删目录 → **后** `rotated.Save(ManifestPath)` 写清单。
3. 故删除失败时：**清单已移除记录，磁盘目录仍在** → 测试的清单断言通过（`rotations.Count == 10`）、磁盘断言失败（`backupDirs.Length == 11`）。与实测签名 `Expected 10 / Actual 11` **完全吻合**。
4. 删除偶发失败的诱因：备份目录刚由 `CopyProjectExcludingDotWorldview` 写入，Windows 下句柄/索引器/AV 的瞬态占用会让 `Directory.Delete` 抛错——间歇性，符合 flaky 特征。

**判定**：**产品健壮性缺陷**（静默失败掩盖不一致），非测试缺陷。归属：应单开工作项（改 `scripts/`，重档）。
**修法方向（未实施）**：`TryDeleteDir` 失败须上抛或记入 `ProjectException`；或改为「先删成功再写清单」以保证清单与磁盘强一致。

### 未定性（2 类）——需独立工作项用最小复现定位

| 现象 | 已排除的可能 | 现状 |
| --- | --- | --- |
| `List_Returns_Manifest_In_CreatedAt_Order` 清单记录丢失（`Expected 3 / Actual 2`） | ~~清单写入非原子~~ —— `BackupManifest.Save`（`BackupManifest.cs:102-114`）已用 `tmp + File.Replace` 原子写；~~临时目录并发冲突~~ —— `ProjectManagementTemp.NewDir` 用 GUID 后缀，逐测试唯一；~~Cleanup 误删~~ —— `Cleanup()` 全仓**无调用点**（死代码） | **未定性**。断言失败（非异常）说明 Save 成功但记录缺失，成因不明 |
| `RecentProjectServiceTests.RecordOpened_Dedupes_By_Path` / `..._Caps_At_10_Oldest_Evicted` 去重与排序偏差 | 同上三条 | **未定性**。该目录近期经 IFileSystem 注入改动（`d9726ab` / `b5d6b80`），时间上相关但**未证实因果** |

**判定**：暂标 **`[待定性]`**，禁止在无最小复现的情况下断言根因（避免把猜测写成结论）。

## 三、超范围事项（登记不处理）

1. `TryDeleteDir` 静默失败 → 产品缺陷，须单开重档工作项。
2. 3 条未定性 flaky → 须单开工作项做最小复现（参考：`Directory.GetDirectories` / `ListAsync` 计数断言在不同并发度下的表现）。
3. 全部修复项按 §6.4 注记排在 `P0-C2` 之后。

## 四、对 T4 的要求

基线文档必须写明：

- B2 失败**不是一个数字**，而是「稳定 7 + flaky 1~4」；
- flaky 项**不得**被后续会话当作「已知通过」忽略，也不得当作「必须修完才能提交」的红线；其处置归属见 §6.4 新增节；
- 引用 §6.4 而非重抄判定表。
