using System;
using System.IO;
using System.Linq;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.Storage;

/// <summary>
/// T14 (plan L470-479)：<see cref="TileSetStyleRegistry"/> 的纯逻辑测试。
///
/// 这一层刻意**不碰 Godot**（不 <c>ResourceLoader</c>、不读盘），所以可以直接喂 YAML 字符串；
/// Godot 感知层 <c>WorldView.Editor.TileSetLoader</c> 的行为由引擎内冒烟覆盖，不在此处实例化。
/// </summary>
public class TileSetStyleRegistryTests
{
    private const string MinimalYaml =
        "version: 1\n" +
        "default: jrpg\n" +
        "styles:\n" +
        "  - id: jrpg\n" +
        "    path: .\n" +
        "    description: JRPG 16×16 风格\n";

    private const string TwoStylesYaml =
        "version: 1\n" +
        "default: jrpg\n" +
        "styles:\n" +
        "  - id: jrpg\n" +
        "    path: .\n" +
        "    description: JRPG 16×16 风格\n" +
        "  - id: _fallback_solid_color\n" +
        "    path: solid\n" +
        "    description: 纯色兜底（无资产时退化）\n";

    [Fact]
    public void Parse_MinimalYaml_ReadsDefault()
    {
        var registry = new TileSetStyleRegistry(MinimalYaml);

        Assert.Equal("jrpg", registry.DefaultId);
        Assert.Single(registry.Styles);
        Assert.Equal("jrpg", registry.Styles[0].Id);
        Assert.Equal(".", registry.Styles[0].Path);
        Assert.Equal("JRPG 16×16 风格", registry.Styles[0].Description);
    }

    [Fact]
    public void Parse_TwoStyles_ExposesBoth()
    {
        var registry = new TileSetStyleRegistry(TwoStylesYaml);

        Assert.Equal(new[] { "jrpg", "_fallback_solid_color" }, registry.Styles.Select(s => s.Id).ToArray());
        Assert.Equal("jrpg", registry.DefaultId);
        Assert.Equal("solid", registry.Styles[1].Path);
    }

    [Fact]
    public void AssetPathFor_KnownId_ReturnsResPath()
    {
        var registry = new TileSetStyleRegistry(TwoStylesYaml);

        Assert.Equal("res://assets/textures/tiles/.", registry.AssetPathFor("jrpg"));
        Assert.Equal("res://assets/textures/tiles/solid", registry.AssetPathFor("_fallback_solid_color"));
    }

    [Fact]
    public void AssetPathFor_UnknownId_Throws()
    {
        var registry = new TileSetStyleRegistry(TwoStylesYaml);

        var ex = Assert.Throws<ArgumentException>(() => registry.AssetPathFor("jrpg_hd"));
        Assert.Contains("jrpg_hd", ex.Message);
    }

    [Fact]
    public void DefaultId_NotInStyles_Throws()
    {
        const string yaml =
            "version: 1\n" +
            "default: missing_style\n" +
            "styles:\n" +
            "  - id: jrpg\n" +
            "    path: .\n";

        var ex = Assert.Throws<InvalidDataException>(() => new TileSetStyleRegistry(yaml));
        Assert.Contains("missing_style", ex.Message);
    }

    [Fact]
    public void Parse_EmptyStyles_Throws()
    {
        const string yaml = "version: 1\ndefault: jrpg\nstyles: []\n";

        // 只断言异常类型是无判别力的：删掉 TileSetStyleRegistry.cs:119-122 的空表守卫后，
        // 紧随其后的「default 不在 styles 中」检查仍会抛同一个 InvalidDataException，测试照绿。
        // 用文案区分，钉住空表守卫本身。
        var ex = Assert.Throws<InvalidDataException>(() => new TileSetStyleRegistry(yaml));
        Assert.Contains("不能为空", ex.Message);
    }

    /// <summary>
    /// 端到端契约：仓库里那份**真实**的 <c>assets/textures/tiles/styles.yaml</c> 必须能被本注册器解析，
    /// 且给出 plan L442-453 约定的 <c>jrpg</c> 默认风格。先复制到临时目录再读，证明构造器不依赖磁盘布局。
    /// </summary>
    [Fact]
    public void Styles_Yaml_Contract_Is_Parsed_From_Real_File()
    {
        var source = RepoStylesYamlPath;
        Assert.True(File.Exists(source), $"仓库内应存在 styles.yaml：{source}");

        var tempDir = Path.Combine(Path.GetTempPath(), "wv-tileset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var copy = Path.Combine(tempDir, "styles.yaml");
            File.Copy(source, copy, overwrite: true);

            var registry = new TileSetStyleRegistry(File.ReadAllText(copy));

            var ids = registry.Styles.Select(s => s.Id).ToArray();
            Assert.Contains("jrpg", ids);
            Assert.Equal("jrpg", registry.DefaultId);
            Assert.StartsWith("res://assets/textures/tiles", registry.AssetPathFor("jrpg"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>从测试工作目录向上找仓库根（含 <c>assets/textures/tiles/styles.yaml</c> 的那一层）。</summary>
    private static string RepoStylesYamlPath
    {
        get
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null &&
                   !File.Exists(Path.Combine(dir.FullName, "assets", "textures", "tiles", "styles.yaml")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "assets", "textures", "tiles", "styles.yaml");
        }
    }
}
