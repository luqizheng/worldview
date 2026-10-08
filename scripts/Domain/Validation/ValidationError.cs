using YamlDotNet.Serialization;

namespace WorldView.Domain.Validation;

/// <summary>
/// 校验错误严重度。
/// MVP 决策：仅 Error 阻塞 save，D1 验收为 IsValid=true。
/// </summary>
public enum Severity
{
    Warning,
    Error,
}

/// <summary>
/// 单条校验问题。
/// 字段定义见 <c>spec.md §6.5</c>：
/// <list type="bullet">
///   <item><c>Severity</c>: <see cref="Severity"/>（Warning / Error）</item>
///   <item><c>Code</c>: 稳定字面量（<c>E_xxx</c> 前缀），跨 i18n 可比</item>
///   <item><c>Path</c>: 错误所在工程内路径（YAML 文件相对工程根）或数据路径（<c>map.city_refs[0]</c>）</item>
///   <item><c>Message</c>: 人类可读描述（中文，遵循项目沟通语言）</item>
/// </list>
/// </summary>
public sealed record ValidationError(
    Severity Severity,
    string Code,
    string Path,
    string Message)
{
    /// <summary>
    /// <c>E_GRID_OOB</c>：地形单元 <c>(x, y)</c> 越出 <c>grid.width × grid.height</c>。
    /// </summary>
    public static ValidationError GridOutOfBounds(string path, int x, int y, int width, int height) =>
        new(Severity.Error, "E_GRID_OOB", path, $"地形单元 ({x}, {y}) 越出地图网格 {width}×{height}");

    /// <summary>
    /// <c>E_CITY_REF_MISSING</c>：<c>city_refs[].map_ref</c> 指向的文件不存在。
    /// </summary>
    public static ValidationError CityRefMissing(string path, string cityRefId, string fileRef) =>
        new(Severity.Error, "E_CITY_REF_MISSING", path, $"城市标记 {cityRefId} 引用 {fileRef} 但文件不存在");

    /// <summary>
    /// <c>E_LAYER_REF_MISSING</c>：<c>layers[].ref</c> 指向的文件不存在。
    /// </summary>
    public static ValidationError LayerRefMissing(string path, string layerRef) =>
        new(Severity.Error, "E_LAYER_REF_MISSING", path, $"图层引用 {layerRef} 但文件不存在");

    /// <summary>
    /// <c>E_ID_DUPLICATE</c>：跨多个文件 <c>id</c> 重复。
    /// </summary>
    public static ValidationError IdDuplicate(string path, string id, string otherPath) =>
        new(Severity.Error, "E_ID_DUPLICATE", path, $"id={id} 在 {otherPath} 已出现");

    /// <summary>
    /// <c>E_REF_ABSOLUTE</c>：<c>ref</c> 是绝对路径。
    /// </summary>
    public static ValidationError RefAbsolute(string path, string field) =>
        new(Severity.Error, "E_REF_ABSOLUTE", path, $"字段 {field} 是绝对路径，违反引用约定（须为相对工程根）");

    /// <summary>
    /// <c>E_REF_PARENT</c>：<c>ref</c> 含 <c>..</c>。
    /// </summary>
    public static ValidationError RefParent(string path, string field) =>
        new(Severity.Error, "E_REF_PARENT", path, $"字段 {field} 含 '..'，违反引用约定（不得越界）");

    /// <summary>
    /// <c>E_GRID_SIZE_MISSING</c>：<c>grid.width</c> 或 <c>grid.height</c> 非正整数。
    /// </summary>
    public static ValidationError GridSizeMissing(string path) =>
        new(Severity.Error, "E_GRID_SIZE_MISSING", path, "grid.width 或 grid.height 必须是非正整数");

    /// <summary>
    /// <c>E_TERRAIN_CELL_DUP</c>：同一 <c>(x, y)</c> 在 sparse cells 中重复出现。
    /// </summary>
    public static ValidationError TerrainCellDup(string path, int x, int y) =>
        new(Severity.Error, "E_TERRAIN_CELL_DUP", path, $"地形单元 ({x}, {y}) 在 sparse cells 中重复出现");

    /// <summary>
    /// <c>E_BUILDING_OOB</c>：建筑 <c>at: [x, y]</c> 越出城市地图网格。
    /// </summary>
    public static ValidationError BuildingOutOfBounds(string path, string buildingId, int x, int y, int width, int height) =>
        new(Severity.Error, "E_BUILDING_OOB", path, $"建筑 {buildingId} 位置 ({x}, {y}) 越出城市网格 {width}×{height}");

    /// <summary>
    /// <c>E_GRID_TOO_LARGE</c>：网格超过 spec.md §5.1.2 sparse 阈值（1024 × 1024）。
    /// </summary>
    public static ValidationError GridTooLargeSparse(string path, int width, int height) =>
        new(Severity.Warning, "W_GRID_TOO_LARGE_SPARSE", path, $"地图网格 {width} × {height} 超过 sparse 上限 1024×1024，建议改用 chunked");

    /// <summary>
    /// <c>W_LAYER_TYPE_MISMATCH</c>：<c>layer.type</c> 与 <c>layer_refs</c> 期望的类型不符（仅在引用层文件有 type 字段时校验）。
    /// </summary>
    public static ValidationError LayerTypeMismatch(string path, string layerFile, string actual) =>
        Warn($"图层 {layerFile} 解析后 type={actual}，与字段定义不符", "W_LAYER_TYPE_MISMATCH", path);

    /// <summary>
    /// <c>W_GENERATION_PARAM</c>：<c>GenerationSpec</c> 的某个字段超出 <see cref="WorldView.Domain.Generation.GenerationLimits"/> 范围。
    /// 仅警告，不阻塞 save（Generation 是新功能，用户手填的旧 spec 可能越界）。
    /// </summary>
    public static ValidationError GenerationParamOutOfRange(string path, string field, string value, string range) =>
        Warn($"生成参数 {field}={value} 超出范围 {range}", "W_GENERATION_PARAM", path);

    /// <summary>
    /// 语法糖：从字符串构造 Warning（避免每次手写 Severity.Warning）。
    /// </summary>
    private static ValidationError Warn(string message, string code, string path) =>
        new(Severity.Warning, code, path, message);
}