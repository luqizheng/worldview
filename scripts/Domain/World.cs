using YamlDotNet.Serialization;

namespace WorldView.Domain;

/// <summary>
/// 工程根（对应 <c>project.yaml</c>）。
/// 字段定义以 <c>docs/数据模型规格_v0.1.md §1</c> 为唯一真源；MVP 投影见 spec.md §2.1。
/// </summary>
public sealed record World(
    [property: YamlMember(Alias = "project_version")] string ProjectVersion = "0.1",
    string Name = "",
    string? Description = null,
    string? Author = null,
    [property: YamlMember(Alias = "default_time")] DefaultTime DefaultTime = null!,
    EraSpec Era = null!,
    [property: YamlMember(Alias = "map_index")] IReadOnlyList<MapIndexEntry> MapIndex = null!,
    [property: YamlMember(Alias = "character_index")] IReadOnlyList<MapIndexEntry> CharacterIndex = null!,
    AssetsSpec? Assets = null,
    WorldSettings? Settings = null)
{
    /// <summary>
    /// 防御性默认值。
    /// </summary>
    public World() : this(
        ProjectVersion: "0.1",
        Name: "",
        Description: null,
        Author: null,
        DefaultTime: new DefaultTime(),
        Era: new EraSpec(),
        MapIndex: Array.Empty<MapIndexEntry>(),
        CharacterIndex: Array.Empty<MapIndexEntry>(),
        Assets: null,
        Settings: new WorldSettings()) { }
}

/// <summary>
/// 工程默认时间（对应 <c>default_time: { year, month, day }</c>）。
/// MVP 范围：年必填；月 / 日可选（按 era.days_per_month 校验）。
/// </summary>
public sealed record DefaultTime(
    int Year = 1,
    int? Month = null,
    int? Day = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public DefaultTime() : this(1) { }
}

/// <summary>
/// 历法配置（对应 <c>era: {...}</c>）。
/// 完整字段（含 <c>fictional_calendar_rules</c> 等）见数据模型规格 §1；MVP 仅基础公历 + 闰年扩展。
/// </summary>
public sealed record EraSpec(
    string Name = "default",
    [property: YamlMember(Alias = "start_year")] int StartYear = 1,
    [property: YamlMember(Alias = "months_per_year")] int MonthsPerYear = 12,
    [property: YamlMember(Alias = "days_per_month")] IReadOnlyList<int> DaysPerMonth = null!,
    [property: YamlMember(Alias = "hours_per_day")] int HoursPerDay = 24,
    [property: YamlMember(Alias = "minutes_per_hour")] int MinutesPerHour = 60,
    [property: YamlMember(Alias = "seconds_per_minute")] int SecondsPerMinute = 60,
    LeapRule? Leap = null)
{
    public EraSpec() : this(
        Name: "default",
        StartYear: 1,
        MonthsPerYear: 12,
        DaysPerMonth: new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 },
        HoursPerDay: 24,
        MinutesPerHour: 60,
        SecondsPerMinute: 60,
        Leap: new LeapRule()) { }
}

/// <summary>
/// 闰年规则（MVP 简化：每隔 N 年的第 M 月加 D 天）。
/// </summary>
public sealed record LeapRule(int Every = 4, int Month = 2, [property: YamlMember(Alias = "add_days")] int AddDays = 1)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public LeapRule() : this(4, 2, 1) { }
}

/// <summary>
/// 工程索引项（<c>map_index</c> / <c>character_index</c> 列表项，跨文件 ref）。
/// 字段定义见 spec.md §3：<c>ref</c> 相对工程根，不得含 <c>..</c>，不得为绝对路径。
/// </summary>
public sealed record MapIndexEntry(
    [property: YamlMember(Alias = "ref")] string RefPath)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public MapIndexEntry() : this("") { }
}

/// <summary>
/// 资产清单（嵌套 <c>assets</c> 字段）。
/// MVP 仅纹理图集；M3+ 扩展字体 / 音频 / Shader。
/// </summary>
public sealed record AssetsSpec(TexturesSpec? Textures = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public AssetsSpec() : this((TexturesSpec?)null) { }
}

/// <summary>
/// 纹理清单（嵌套 <c>assets.textures</c> 字段）。
/// MVP 仅瓦片图集。<c>manifest</c> 指向 <c>assets/textures/tiles/index.yaml</c>。
/// </summary>
public sealed record TexturesSpec(TilesSpec? Tiles = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public TexturesSpec() : this((TilesSpec?)null) { }
}

/// <summary>
/// 瓦片图集清单（嵌套 <c>assets.textures.tiles</c> 字段）。
/// </summary>
public sealed record TilesSpec([property: YamlMember(Alias = "manifest")] string? Manifest = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public TilesSpec() : this((string?)null) { }
}

/// <summary>
/// 工程全局设置（嵌套 <c>settings</c> 字段）。
/// MVP 字段：<c>autosave</c> / <c>backup_keep</c>；其他推迟到 M7+（见 N12 自动保存）。
/// </summary>
public sealed record WorldSettings(
    bool Autosave = false,
    [property: YamlMember(Alias = "backup_keep")] int BackupKeep = 5)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public WorldSettings() : this(false, 5) { }
}
