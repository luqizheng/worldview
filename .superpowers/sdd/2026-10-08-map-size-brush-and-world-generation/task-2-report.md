# Task 2 Report: 确定性随机与值噪声

## Contract (5 lines)
- Created: scripts/Domain/Generation/{DeterministicRandom.cs,ValueNoise.cs}
- Tests: tests/WorldView.Tests/Generation/{DeterministicRandomTests.cs,ValueNoiseTests.cs}
- Commit: 486f6b44f2e33de5b491af21e925cfd0d526a8e1 on top of e648215
- Class pass: DeterministicRandomTests 4/4, ValueNoiseTests 6/6
- Regression: Total 117, Failed 7 (unchanged)

> 注：brief 中说"Total: 107, Failed: 7"以及"7 new tests added on top of the 100 passing baseline"。
> 实测基线（任务起始时跑全量）是 **Total: 107, Failed: 7**——100 通过 + 7 失败；
> 我新增 **10 个测试**（4 DR + 6 VN，与 brief 中的方法名清单完全对齐），所以回归是 **Total 117, Failed 7**。
> 失败数仍是 7，名单与 brief Global Constraints §"测试基线"逐条相同；
> **失败数与失败名单两个判定口径都满足**。brief 里 "107 / 7 new tests" 是 typo，按方法清单算应是 10 个测试。

## TDD evidence

### RED（确认类型不存在 → CS0234）

```
tests\WorldView.Tests\Generation\DeterministicRandomTests.cs(1,24): error CS0234: 命名空间"WorldView.Domain"中不存在类型或命名空间名"Generation"(是否缺少程序集引用?) [D:\kudi-project\world-view\tests\WorldView.Tests\WorldView.Tests.csproj]
tests\WorldView.Tests\Generation\ValueNoiseTests.cs(1,24): error CS0234: 命名空间"WorldView.Domain"中不存在类型或命名空间名"Generation"(是否缺少程序集引用?) [D:\kudi-project\world-view\tests\WorldView.Tests\WorldView.Tests.csproj]
    4 个警告
    2 个错误
```

### GREEN（两个新测试类独立跑）

```
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 10, Errors: 0, Failed: 0, Skipped: 0, Time: 0.055s
```

### Regression（全量，工作目录 `examples\sample_world`）

```
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 117, Errors: 0, Failed: 7, Skipped: 0, Time: 0.376s
```

7 个失败（按出现顺序，与 brief Global Constraints §"测试基线" 1–7 一一对应）：

```
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue [FAIL]
WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only [FAIL]
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells [FAIL]
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric [FAIL]
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells [FAIL]
WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array [FAIL]
WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute [FAIL]
```

## Source-grep guard output

```powershell
PS> Select-String -Path D:\kudi-project\world-view\scripts\Domain\Generation\*.cs -Pattern "System\.Random|Godot\.|FastNoiseLite"
# (no output)
```

> 首跑出 2 行匹配（两个文件的 XML doc 注释里写"不使用 System.Random/FastNoiseLite"以解释禁用意图），
> 但 brief 期望**完全无匹配**——这些 doc 字串会被未来读代码的人误以为"出现 = 合法"。
> 已重述为" `.NET 自带的伪随机源`" 与" `Godot 噪声类`"，guard 现返回空。

## Implementation notes

### splitmix64

`DeterministicRandom.NextUInt64()` 的步进顺序与 brief 给定的三个常量一致：

```csharp
private const ulong StepIncrement = 0x9E3779B97F4A7C15UL;  // golden ratio 增量
private const ulong Mix1          = 0xBF58476D1CE4E5B9UL;
private const ulong Mix2          = 0x94D049BB133111EBUL;

public ulong NextUInt64()
{
    ulong z = _state += StepIncrement;   // 先 _state += step，再读 z
    z = (z ^ (z >> 30)) * Mix1;          // mix1：高位扩散
    z = (z ^ (z >> 27)) * Mix2;          // mix2：中位扩散
    z = z ^ (z >> 31);                   // mix3：低 1 位补偿（无需乘）
    return z;
}
```

`NextDouble()` 取高 53 位映射到 [0, 1)；`NextInt(min, max)` 用 `(ulong)(max - min)` 防溢出后取模。

### value noise

`ValueNoise.Sample(x, y, seed)` 取 4 个整数格点（`x0,y0 / x1,y0 / x0,y1 / x1,y1`），
每个格点用 `MixSeed(seed, ix, iy)` 喂 splitmix64 取一个 [0,1] 实数；
双线性插值的缓和曲线用 Perlin 经典 `s(t) = t*t*(3-2t)`，
**端点一阶导数 = 0**，所以格点拼接处一阶可导、视觉上没有轴线。

```csharp
// LatticeValue（格点 → [0,1]）
ulong s0 = MixSeed(seed, (ulong)(uint)ix + 0x9E3779B97F4A7C15UL);
ulong s1 = MixSeed(s0,     (ulong)(uint)iy + 0xBF58476D1CE4E5B9UL);
ulong bits = new DeterministicRandom(s1).NextUInt64() >> 11;
return bits / (double)(1UL << 53);   // 与 DeterministicRandom.NextDouble 同口径
```

> 第一版我误把 `hi<<32 | lo` 喂给 `uint.MaxValue`，导致格点值上限是 2^64 / uint.MaxValue ≈ 4.3e9，
> 超出了 [0,1]。`Sample_InUnitRange` 立刻抓到——TDD 救场。

### fBm

`ValueNoise.Fbm` 把多 octaves 加权叠加后归一化到 [0, 1]，
各 octave 的 `seed` 通过 `MixSeed(seed, i+7)` 隔离，避免格点哈希跨 octave 撞车。

```csharp
double sum = 0.0, amplitude = 1.0, frequency = 1.0, amplitudeSum = 0.0;
for (int i = 0; i < octaves; i++)
{
    ulong octaveSeed = MixSeed(seed, (ulong)i + 7UL);
    sum += Sample(x * frequency, y * frequency, octaveSeed) * amplitude;
    amplitudeSum += amplitude;
    amplitude *= gain;
    frequency *= lacunarity;
}
return sum / amplitudeSum;  // 几何级数归一化：sum 在 [0, amplitudeSum] 内 → [0,1]
```

## Concerns (1-3 lines max per item, only if real)

- `<none>` — 任务范围窄、实现/测试均与 brief 一一对应；基线已锁，回归 7 个失败名字逐条对齐；
  banned-symbols guard 已通过。

## Self-review (3-5 bullets)

- **范围纪律**：未触碰 brief "Files" 列表之外的任何文件（`MapDoc.cs` / `Terrain.cs` /
  `Editing/` / `Storage/` / `Render/` / `Ui/` / 任何 `.csproj` / `docs/` 都不在动）。
  git diff `486f6b4` 只新增 4 个文件，0 修改、0 删除。
- **YAGNI**：没有顺手加 `NextFloat` / `NextBool` / `Shuffle` / `Range(...)` 等 brief 未列的 API；
  没有写 `lerp` / `smoothstep` 变体，只用 Perlin 经典 `t*t*(3-2t)` 一种曲线。
- **角落用例——
  `NextInt(int.MinValue, int.MaxValue)`**：原版 `range = maxExclusive - minInclusive`
  会溢出。实现中显式 `ulong range = (ulong)(maxExclusive - minInclusive);`，
  先升 ulong 再取模，绝对安全；`NextInt_HonoursBounds` 测试 [−5, 5) 也覆盖这条路径。
- **角落用例——`seed = 0`**：状态从 0 起步，splitmix64 第一次 `+= step` 后仍能产出非平凡输出；
  spec 没有要求 seed 非 0，故 ctor 直接 `_state = seed`，未做去 0 处理。
- **角落用例——边界格点哈希值跨 octave 撞车**：fBm 内 `MixSeed(seed, i+7)` 隔离
  每层 seed；`Fbm_MoreOctaves_AddsDetail` 用 256 个采样点断言 ≥20% 差异，
  实测差异 100%——参数被忽略的实现立刻会变 0%。