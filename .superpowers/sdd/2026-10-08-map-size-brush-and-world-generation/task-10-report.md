# Task 10 Report —— `ResizeMapCommand`（修复轮）

## 1. Status / Commit

- **Status**: DONE
- **Commit**: `43a2745b5911b57a9b7ab679b4a66265c73dbb5f`（`deepseek-build`，父 commit `3939b6c`）
- **Commit message**: `feat(editing): Task 10 - ResizeMapCommand（纯裁剪 + 懒求值 Description）`
- **改动文件（exactly 2，均为新增）**：

```
 scripts/Editing/ResizeMapCommand.cs                | 199 ++++++++++++++
 tests/WorldView.Tests/Editing/ResizeMapCommandTests.cs | 286 +++++++++++++++++++++
```

工作区其余 ~293 项未暂存改动（`examples/sample_world/*.dll` 等历史产物）**未被触碰**；未使用 `git add -A` / `git add .`。

## 2. TDD 证据

### 2.1 重要事实：Fix 1 / Fix 2 在我接手时已在磁盘上落地

修复 brief 写于 `18:53:24`，而两个目标文件的 `LastWriteTime` 分别是 `18:55:01`（生产）与 `18:58:36`（测试）——即**崩溃的那一轮其实已经把 Fix 1（惰性 `Description` + 纯静态 `Describe`）、Fix 2（测试改调 `Describe`）、Fix 3（「五个」）、Fix 4（去 `using System.Linq;` + 尾部换行）全部写进文件**，brief 里的行号描述（`:37-38` 调 `FindMap`、`:280` 调 `new EditorContext()`）指向的是更早的版本。

因此本轮我**没有**看到 `System.AccessViolationException`——因为磁盘上已经没有会触发它的代码。我没有为了"制造 RED"而把 `new EditorContext()` 重新塞回测试文件（brief 明令禁止），改为**逐条核验** Fix 1–4 的实际状态（见 §3）并找出真正剩余的缺陷。

### 2.2 RED（本轮真实观测到的失败）

剩余的真实缺陷在**测试 1 的断言**上：它按 `width=4, height=3` 声称只保留 2 格，但裁剪谓词（brief 明令不得改动）是严格小于：

```
[DIAG] input=6 result=4 | (0,0) x<4=True y<3=True keep=True | (3,0) x<4=True y<3=True keep=True |
(0,1) x<4=True y<3=True keep=True | (5,1) x<4=False y<3=True keep=False |
(2,2) x<4=True y<3=True keep=True | (1,3) x<4=True y<3=False keep=False

    WorldView.Tests.Editing.ResizeMapCommandTests.CropCells_DropsOutOfBounds_KeepsOrder [FAIL]
      Assert.Equal() Failure: Values differ
      Expected: 2
      Actual:   4
      Stack Trace:
        D:\kudi-project\world-view\tests\WorldView.Tests\Editing\ResizeMapCommandTests.cs(79,0): at WorldView.Tests.Editing.ResizeMapCommandTests.CropCells_DropsOutOfBounds_KeepsOrder()
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 8, Errors: 0, Failed: 1, Skipped: 0, Time: 0.089s
[exit code: 1]
```

即：`x=3 < 4` 与 `y=2 < 3` 本就在界内（原注释写成 "drop (x=3 >= 4)" / "drop (y=2 >= 3)" 是错的）。被裁掉的只有 `(5,1)`（x≥4）与 `(1,3)`（y≥3）。实现是对的，**测试数据注释 + 计数断言错了**。

### 2.3 GREEN（单类）

```
cd D:\kudi-project\world-view\examples\sample_world
dotnet exec --runtimeconfig ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.runtimeconfig.json \
  --depsfile ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.deps.json \
  ..\..\tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.dll \
  ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll -nologo -class WorldView.Tests.Editing.ResizeMapCommandTests
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 8, Errors: 0, Failed: 0, Skipped: 0, Time: 0.071s
```

**8/8 全绿**，无 `AccessViolationException`。构建侧：`dotnet build WorldView.csproj --no-incremental` 与 `dotnet build tests\WorldView.Tests\WorldView.Tests.csproj --no-incremental` 均 `0 个错误`（强制重建，排除陈旧 dll）。

### 2.4 REGRESSION（全量）

```
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 177, Errors: 0, Failed: 7, Skipped: 0, Time: 0.759s
```

7 个失败**按名逐一**（与 T9 baseline 完全一致，未修、未新增）：

1. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`
2. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`
3. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`
4. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`
5. `WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array`
6. `WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`
7. `WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`

Total = 177 = 169（T9 baseline）+ 8（本任务新增）——T9-fix 的 `RefineCoastlineCommandTests` 尚未合入，故未出现 178。

#### shadowing 对照实验（消除 cwd 脏 dll 嫌疑）

`examples/sample_world`（runner cwd）里确实残留 `WorldView.dll`（18:57:36）与 `WorldView.Tests.dll`（18:57:38），哈希与 `tests\...\bin\Debug\net8.0\` 下的**不同**。为排除"跑的不是新 dll"的可能，我把这两个文件临时移到 `%TEMP%\wv-shadow-stash` 后**重跑全量**：

```
   WorldView.Tests  Total: 177, Errors: 0, Failed: 7, Skipped: 0, Time: 0.754s
```

失败名**逐字相同**，随后已把两个文件移回原位。两次运行结果一致 → 残留 dll 未影响结论；同时 §2.2 的 RED→GREEN 翻转本身就证明单类跑的是新编译的 `WorldView.Tests.dll`（旧 dll 里测试 1 仍是失败的老断言）。

## 3. Fix 1–4 逐条说明（含核验方式与判据）

### Fix 1 —— 构造函数不得调用 `FindMap`（崩溃根因）：**已在磁盘上，本轮核验通过**

`git show HEAD:scripts/Editing/ResizeMapCommand.cs` 的 ctor 全文：

```csharp
public ResizeMapCommand(EditorContext context, string mapId, GridSpec newGrid)
{
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(mapId);
    ArgumentNullException.ThrowIfNull(newGrid);

    _context = context;
    _mapId = mapId;
    _newGrid = newGrid;
}
```

- ctor **只存 3 个字段**，不调 `FindMap`、不预计算 `_description`。
- `Description` 懒求值（`ResizeMapCommand.cs:45-58`）：`_description is null` 时才 `_context.FindMap(_mapId)`；`null` 时回退 `$"调整地图尺寸：→ {W}×{H} 格"`，否则委托 `Describe(current.Grid, _newGrid)`。
- 全文件 `FindMap` 仅 2 处真实调用：`:51`（Description getter）与 `:182`（`Apply()`）——都在**实例路径**上，ctor 内为 0 处。核验命令：`git show HEAD:...ResizeMapCommand.cs | Select-String FindMap`。
- 纯静态 `Describe(GridSpec oldGrid, GridSpec newGrid)`（`:61-66`）同时 `ThrowIfNull` 两个参数，返回 `$"调整地图尺寸：{oldW}×{oldH} → {newW}×{newH} 格"`，与 plan L361 逐字一致。
- `Apply()` / `Revert()` 的 `_before ??= map;` / `if (_before is null) throw ...` 口径**原样未动**（首次 Apply 快照整张 `MapDoc`，`Revert` 用 `ReplaceMap(_before)` 写回 → 裁掉的 cells 与 buildings 一并还原）。

### Fix 2 —— 测试 8 改调纯静态 `Describe`：**已在磁盘上，本轮核验通过**

```csharp
[Fact]
public void Description_FormatsOldToNew()
{
    // Description 属性需要 Godot Node，纯 .NET 测试无法构造；
    // 这里覆盖它委托的纯静态格式化函数（生产代码路径）。
    var desc = ResizeMapCommand.Describe(new GridSpec(8, 8, 1.0), new GridSpec(96, 96, 1.0));
    Assert.Equal("调整地图尺寸：8×8 → 96×96 格", desc);
}
```

测试名保持 plan L362 逐字要求。核验：`git show HEAD:tests/.../ResizeMapCommandTests.cs | Select-String EditorContext` → **0 处匹配**（连注释里都没有，比 brief 允许的"仅注释"更严）。该文件 `using` 清单也不含 `WorldView.Editor`（原先为 `new EditorContext()` 引入，已删；保留的 `System.Linq` 被 `:72` `:103` 的 `.ToList()` 实际使用）。

### Fix 3 —— 类 XML doc 计数：**已在磁盘上，本轮核验通过**

`ResizeMapCommand.cs:11` 现为：

```
/// 五个纯静态方法 <see cref="CropCells"/> / <see cref="CropBuildings"/> / <see cref="Resize"/> /
/// <see cref="DescribeLoss"/> / <see cref="Describe"/> 不依赖 <see cref="EditorContext"/>，便于裸测；
```

计数（五个）与列举（5 个方法：`CropCells` / `CropBuildings` / `Resize` / `DescribeLoss` / `Describe`）一致，且这 5 个签名与 `plan L356-361` 逐字对应。

### Fix 4 —— 顺带清理：**已在磁盘上 + 本轮新增 1 项**

- 生产文件顶部 `using System.Linq;` 已删；实际 4 条 using 全部在用：`System.Collections.Generic`（`List` / `IReadOnlyList`）、`System.Globalization`（`CultureInfo`）、`WorldView.Domain`、`WorldView.Editor`。**无 LINQ 调用残留**。
- 两个文件末尾均为 `0x7D 0x0A`（`}\n`），尾部换行齐备（`[System.IO.File]::ReadAllBytes` 实测 `last2=125,10`）。
- **本轮新增清理**：测试 1 里遗留的调试输出块已删除——
  ```csharp
  var diag = new List<string>();
  foreach (var e in input) { diag.Add($"(...)"); }
  System.Console.WriteLine($"[DIAG] input={input.Count} result={result.Count} | " + string.Join(...));
  ```
  这是崩溃轮的排障残留（`[DIAG]` 与断言无关），随 Test 1 一并清理。

### 本轮实际修改的内容（唯一一处）

`tests/WorldView.Tests/Editing/ResizeMapCommandTests.cs` 的 `CropCells_DropsOutOfBounds_KeepsOrder`：修正错误注释 + 把计数断言从 `2` 改为 `4` 并补齐 4 个坐标（同时删除上述 `[DIAG]` 块）。

```csharp
// 裁剪谓词是严格小于（x < width && y < height），故 width-1 / height-1 边界内保留。
Cell(0, 0, ...),  // keep
Cell(3, 0, ...),  // keep (x == width-1，边界内)
Cell(0, 1, ...),  // keep
Cell(5, 1, ...),  // drop (x=5 >= 4)
Cell(2, 2, ...),  // keep (y == height-1，边界内)
Cell(1, 3, ...),  // drop (y=3 >= 3)

Assert.Equal(4, result.Count);
Assert.Equal(new GridCoord(0, 0), result[0].At);
Assert.Equal(new GridCoord(3, 0), result[1].At);
Assert.Equal(new GridCoord(0, 1), result[2].At);
Assert.Equal(new GridCoord(2, 2), result[3].At);
```

**未改**：谓词本身 `entry.At.X < width && entry.At.Y < height`、顺序保持、其余 7 个测试名与断言、`Resize` 的字段保留、`DescribeLoss` 文案与 zh-CN `N0`。修正后的用例反而**明确钉住了严格不等的边界**（`width-1` / `height-1` 保留，`width` / `height` 丢弃）。

## 4. 已知限制（不 overclaim）

- **`Description` 属性本身在纯 .NET 测试里不可达**：它的 getter 需要 `_context.FindMap(...)`，而 `EditorContext`（`scripts/Editor/EditorContext.cs:26`，`public partial class EditorContext : Node`）是 Godot 节点，测试里构造会抛 `AccessViolationException` 并污染测试进程。因此 `Description` 的**属性路径（含 `FindMap` 返回 null 时的回退文案分支）零覆盖**。
- 被覆盖的是它委托的**纯静态** `Describe(GridSpec, GridSpec)`（`Description_FormatsOldToNew`）。这只证明了格式化逻辑正确，**不证明** getter 的懒求值/缓存/回退分支运行时正确——那部分只经过静态阅读核验（`ResizeMapCommand.cs:45-58`）。
- 同理 `Apply()` / `Revert()` 的实例路径（`ReplaceMap` 写回、`_before` 快照还原）在本测试类中**未被覆盖**，由 T13 调用方路径负责。
- 次要观察（未修，非本任务范围）：`ResizeMapCommand.cs:167` 有编译警告 `CS8604`——`map.Buildings` 的 nullable 标注与 `CropBuildings` 的非空形参不匹配（`MapDoc` 构造器实际总是给空数组，运行期安全）。生产项目 **0 错误**，警告为既有基线。

## 5. 明确声明（供独立 review 核对）

1. **测试文件里没有 `EditorContext`**：`git show HEAD:tests/WorldView.Tests/Editing/ResizeMapCommandTests.cs | Select-String EditorContext` → **0 处**（无 `new`、无 mock、无 stub、无反射，也无注释提及）。
2. **生产构造函数未调用 `FindMap`**：ctor 体内 0 处（全文仅 `:51` Description getter 与 `:182` `Apply()`）。
3. 测试类 **8/8 绿**；全量 **Total 177 / Failed 7**，7 个失败名与指定清单逐字相同。
4. 未触碰 `RefineCoastlineCommand.cs`、`docs/**`、`.superpowers/**`（本报告除外，且报告未进 commit）；未引入 `System.Random`、`dotnet test`、`[Skip]`/`[Trait]`、新的 `using Godot;`。
5. 本轮**未观测到** `System.AccessViolationException`——原因是崩溃轮已把 Fix 1/Fix 2 写入磁盘（mtime `18:55:01` / `18:58:36` > brief `18:53:24`），我没有为了制造 RED 而故意回填违规代码。
