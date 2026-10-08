using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.Generation;

/// <summary>
/// `generation` 数据契约测试（task-3-brief + spec §3）。
///
/// 契约要点（与 <c>docs/数据模型规格_v0.1.md</c> §2 同步）：
/// <list type="bullet">
///   <item><c>GenerationSpec</c> 是 <c>MapDoc</c> 的可选尾部字段；缺省 <c>null</c>。</item>
///   <item>YAML 键统一 snake_case：<c>algorithm</c> / <c>seed</c> / <c>land_ratio</c> / <c>ruggedness</c> / <c>min_island_cells</c> / <c>min_lake_cells</c> / <c>erosion</c>。</item>
///   <item>老 <c>main.yaml</c>（无 <c>generation</c> 键）必须能反序列化且 <c>Generation = null</c>。</item>
///   <item><c>GenerationLimits</c> 静态类负责参数范围校验，<c>DescribeViolation</c> 返中文描述供 Task 6 告警复用。</item>
/// </list>
/// </summary>
public class GenerationSpecYamlTests
{
    private const double Tolerance = 1e-9;

    private static string SampleWorldDir
    {
        get
        {
            // 与 tests/WorldView.Tests/Storage/ProjectLoaderTests.cs 一致：
            // 从测试工作目录向上找示例工程根（含 project.yaml）。
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.yaml")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    [Fact]
    public void Roundtrip_PreservesAllFields()
    {
        // 0xDEADBEEFCAFEBABE 高位为 1，C# 字面量默认推断为 ulong；
        // GenerationSpec.Seed 类型为 long，故显式 unchecked 强转（结果为负 long，不影响 roundtrip）。
        const long pinnedSeed = unchecked((long)0xDEADBEEFCAFEBABEul);

        // 构造一组非默认值的 GenerationSpec（覆盖全 7 字段）。
        var spec = new GenerationSpec(
            Algorithm: "coastline_v1",
            Seed: pinnedSeed,
            LandRatio: 0.42,
            Ruggedness: 0.6,
            MinIslandCells: 12,
            MinLakeCells: 9,
            Erosion: 2);

        var yaml = YamlSchemas.Serialize(spec);
        var back = YamlSchemas.Deserialize<GenerationSpec>(yaml);

        Assert.Equal("coastline_v1", back.Algorithm);
        Assert.Equal(pinnedSeed, back.Seed);
        Assert.Equal(0.42, back.LandRatio, Tolerance);
        Assert.Equal(0.6, back.Ruggedness, Tolerance);
        Assert.Equal(12, back.MinIslandCells);
        Assert.Equal(9, back.MinLakeCells);
        Assert.Equal(2, back.Erosion);
    }

    [Fact]
    public void AbsentGeneration_Deserializes_To_Null()
    {
        // R4：使用 walk-up 模式定位示例工程根，加载实际的 world 地图（world_kael.yaml，无 generation 键）。
        // 不能写死 examples/sample_world/maps/world/main.yaml（已废弃为注释文件）。
        var sampleRoot = SampleWorldDir;
        var worldYamlPath = Path.Combine(sampleRoot, "maps", "world", "world_kael.yaml");
        Assert.True(File.Exists(worldYamlPath),
            $"示例世界地图应存在：{worldYamlPath}");

        var yaml = File.ReadAllText(worldYamlPath);
        var map = YamlSchemas.Deserialize<MapDoc>(yaml);

        Assert.Null(map.Generation);
    }

    [Fact]
    public void Serialized_Keys_UseUnderscores()
    {
        var spec = new GenerationSpec();  // 默认值：algorithm="coastline_v1", seed=0, land_ratio=0.35 ...
        var yaml = YamlSchemas.Serialize(spec);

        // brief 要求：land_ratio / min_island_cells / min_lake_cells / seed / algorithm: coastline_v1
        Assert.Contains("land_ratio:", yaml);
        Assert.Contains("min_island_cells:", yaml);
        Assert.Contains("min_lake_cells:", yaml);
        Assert.Contains("seed:", yaml);
        Assert.Contains("algorithm: coastline_v1", yaml);
    }

    [Fact]
    public void IsWithinRanges_RejectsOutOfRange()
    {
        // 默认值应在范围内
        Assert.True(GenerationLimits.IsWithinRanges(new GenerationSpec()));

        // land_ratio 超出 [0.05, 0.80]
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(LandRatio: 0.9)));
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(LandRatio: 0.01)));

        // ruggedness 超出 [0, 1]
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(Ruggedness: 1.5)));
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(Ruggedness: -0.1)));

        // erosion 超出 [0, 3]
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(Erosion: 5)));
        Assert.False(GenerationLimits.IsWithinRanges(
            new GenerationSpec(Erosion: -1)));

        // DescribeViolation：默认值应返回 null
        Assert.Null(GenerationLimits.DescribeViolation(new GenerationSpec()));
        // 越界时应返回非空的中文描述
        Assert.NotNull(GenerationLimits.DescribeViolation(
            new GenerationSpec(LandRatio: 0.9)));
        Assert.NotNull(GenerationLimits.DescribeViolation(
            new GenerationSpec(Ruggedness: 1.5)));
        Assert.NotNull(GenerationLimits.DescribeViolation(
            new GenerationSpec(Erosion: 5)));
    }
}
