# Task 1 Report — B1 警告基线采集

Plan: `docs/superpowers/plans/2026-10-10-p0-c2-acceptance-baseline.md`
Brief: `task-1-brief.md` · 档位：轻档 · 采集时间：2026-10-10 05:35 (+0800)

## 命令

```bash
dotnet build D:\kudi-project\world-view\WorldView.sln -c Debug --no-incremental
```

**口径说明（关键）**：警告数**必须用 `--no-incremental` 全量重建采集**。增量构建只对被重编项目报警告，实测同一棵树增量跑得 **19 条**、全量跑得 **184 条**，两者不可比。基线采 184。

## 结果

| 项 | 值 |
| --- | --- |
| error | **0** |
| warning（MSBuild 汇总口径） | **184** |
| 构建耗时 | 约 1 分钟（全量） |

## 警告构成

总计 184 = **137（CS2002）** + **47（其余 11 类）**。

### CS2002 — 源文件指定多次（137 条，配置缺陷）

全部属于 `WorldView.csproj`。样本：

```
warning CS2002: 源文件"D:\kudi-project\world-view\scripts\Domain\Building.cs"指定了多次
```

137 个不同源文件被重复纳入编译（`scripts/**` 与 Generator 相关 include 存在重叠）。**不影响构建正确性**（0 error），但属 csproj 配置缺陷，是警告总数从 47 膨胀到 184 的**唯一原因**。

### 其余 11 类（47 条，逐类真去重计数）

| 警告码 | 条数 | 含义 | 归属项目 |
| --- | --- | --- | --- |
| xUnit1051 | 18 | 未使用 `TestContext.Current.CancellationToken` | `WorldView.Tests` |
| CS0108 | 14 | 成员隐藏继承成员（缺 `new`） | `WorldView` |
| RS2008 | 7 | 分析器规则缺 release tracking | `scripts/Generator` |
| xUnit2013 | 1 | 集合长度应改用 `Assert.Single` | `WorldView.Tests` |
| CS8625 | 1 | 不可为 null 的引用传 null | `WorldView` |
| CS8620 | 1 | 泛型可空性不匹配 | `WorldView` |
| CS8604 | 1 | 可能为 null 的引用参数 | `WorldView` |
| CS8602 | 1 | 解引用可能为 null | `WorldView` |
| CS8601 | 1 | 可能为 null 的引用赋值 | `WorldView` |
| CS0219 | 1 | 变量赋值后未使用 | `WorldView` |
| CS0109 | 1 | 成员未隐藏任何基类成员 | `WorldView` |

> 计数口径：按 `文件(行,列): warning CODE` 唯一化后计数。原始日志中每条出现两次（内联 + 汇总），未去重会得到 2 倍值。

## 与 plan 的偏差

1. **plan 未规定 `--no-incremental`**。实测增量与全量结果差 10 倍（19 vs 184），故本报告确立「全量重建」为 B1 唯一有效口径，并已回写 §0.5-B1。
2. **plan 预估 warning 量级未给**（规格要求「记录分组清单」），已按 11 类 + CS2002 单列补足。
3. **发现 CS2002 配置缺陷**（137 条）——超出 plan 范围（plan 只采集不修），已登记 §6.3 待后续工作项处理。

## 结论

B1 基线 = **0 error / 184 warnings（全量口径）**，其中 137 条为 `WorldView.csproj` 重复编译项缺陷、47 条为常规代码/分析器警告。
