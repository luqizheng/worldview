using System;
using System.Collections.Generic;
using System.Globalization;

namespace WorldView.Domain.Objects;

/// <summary>
/// 十内置 Object Type 种子目录（P2-09-21 V0.1 推荐种子集 + P2-09-26 V0.2 现行
/// descriptor 目录的 pin 值）。类型语义以 P2-09-26 V0.2 descriptor 的显式声明为界
/// （CD-02-B 无 registry compiler，不发明语义）；Building/Cave 不在目录内
/// （P2-09-41 未完成 typed_v2，legacy allowlist 不含它们，CD-02-A §7.4）。
/// </summary>
public static class BuiltinObjectTypes
{
    /// <summary>十内置类型 Bound 时钉住的 descriptor schema 版本（P2-09-26 V0.2 实际值）。</summary>
    public const long DescriptorSchemaVersion = 2;

    /// <summary>语义标识模板（world-view.object-type.&lt;type_key&gt;.v1）。</summary>
    public const string SemanticsIdTemplate = "world-view.object-type.{0}.v1";

    /// <summary>十内置 type_key（顺序即播种顺序；P2-09-21 V0.1 / P2-09-26 V0.2）。</summary>
    public static IReadOnlyList<string> TypeKeys { get; } =
    [
        "landmass",
        "mountain_range",
        "mountain_peak",
        "river",
        "lake",
        "terrain_region",
        "settlement",
        "road",
        "political_region",
        "point_of_interest",
    ];

    /// <summary>type_key → 显示名（P2-09-21 §6 能力表）。</summary>
    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["landmass"] = "Landmass",
            ["mountain_range"] = "Mountain Range",
            ["mountain_peak"] = "Mountain Peak",
            ["river"] = "River",
            ["lake"] = "Lake",
            ["terrain_region"] = "Terrain Region",
            ["settlement"] = "Settlement",
            ["road"] = "Road",
            ["political_region"] = "Political Region",
            ["point_of_interest"] = "Point of Interest",
        };

    /// <summary>type_key 是否为内置类型（未知 type_key 不静默映射，OT-01 fail-closed）。</summary>
    public static bool IsBuiltinTypeKey(string typeKey) => DisplayNames.ContainsKey(typeKey);

    /// <summary>由 type_key 推导语义标识（模板一致；仅内置类型可调用）。</summary>
    public static string SemanticsIdFor(string typeKey)
    {
        if (!IsBuiltinTypeKey(typeKey))
        {
            throw new InvalidOperationException($"未知 Object Type \"{typeKey}\"：不在内置种子目录内，fail-closed（OT-01）。");
        }

        return string.Format(CultureInfo.InvariantCulture, SemanticsIdTemplate, typeKey);
    }
}

/// <summary>
/// Object ID 分配格式（CD-02-A §8/OD-3）：CD-02-B 采用最小分配格式
/// （十进制 sequence 文本）作为占位实现；逐类型显示语法（前缀等）留待
/// Object Type 配置设计关门。已分配值永不被格式变更改写（P2-09-04 §8）。
/// </summary>
public static class ObjectAllocationFormat
{
    /// <summary>最小分配格式版本（decimal sequence text）。</summary>
    public const long VersionMinimalDecimal = 1;

    /// <summary>最小分配格式定义（allocation_format_json 列的 V1 内容；占位契约，非可执行 schema）。</summary>
    public const string MinimalDecimalJson = "{\"display_grammar\":\"decimal_sequence\"}";

    /// <summary>由分配序号派生领域可见 ID（最小格式：不变文化十进制文本）。</summary>
    public static string DeriveAllocatedIdValue(long sequence)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "分配序号必须 >= 1。");
        }

        return sequence.ToString(CultureInfo.InvariantCulture);
    }
}
