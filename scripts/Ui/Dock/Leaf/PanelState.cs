using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorldView.Ui.Dock.Leaf;

/// <summary>
/// 单个面板 / 行 / 节的 **UI 私有**状态（spec §5.2 / §5.3）。
///
/// <para>字段名与 spec §5.2 的 YAML 示例一一对应（通过
/// <see cref="UnderscoredNamingConvention"/> 转成 <c>snake_case</c>）。</para>
///
/// <para><b>不进数据契约</b>：这里的状态<b>不</b>写入 <c>MapDoc</c> / <c>World</c> /
/// <c>Project</c>，<b>不进</b> <c>project.yaml</c>（spec §5.1）。</para>
/// </summary>
public sealed class PanelState
{
    /// <summary>面板稳定 ID（如 <c>minimap</c>），跨会话复用。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary><c>dock_edge</c> / <c>dock_container</c> / <c>dock_child</c> / <c>float</c>。</summary>
    public string? Kind { get; set; }

    /// <summary><c>top</c> / <c>bottom</c> / <c>left</c> / <c>right</c>；或浮动的 <c>float</c>。</summary>
    public string? Edge { get; set; }

    public bool? Visible { get; set; }

    public int? Height { get; set; }

    public bool? Collapsed { get; set; }

    public int? CollapsedHeight { get; set; }

    /// <summary>仅 <c>dock_container</c>：行列表。</summary>
    public List<PanelState>? Rows { get; set; }

    /// <summary>仅 row：<c>full_width</c> / <c>horizontal</c>。</summary>
    public string? Layout { get; set; }

    public int? Order { get; set; }

    /// <summary>仅 <c>timeline_row</c>：展开态高度（56）。</summary>
    public int? ExpandedHeight { get; set; }

    /// <summary>仅 <c>timeline_row</c>：时间模式高度（120，overlay，不进布局）。</summary>
    public int? ModeHeight { get; set; }

    /// <summary>仅 <c>timeline_row</c>：28 ↔ 56。</summary>
    public bool? Expanded { get; set; }

    public int? Flex { get; set; }

    /// <summary>仅 row：section 列表。</summary>
    public List<PanelState>? Sections { get; set; }

    /// <summary>仅 section：<c>fixed</c> / <c>flex</c> / <c>auto</c>。</summary>
    public string? SizeMode { get; set; }

    /// <summary>
    /// 面板尺寸。<c>int</c>（浮窗宽）或字符串 <c>auto</c>（仅 minimap = 跟行高成方形）。
    /// 用 <see cref="object"/> 是因为同一个键要同时承载整型与字面量 <c>auto</c>。
    /// </summary>
    public object? Width { get; set; }

    /// <summary>浮动窗口位置（仅 <c>kind: float</c>）。</summary>
    public int? X { get; set; }

    public int? Y { get; set; }

    /// <summary>建筑格边长（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发；<c>[待标定]</c> 40–96）。</summary>
    public int? CellSize { get; set; }

    /// <summary>建筑格间距（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发）。</summary>
    public int? CellGap { get; set; }

    /// <summary>当前分类（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发）。</summary>
    public string? ActiveCategory { get; set; }

    /// <summary>每分类记住的页码（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发；<c>items_per_page</c> 不落盘）。</summary>
    public Dictionary<string, int>? PagesByCategory { get; set; }

    // ---------------------------------------------------------------- 读侧便利属性

    [YamlIgnore]
    public bool AutoWidth =>
        Width is string text && string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase);

    [YamlIgnore]
    public int? WidthPx => Width switch
    {
        int i => i,
        long l => (int)l,
        string s when int.TryParse(s, out var v) => v,
        _ => null,
    };

    public PanelState WithId(string id) => new()
    {
        Id = id,
        Kind = Kind,
        Edge = Edge,
        Visible = Visible,
        Height = Height,
        Collapsed = Collapsed,
        CollapsedHeight = CollapsedHeight,
        Rows = Rows,
        Layout = Layout,
        Order = Order,
        ExpandedHeight = ExpandedHeight,
        ModeHeight = ModeHeight,
        Expanded = Expanded,
        Flex = Flex,
        Sections = Sections,
        SizeMode = SizeMode,
        Width = Width,
        X = X,
        Y = Y,
        CellSize = CellSize,
        CellGap = CellGap,
        ActiveCategory = ActiveCategory,
        PagesByCategory = PagesByCategory,
    };
}

/// <summary>
/// 一整份布局文档（<c>.worldview/layouts/&lt;projectId&gt;.yaml</c>）。
///
/// <para>顶层固定 <c>schema_version</c> + <c>project_id</c>；
/// 其余是 <see cref="PanelState"/> 的树。</para>
/// </summary>
public sealed class LayoutState
{
    /// <summary>当前 schema 版本；不匹配时回退默认布局（spec §5.2 读时机 2）。</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string ProjectId { get; set; } = string.Empty;

    public List<PanelState> Root { get; set; } = new();

    // ---------------------------------------------------------------- YAML 读写

    private static ISerializer BuildSerializer() => new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static IDeserializer BuildDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>工程 id：取工程根目录名（多工程隔离用，spec §5.1）。</summary>
    public static string ProjectIdOf(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return "default";
        }

        var trimmed = projectRoot.TrimEnd('/', '\\');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? "default" : name;
    }

    /// <summary><c>&lt;projectRoot&gt;/.worldview/layouts/&lt;projectId&gt;.yaml</c>。</summary>
    public static string LayoutPath(string projectRoot)
    {
        var root = projectRoot;
        if (root.StartsWith("res://", StringComparison.Ordinal) || root.StartsWith("user://", StringComparison.Ordinal))
        {
            root = ProjectSettings.GlobalizePath(root);
        }

        return Path.Combine(root, ".worldview", "layouts", ProjectIdOf(root) + ".yaml");
    }

    /// <summary>
    /// 读取布局。<b>容错</b>：文件不存在、解析失败、<c>schema_version</c> 不匹配、
    /// <c>root</c> 为空 → 一律回退默认布局，<b>不报错、不删除旧文件</b>（便于排查）。
    /// </summary>
    public static LayoutState Load(string projectRoot)
    {
        var path = LayoutPath(projectRoot);
        try
        {
            if (!File.Exists(path))
            {
                return Default(projectRoot);
            }

            var document = BuildDeserializer().Deserialize<LayoutState>(File.ReadAllText(path));
            if (document is null || document.SchemaVersion != CurrentSchemaVersion)
            {
                GD.PushWarning($"布局文件 schema_version 不匹配，回退默认布局（保留原文件）：{path}");
                return Default(projectRoot);
            }

            if (document.Root.Count == 0)
            {
                return Default(projectRoot);
            }

            if (string.IsNullOrEmpty(document.ProjectId))
            {
                document.ProjectId = ProjectIdOf(projectRoot);
            }

            return document;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"布局文件读取失败，回退默认布局：{ex.Message}");
            return Default(projectRoot);
        }
    }

    /// <summary>同步写盘（spec §5.2：禁止在 <c>_ExitTree</c> 里 await）。</summary>
    public void Save(string projectRoot)
    {
        try
        {
            var path = LayoutPath(projectRoot);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SchemaVersion = CurrentSchemaVersion;
            ProjectId = ProjectIdOf(projectRoot);
            File.WriteAllText(path, BuildSerializer().Serialize(this));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"布局文件写入失败：{ex.Message}");
        }
    }

    // ---------------------------------------------------------------- 默认布局（spec §5.2）

    /// <summary>spec §5.2 给出的默认布局，逐字段对齐。</summary>
    public static LayoutState Default(string projectRoot) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        ProjectId = ProjectIdOf(projectRoot),
        Root = new List<PanelState>
        {
            new()
            {
                Id = "top_strip",
                Kind = "dock_edge",
                Edge = "top",
                Visible = true,
                Height = 32,
            },
            new()
            {
                Id = "bottom_palette",
                Kind = "dock_container",
                Edge = "bottom",
                Visible = true,
                Collapsed = false,
                Height = 200,
                CollapsedHeight = 40,
                Rows = new List<PanelState>
                {
                    new()
                    {
                        Id = "timeline_row",
                        Order = 0,
                        Layout = "full_width",
                        Height = 28,
                        ExpandedHeight = 56,
                        ModeHeight = 120,
                        Expanded = false,
                        Sections = new List<PanelState>
                        {
                            new()
                            {
                                Id = "timeline",
                                Order = 0,
                                SizeMode = "flex",
                                Flex = 1,
                                Visible = true,
                            },
                        },
                    },
                    new()
                    {
                        Id = "build_row",
                        Order = 1,
                        Layout = "horizontal",
                        Flex = 1,
                        Sections = new List<PanelState>
                        {
                            new()
                            {
                                Id = "minimap",
                                Order = 0,
                                SizeMode = "fixed",
                                Width = "auto",
                                Visible = true,
                            },
                            new()
                            {
                                Id = "tools",
                                Order = 1,
                                SizeMode = "flex",
                                Flex = 1,
                                CellSize = 56,
                                CellGap = 4,
                                ActiveCategory = "residences",
                                PagesByCategory = new Dictionary<string, int>
                                {
                                    ["residences"] = 0,
                                    ["castles"] = 0,
                                    ["religion"] = 0,
                                    ["military"] = 0,
                                    ["nature"] = 0,
                                    ["facilities"] = 0,
                                },
                            },
                        },
                    },
                    new()
                    {
                        Id = "tool_row",
                        Order = 2,
                        Layout = "full_width",
                        Height = 20,
                        Sections = new List<PanelState>
                        {
                            new()
                            {
                                Id = "tool_strip",
                                Order = 0,
                                SizeMode = "flex",
                                Flex = 1,
                                Visible = true,
                            },
                        },
                    },
                },
            },
            new()
            {
                Id = "inspector",
                Kind = "float",
                X = 1620,
                Y = 80,
                Width = 280,
                Height = 420,
                Visible = false,
            },
            new()
            {
                Id = "layer_panel",
                Kind = "float",
                X = 1620,
                Y = 520,
                Width = 280,
                Height = 320,
                Visible = false,
            },
        },
    };

    /// <summary>按 <c>id</c> 找顶层条目（找不到返回 null）。</summary>
    public PanelState? Find(string id)
    {
        foreach (var entry in Root)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>按 <c>id</c> 在 rows / sections 里递归找（找不到返回 null）。</summary>
    public PanelState? FindDeep(string id)
    {
        foreach (var entry in Root)
        {
            if (entry.Id == id)
            {
                return entry;
            }

            if (FindIn(entry.Rows, id) is { } inRows)
            {
                return inRows;
            }

            if (FindIn(entry.Sections, id) is { } inSections)
            {
                return inSections;
            }
        }

        return null;
    }

    private static PanelState? FindIn(List<PanelState>? list, string id)
    {
        if (list is null)
        {
            return null;
        }

        foreach (var entry in list)
        {
            if (entry.Id == id)
            {
                return entry;
            }

            if (FindIn(entry.Rows, id) is { } inRows)
            {
                return inRows;
            }

            if (FindIn(entry.Sections, id) is { } inSections)
            {
                return inSections;
            }
        }

        return null;
    }
}
