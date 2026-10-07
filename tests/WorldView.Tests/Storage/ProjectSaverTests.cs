using WorldView.Domain;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.Storage;

/// <summary>
/// ProjectSaver + ProjectLoader 端到端 round-trip。
///
/// 验证:
///  - Save 后文件落盘,且能被 ProjectLoader 重新读回
///  - 字段逐个对齐(键序允许差异)
///  - terrain_data.cells 在 save 时清空(MVP 由 cells_ref 承担,见 ProjectSaver 注释)
///  - 不动 layers/*.yaml 与 cells_ref 文件
/// </summary>
public class ProjectSaverTests
{
    [Fact]
    public void Roundtrip_Load_Save_Load_Preserves_All_Map_Fields()
    {
        // 1) 加载示例工程到临时目录的副本
        var sourceDir = LocateSampleWorld();
        var scratch = CopySampleWorldToScratch(sourceDir);

        try
        {
            var first = ProjectLoader.Load(scratch);
            Assert.True(first.Validation.IsValid, first.Validation.ToString());

            // 2) 调 Save(用 first.Maps,与原顺序一致)
            ProjectSaver.Save(scratch, first.World, first.Maps);

            // 3) 重新 Load
            var second = ProjectLoader.Load(scratch);
            Assert.True(second.Validation.IsValid, second.Validation.ToString());

            // 4) 逐字段比对所有 map
            Assert.Equal(first.Maps.Count, second.Maps.Count);
            for (var i = 0; i < first.Maps.Count; i++)
            {
                var a = first.Maps[i];
                var b = second.Maps[i];
                Assert.Equal(a.Id, b.Id);
                Assert.Equal(a.Name, b.Name);
                Assert.Equal(a.Type, b.Type);
                Assert.Equal(a.Grid.Width, b.Grid.Width);
                Assert.Equal(a.Grid.Height, b.Grid.Height);
                Assert.Equal(a.Grid.CellDistance, b.Grid.CellDistance);
                Assert.Equal(a.Grid.DistanceUnit, b.Grid.DistanceUnit);
                Assert.Equal(a.Time.DefaultGranularity, b.Time.DefaultGranularity);
                Assert.Equal(a.Time.MinGranularity, b.Time.MinGranularity);
                Assert.Equal(a.Presentation.ViewMode, b.Presentation.ViewMode);
                Assert.Equal(a.Presentation.Theme, b.Presentation.Theme);
                Assert.Equal(a.TerrainData.Encoding, b.TerrainData.Encoding);
                Assert.Equal(a.Buildings.Count, b.Buildings.Count);
                for (var j = 0; j < a.Buildings.Count; j++)
                {
                    Assert.Equal(a.Buildings[j], b.Buildings[j]);
                }
            }
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    [Fact]
    public void Save_Writes_ProjectYaml_And_All_Maps()
    {
        var sourceDir = LocateSampleWorld();
        var scratch = CopySampleWorldToScratch(sourceDir);
        try
        {
            var first = ProjectLoader.Load(scratch);
            ProjectSaver.Save(scratch, first.World, first.Maps);

            Assert.True(File.Exists(Path.Combine(scratch, "project.yaml")));
            foreach (var entry in first.World.MapIndex)
            {
                var mapPath = Path.Combine(scratch, entry.RefPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(mapPath), $"应存在: {mapPath}");
            }
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    [Fact]
    public void Save_Does_Not_Strip_Cells_Ref_Files()
    {
        // ProjectSaver 注释:不重写 cells_ref 文件,只把 cells 内联清空。
        // 加载示例工程 -> save -> 重新加载 -> 验证 cells_ref 引用的 cells 数与原文件一致。
        var sourceDir = LocateSampleWorld();
        var scratch = CopySampleWorldToScratch(sourceDir);
        try
        {
            // 当前示例工程的 world_kael.yaml 的 cells_ref 为空,所以改测:
            // 我们手动给 world_kael 注入 cells_ref,save 后 cells_ref 文件必须保持原样。
            var first = ProjectLoader.Load(scratch);
            var worldIdx = first.Maps.ToList().FindIndex(m => m.Type == MapType.World);
            Assert.True(worldIdx >= 0);

            var relCellsRef = "maps/world/world_kael.terrain.yaml";
            var cellsRefAbs = Path.Combine(scratch, relCellsRef.Replace('/', Path.DirectorySeparatorChar));
            var cellsBefore = File.ReadAllText(cellsRefAbs);

            // 改 map doc 的 cells_ref
            var updated = first.Maps[worldIdx] with
            {
                TerrainData = first.Maps[worldIdx].TerrainData with { CellsRef = relCellsRef }
            };
            var maps = first.Maps.ToList();
            maps[worldIdx] = updated;

            ProjectSaver.Save(scratch, first.World, maps);

            var cellsAfter = File.ReadAllText(cellsRefAbs);
            Assert.Equal(cellsBefore, cellsAfter);
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    [Fact]
    public void Save_Rejects_Nonexistent_Root()
    {
        var noRoot = Path.Combine(Path.GetTempPath(), "worldview-no-root-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(
            () => ProjectSaver.Save(noRoot, new World(), Array.Empty<MapDoc>()));
    }

    [Fact]
    public void Save_Rejects_Null_World()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "worldview-save-null-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Assert.Throws<ArgumentNullException>(
                () => ProjectSaver.Save(scratch, null!, Array.Empty<MapDoc>()));
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string LocateSampleWorld()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.yaml")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string CopySampleWorldToScratch(string sourceDir)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "worldview-save-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDir, scratch);
        return scratch;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest);
        }
    }

    private static void DeleteDirectory(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
