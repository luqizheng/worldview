# Task 1 Report: MapSizing 纯单位换算

## 1. 交付物

- 新建 `scripts/Editor/MapSizing.cs`（59 行）——`public static class WorldView.Editor.MapSizing`，**不含** `using Godot;`，仅 `using System;` + `using WorldView.Domain;`。
- 新建 `tests/WorldView.Tests/Editor/MapSizingTests.cs`（84 行）——命名空间 `WorldView.Tests.Editor`，9 个 `[Fact]`，逐条覆盖 brief 的断言。
- 无 `.csproj` 改动，无 `Storage/` / `Domain/` 改动，未 `git add` `docs/superpowers/**`。

## 2. 实现摘要（与 brief 签名逐字一致）

```csharp
public const int MaxRadiusCells = 64;

public static int CellsForLength(double length, double cellDistance)
{
    if (cellDistance <= 0)
    {
        throw new ArgumentOutOfRangeException(
            nameof(cellDistance), cellDistance, "格距离必须为正数。");
    }

    return Math.Max(1, (int)Math.Ceiling(length / cellDistance));
}

public static double LengthForCells(int cells, double cellDistance) => cells * cellDistance;

public static GridSpec GridFromExtent(double widthPhysical, double heightPhysical, GridSpec template)
{
    return template with
    {
        Width = CellsForLength(widthPhysical, template.CellDistance),
        Height = CellsForLength(heightPhysical, template.CellDistance),
    };
}
```

`GridFromExtent` 用 `GridSpec` 的 `with` 表达式，只改写 `Width` / `Height`，`DistanceUnit` / `BaseUnit` / `HeightUnit` / `Origin` / `Show` / `Snap` / `ScaleLabel` 全部沿用模板（`GridSpec` 是 `sealed record`，见 `scripts/Domain/MapDoc.cs:19-33`）。

## 3. TDD 证据

### 3.1 RED

先写测试、类型尚不存在，构建即失败（编译不过正是预期的第一步失败）。

命令：

```powershell
dotnet build D:\kudi-project\world-view\tests\WorldView.Tests\WorldView.Tests.csproj
```

输出（节选，共 9 个 error，全为同一原因）：

```
生成失败。
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(20,27): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(27,27): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(34,25): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(41,25): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(48,58): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(55,17): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(65,17): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(75,26): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
D:\kudi-project\world-view\tests\WorldView.Tests\Editor\MapSizingTests.cs(82,21): error CS0103: 当前上下文中不存在名称"MapSizing" [...]
    0 个警告
    9 个错误
[exit code: 1]
```

为什么是预期的：`MapSizing` 尚未创建，9 处引用全部无法解析；这正是 brief「编译不过是预期的第一步失败（类型还不存在）」的形态。

### 3.2 GREEN

`dotnet test` 在本机不可用（VSTest `Win32Exception(5): 拒绝访问`），改用 xunit console runner。

命令（见 brief 的环境事实）：

```powershell
dotnet build D:\kudi-project\world-view\tests\WorldView.Tests\WorldView.Tests.csproj
$out = "D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0"
Copy-Item "$env:USERPROFILE\.nuget\packages\xunit.runner.console\2.9.2\tools\net6.0\*" $out -Force
$runner = "$out\xunit.console.dll"; $dll = "$out\WorldView.Tests.dll"
dotnet exec --runtimeconfig "$out\WorldView.Tests.runtimeconfig.json" --depsfile "$out\WorldView.Tests.deps.json" $runner $dll -nologo -class "WorldView.Tests.Editor.MapSizingTests"
```

输出：

```
  Discovering: WorldView.Tests
  Discovered:  WorldView.Tests
  Starting:    WorldView.Tests
  Finished:    WorldView.Tests
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 9, Errors: 0, Failed: 0, Skipped: 0, Time: 0.051s
```

主工程构建：`dotnet build D:\kudi-project\world-view\WorldView.csproj` → `0 个警告 / 0 个错误`。

### 3.3 全量回归

必须 `workdir = D:\kudi-project\world-view\examples\sample_world`（否则 fixture 相关 19 个测试会失败）。

```
  WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells [FAIL]
  WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue [FAIL]
  WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only [FAIL]
  WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric [FAIL]
  WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells [FAIL]
  WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array [FAIL]
  WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute [FAIL]
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 107, Errors: 0, Failed: 7, Skipped: 0, Time: 0.393s
```

- `Total: 107` = 基线 98 + 本任务新增 9。
- `Failed: 7`，与基线失败清单**逐项一致**（7 个名字完全对应）。未修改 `UndoStack.Push` 的 redo 清除行为，未修 `BrushShapes` / `YamlSchemas` / `ProjectLoader` 任何既有测试或源码。

## 4. 测试方法清单（brief 断言的逐条落点）

| 断言 | 测试方法 |
|---|---|
| `CellsForLength(5000, 50) == 100` | `CellsForLength_5000km_at_50km_Is_100`（brief 指定名，逐字采用） |
| `CellsForLength(5000, 33) == 152` | `CellsForLength_5000km_at_33km_Is_152_Covering_Requested_Length` |
| `CellsForLength(5, 50) == 1` | `CellsForLength_5km_at_50km_Is_One_Not_Zero` |
| `CellsForLength(200, 50) == 4` | `CellsForLength_200km_at_50km_Is_4` |
| `Throws<ArgumentOutOfRangeException>` | `CellsForLength_Zero_CellDistance_Throws` |
| `g.Width/Height == 512` | `GridFromExtent_12800km_at_25km_Is_512x512` |
| `DistanceUnit=="km"` / `BaseUnit=="m"` 其余字段不动 | `GridFromExtent_Keeps_Other_Template_Fields` |
| `MaxRadiusCells == 64` | `MaxRadiusCells_Is_64` |
| `LengthForCells(152,33) >= 5000` | `LengthForCells_152_Cells_at_33km_Covers_5000km`（并在 `…_Is_152_…` 中作为覆盖性断言复用） |

歧义处理（按 controller 裁决）：brief 的 run 命令点名 `CellsForLength_5000km_at_50km_Is_100`，其代码块却是裸 `Assert` 行，故把该方法作为 5000/50 用例的**逐字名称**，其余断言拆成同类中的兄弟 `[Fact]`，一条用例一个方法、名字描述用例。

## 5. 自审发现与修正

1. **测试名拼写错误**：`CellsForLength_5000km_at_33km_Ceils_To_152` 中 `Ceils` 为拼写错误，且名字未表达该用例的关键性质（不丢长度）。已改名 `CellsForLength_5000km_at_33km_Is_152_Covering_Requested_Length`，并把「152×33 ≥ 5000」的不变量断言直接写进该方法（不再只靠注释声称）。
2. **`GridFromExtent` 中多余的 `ArgumentNullException.ThrowIfNull(template)`**：brief 未要求，属于 YAGNI 超范围防御（且 `ArgumentNullException` 与 `ArgumentOutOfRangeException` 的抛出契约不在冻结签名内）。已删除，实现与 brief 逐字一致。
3. **测试文件多余的 `using System;`**：`tests/WorldView.Tests.csproj` 开启 `ImplicitUsings`（`tests/WorldView.Tests/WorldView.Tests.csproj:8`），`System` 已在 `WorldView.Tests.GlobalUsings.g.cs:2` 全局引入，显式 using 冗余。已删除（`using WorldView.Domain;` / `using WorldView.Editor;` / `using Xunit;` 保留）。
4. **`scripts/Editor/MapSizing.cs` 的 `using System;` 保留**：虽同样开启 `ImplicitUsings`（`WorldView.csproj:10`），但这是 brief 明确指定的形式（"`using System;` + `using WorldView.Domain;`"），且与同目录 `scripts/Editor/EditorContext.cs` 及多数 `scripts/**` 文件一致，故不改。
5. **纯逻辑约束自检**：`scripts/Editor/MapSizing.cs` 中 `grep -i godot` 无命中；`scripts/**` 其余文件可引用 Godot，但本类不引用，可脱离引擎单测。
6. **未跟踪文件 `docs/superpowers/`**：commit 前后 `git status` 均显示为 `??`，未被 staged；本次提交只含两个目标文件（`git show --name-only` 已确认）。

自审结论：diff 与 brief 的接口清单、命名空间、命名、范围完全对齐；无 YAGNI 残留；测试无共享状态、无顺序依赖、输出干净（无警告）。

## 6. 变更文件与提交

- `scripts/Editor/MapSizing.cs`（新增）
- `tests/WorldView.Tests/Editor/MapSizingTests.cs`（新增）

提交：`e648215 feat(editor): 新增 MapSizing 纯单位换算（物理长度 ↔ 网格格数）`（2 files changed, 143 insertions，分支 `deepseek-build`）。

## 7. 遗留关注点（供 controller / Task 12 / Task 13 参考）

1. **`CellsForLength` 的整数溢出未防御**：`(int)Math.Ceiling(length / cellDistance)` 在极端输入（如 `length = 1e20, cellDistance = 1`）下会得到超出 `int` 范围的双精度值，C# 未检查的 double → int 转换在此为未定义结果（通常 `int.MinValue`），随后被 `Math.Max(1, …)` 兜成 1。brief 未要求防御，Task 13 的地图尺寸对话框若允许任意输入需自行上限校验。本类不做，符合「`length <= 0` 由 UI 拦截」的分工。
2. **`MaxRadiusCells` 仅作为常量存在**，本类不做封顶计算；Task 12 需要自己在输入换算后与它取 `Math.Min`（这与 brief 描述一致，只是提醒调用侧别忘了）。
3. **未生成 `.uid` 文件**：`scripts/Editor/` 下既有脚本都有 Godot 生成的 `*.cs.uid`。本类未被任何场景引用（纯静态工具类），本次仅经 `dotnet build` 编译、未经 Godot 编辑器导入，故无 `.uid`。若后续在编辑器里打开工程，Godot 可能会生成 `scripts/Editor/MapSizing.cs.uid`，届时会以「工具链自动改写的工程文件」形式出现在工作区（参考提交 `fff1908` 的先例）。
4. **本任务的 7 个失败是基线噪声**，与 MapSizing 无因果关系（其中 `UndoStackTests` 两个是既有的 redo 清除行为导致、`BrushShapesTests` 两个是圆盘半径定义变更、其余三个是 schema / 加载器契约变更）。按 brief 要求未处理。
