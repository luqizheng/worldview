using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace WorldView.Domain.Registry;

/// <summary>Registry 校验失败（稳定错误码；fail-closed，不静默降级）。</summary>
public sealed class RegistryException : Exception
{
    /// <summary>稳定错误码：registry_format / descriptor_invalid / unknown_type_key /
    /// pin_mismatch / definition_invalid / unknown_definition_field / unknown_geometry_role。</summary>
    public string ErrorCode { get; }

    public RegistryException(string errorCode, string message)
        : this(errorCode, message, null)
    {
    }

    public RegistryException(string errorCode, string message, Exception? inner)
        : base($"[{errorCode}] {message}", inner)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>definition 字段声明类别（P2-09-26 V0.2 descriptor 数据；P2-09-24 §3 类型政策）。</summary>
public enum DefinitionFieldKind
{
    /// <summary>"required string"：必填非空白字符串。</summary>
    RequiredString,

    /// <summary>"optional string"：可缺省；出现时为字符串。</summary>
    OptionalString,

    /// <summary>JSON 字符串数组 = 有界枚举（稳定代码；区分大小写）。</summary>
    Enum,

    /// <summary>"non_negative_decimal"：数值 ≥ 0。</summary>
    NonNegativeDecimal,

    /// <summary>"required stable reference"：必填稳定引用（非空白字符串；语义校验归引用层）。</summary>
    RequiredStableReference,

    /// <summary>"optional immutable Asset reference"：可缺省 Asset 引用（hash 校验归 Asset 层，CD-02-D）。</summary>
    OptionalAssetReference,
}

/// <summary>definition 字段声明。</summary>
public sealed record DefinitionFieldDeclaration
{
    public required string Field { get; init; }

    public required DefinitionFieldKind Kind { get; init; }

    /// <summary>仅 Enum 非空。</summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

/// <summary>geometry role 声明（registry 只声明 role/基数/族；payload 编码待 CD-02-D）。</summary>
public sealed record GeometryRoleDeclaration
{
    public required string Role { get; init; }

    /// <summary>最少组件数（≥0；0 = 可缺省）。</summary>
    public required long Min { get; init; }

    /// <summary>最多组件数（null = 无上限）。</summary>
    public long? Max { get; init; }

    public required IReadOnlyList<string> Families { get; init; }
}

/// <summary>
/// 单个 Object Type descriptor（P2-09-26 V0.2 基础目录 + P2-09-41 V0.4 采纳扩展的
/// 不可变投影）。Registry 是编译期权威：本记录只暴露 descriptor 数据，不是可执行 schema。
/// </summary>
public sealed record ObjectTypeDescriptor
{
    public required string TypeKey { get; init; }

    public required long SchemaVersion { get; init; }

    public required string SemanticsId { get; init; }

    public required bool Allocatable { get; init; }

    public required IReadOnlyList<GeometryRoleDeclaration> GeometryRoles { get; init; }

    /// <summary>geometry 各 role 合计最小组件数（如 mountain_range=1；无则 null）。</summary>
    public long? GeometryCombinedMin { get; init; }

    public required IReadOnlyList<DefinitionFieldDeclaration> DefinitionFields { get; init; }

    /// <summary>声明的 runtime 字段（V1 全部 disabled_pending_*；启用待对应契约关门）。</summary>
    public required IReadOnlyList<string> RuntimeFields { get; init; }

    public required IReadOnlyList<string> Invariants { get; init; }

    /// <summary>initial_state_policy.includes（V1：map_membership/initial_placement/initial_runtime_attributes）。</summary>
    public required IReadOnlyList<string> InitialStateIncludes { get; init; }

    /// <summary>原始 command_capabilities（结构化策略对象；消费方为 Command 阶段）。</summary>
    public required IReadOnlyDictionary<string, JsonElement> CommandCapabilities { get; init; }

    /// <summary>typed aggregate schema 工件名（仅 Building/Cave 采纳类型有；P2-09-41 definition_schema_ref）。</summary>
    public string? DefinitionSchemaRef { get; init; }

    /// <summary>child aggregate 段清单（仅采纳类型；P2-09-41 child_aggregate_contract.sections）。</summary>
    public IReadOnlyList<string> ChildAggregateSections { get; init; } = Array.Empty<string>();

    public GeometryRoleDeclaration? FindGeometryRole(string role) =>
        GeometryRoles.FirstOrDefault(r => string.Equals(r.Role, role, StringComparison.Ordinal));

    public DefinitionFieldDeclaration? FindDefinitionField(string field) =>
        DefinitionFields.FirstOrDefault(f => string.Equals(f.Field, field, StringComparison.Ordinal));

    /// <summary>true = 该类型的 definition payload 是 typed aggregate 文档（P2-09-38/39 schema 校验路由）。</summary>
    public bool HasTypedAggregateSchema => !string.IsNullOrWhiteSpace(DefinitionSchemaRef);
}

/// <summary>
/// Object Type Descriptor Registry 运行时（CD-02-C/E；数据源 = P2-09-26 V0.2 基础
/// 目录 + P2-09-41 V0.4 Building/Cave 采纳扩展，均以程序集嵌入资源加载并 fail-closed
/// 结构校验）。职责边界（CD-02-A §7）：编译期权威 = descriptor 数据的正式读取与校验；
/// 无反射插件、无网络 fetch、无用户 authored handler；未知 type_key/未知字段/不支持
/// 版本 fail-closed（OT-01/OT-05/OT-06）。Registry V2 激活另受 P2-09-45 §8 十项验收门
/// 约束——本运行时是 V1 目录（format_version=1）读取层，不宣布 V2 激活。
/// </summary>
public static class ObjectTypeRegistry
{
    private const string BaseResourceName = "WorldView.Domain.Registry.p2-09-26-v1-object-type-descriptor-registry-v0.2.json";
    private const string AdoptionResourceName = "WorldView.Domain.Registry.p2-09-41-building-cave-object-type-descriptor-extension-v0.4.json";

    private static readonly Lazy<RegistryData> Data = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record RegistryData(IReadOnlyList<ObjectTypeDescriptor> Descriptors)
    {
        public Dictionary<string, ObjectTypeDescriptor> ByKey { get; } =
            Descriptors.ToDictionary(d => d.TypeKey, StringComparer.Ordinal);
    }

    public static IReadOnlyList<ObjectTypeDescriptor> All => Data.Value.Descriptors;

    public static bool IsKnownTypeKey(string typeKey) => Data.Value.ByKey.ContainsKey(typeKey);

    public static ObjectTypeDescriptor? Find(string typeKey) =>
        Data.Value.ByKey.TryGetValue(NormalizeTypeKey(typeKey), out var descriptor) ? descriptor : null;

    /// <summary>未知 type_key 不静默映射（OT-01 fail-closed）。</summary>
    public static ObjectTypeDescriptor Require(string typeKey) =>
        Find(typeKey)
        ?? throw new RegistryException("unknown_type_key", $"未知 Object Type \"{typeKey}\"：不在现行 descriptor 目录内。");

    /// <summary>typed aggregate schema 资源名（仅采纳类型；定义 schema 路由，P2-09-41 definition_schema_ref）。</summary>
    public static string? DefinitionSchemaResourceName(string typeKey) =>
        Require(typeKey).DefinitionSchemaRef is { } fileName
            ? $"WorldView.Domain.Registry.{fileName}"
            : null;

    /// <summary>
    /// definition payload 统一校验入口（canonical JSON）：采纳类型（HasTypedAggregateSchema）
    /// 路由到 <see cref="WorldView.Domain.Aggregates.BuildingCaveAggregateValidator"/>
    /// （P2-09-38/39 schema + 跨记录层）；其余类型走 descriptor 字段校验
    /// （<see cref="ValidateDefinitionPayload"/>）。违规抛 <see cref="RegistryException"/>。
    /// </summary>
    public static void ValidateDefinition(string typeKey, string canonicalJson)
    {
        var descriptor = Require(typeKey);
        if (!descriptor.HasTypedAggregateSchema)
        {
            ValidateDefinitionPayload(typeKey, canonicalJson);
            return;
        }

        var issues = WorldView.Domain.Aggregates.BuildingCaveAggregateValidator.Validate(typeKey, canonicalJson);
        if (issues.Count > 0)
        {
            var first = issues[0];
            throw new RegistryException(
                "definition_invalid",
                $"[{first.ErrorCode}] {first.Message}（共 {issues.Count} 项聚合校验问题；{typeKey}）");
        }
    }

    /// <summary>R-钉住：pin 必须与 descriptor 声明一致；不支持版本 fail-closed（OT-05/OT-06）。</summary>
    public static void ValidatePin(string typeKey, long schemaVersion, string semanticsId)
    {
        var descriptor = Require(typeKey);
        if (descriptor.SchemaVersion != schemaVersion
            || !string.Equals(descriptor.SemanticsId, semanticsId, StringComparison.Ordinal))
        {
            throw new RegistryException(
                "pin_mismatch",
                $"Object Type \"{typeKey}\" 的 pin (schema_version={schemaVersion}, semantics_id=\"{semanticsId}\") "
                + $"与 descriptor 声明不一致（预期 {descriptor.SchemaVersion} / \"{descriptor.SemanticsId}\"）。");
        }
    }

    /// <summary>
    /// typed definition payload 校验（OD-2 的运行时启用；descriptor 数据为校验依据，
    /// 不发明字段语义）。canonical JSON 必须为 object；未知字段拒绝
    /// （common_rules.unknown_fields=reject）；required 缺失/optional 类型错/枚举越界/
    /// 数值负数/引用空白均拒绝。
    /// </summary>
    public static void ValidateDefinitionPayload(string typeKey, string canonicalJson)
    {
        var descriptor = Require(typeKey);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(canonicalJson);
        }
        catch (JsonException ex)
        {
            throw new RegistryException("definition_invalid", $"definition payload 不可解析：{ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new RegistryException("definition_invalid", $"definition payload 必须为 JSON object（{typeKey}）。");
            }

            var declared = descriptor.DefinitionFields;
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                present.Add(property.Name);
                var declaration = declared.FirstOrDefault(f => string.Equals(f.Field, property.Name, StringComparison.Ordinal));
                if (declaration is null)
                {
                    throw new RegistryException(
                        "unknown_definition_field",
                        $"definition payload 含未声明字段 \"{property.Name}\"（{typeKey}；unknown_fields=reject）。");
                }

                ValidateFieldValue(typeKey, property.Name, declaration, property.Value);
            }

            foreach (var declaration in declared)
            {
                var required = declaration.Kind is DefinitionFieldKind.RequiredString
                    or DefinitionFieldKind.RequiredStableReference
                    or DefinitionFieldKind.Enum
                    or DefinitionFieldKind.NonNegativeDecimal;
                if (required && !present.Contains(declaration.Field))
                {
                    throw new RegistryException(
                        "definition_invalid",
                        $"definition payload 缺少必填字段 \"{declaration.Field}\"（{typeKey}）。");
                }
            }
        }
    }

    /// <summary>geometry role 必须为 descriptor 已声明 role（declared_geometry_roles_only 政策）。</summary>
    public static void ValidateGeometryRole(string typeKey, string role)
    {
        var descriptor = Require(typeKey);
        if (descriptor.FindGeometryRole(role) is null)
        {
            throw new RegistryException(
                "unknown_geometry_role",
                $"geometry role \"{role}\" 未在 {typeKey} descriptor 中声明。");
        }
    }

    /// <summary>initial_state_policy.includes（Initial State payload 的结构级 includes 列表；typed 字段 schema 仍为 OD-1 open gate）。</summary>
    public static IReadOnlyList<string> InitialStateIncludes(string typeKey) => Require(typeKey).InitialStateIncludes;

    // ---- 加载与结构校验（fail-closed；错误码 registry_format/descriptor_invalid） ----

    private static RegistryData Load()
    {
        // 基础目录（P2-09-26 V0.2 十类型）+ 采纳扩展（P2-09-41 V0.4 building/cave_system）
        // 合并为运行时目录；两文件键集不相交（重叠即 fail-closed）。
        var descriptors = new List<ObjectTypeDescriptor>();
        var baseRegistry = LoadRegistryFile(BaseResourceName);
        foreach (var property in baseRegistry.EnumerateObject())
        {
            descriptors.Add(ParseDescriptor(property.Name, property.Value));
        }

        using var adoption = JsonDocument.Parse(ReadResource(AdoptionResourceName));
        var additions = adoption.RootElement.GetProperty("object_type_additions");
        foreach (var property in additions.EnumerateObject())
        {
            if (descriptors.Any(d => string.Equals(d.TypeKey, property.Name, StringComparison.Ordinal)))
            {
                throw new RegistryException("registry_format", $"采纳扩展 type_key \"{property.Name}\" 与基础目录冲突。");
            }

            descriptors.Add(ParseDescriptor(property.Name, property.Value));
        }

        if (descriptors.Count == 0)
        {
            throw new RegistryException("registry_format", "registry 不含任何 descriptor。");
        }

        return new RegistryData(descriptors);
    }

    private static JsonElement LoadRegistryFile(string resourceName)
    {
        using var document = JsonDocument.Parse(ReadResource(resourceName));
        var root = document.RootElement;
        RequireString(root, "format", "registry_format");
        if (root.TryGetProperty("format_version", out var formatVersion) && formatVersion.GetInt64() != 1)
        {
            throw new RegistryException("registry_format", "不支持的 registry format_version（预期 1）。");
        }

        if (!root.TryGetProperty("object_types", out var types) || types.ValueKind != JsonValueKind.Object)
        {
            throw new RegistryException("registry_format", $"registry 资源 {resourceName} 缺少 object_types 对象。");
        }

        // EnumerateObject 在 using 释放后失效——克隆为字符串再重新解析（简单可靠）。
        return JsonDocument.Parse(types.GetRawText()).RootElement;
    }

    private static string ReadResource(string resourceName)
    {
        var assembly = typeof(ObjectTypeRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new RegistryException("registry_format", $"嵌入资源 {resourceName} 缺失（部署损坏）。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ObjectTypeDescriptor ParseDescriptor(string expectedKey, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new RegistryException("descriptor_invalid", $"descriptor \"{expectedKey}\" 必须为 object。");
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "type_key", "schema_version", "semantics_id", "allocatable", "geometry",
            "definition", "runtime", "invariants", "command_capabilities",
            "definition_edit_policy", "initial_state_policy", "runtime_attribute_change_policy",
            // P2-09-41 V0.4 采纳扩展块（S1）：definition schema 引用与 child aggregate 契约。
            "definition_schema_ref", "asset_contract", "child_aggregate_contract",
        };
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new RegistryException("descriptor_invalid", $"descriptor \"{expectedKey}\" 含未知键 \"{property.Name}\"（fail-closed）。");
            }
        }

        var typeKey = RequireString(value, "type_key", "descriptor_invalid");
        if (!string.Equals(typeKey, expectedKey, StringComparison.Ordinal))
        {
            throw new RegistryException("descriptor_invalid", $"descriptor type_key \"{typeKey}\" 与目录键 \"{expectedKey}\" 不一致。");
        }

        var schemaVersion = value.TryGetProperty("schema_version", out var sv) && sv.ValueKind == JsonValueKind.Number
            ? sv.GetInt64()
            : throw new RegistryException("descriptor_invalid", $"descriptor \"{typeKey}\" 缺少合法 schema_version。");
        var semanticsId = RequireString(value, "semantics_id", "descriptor_invalid");
        var expectedSemanticsId = $"world-view.object-type.{typeKey}.v1";
        if (!string.Equals(semanticsId, expectedSemanticsId, StringComparison.Ordinal))
        {
            throw new RegistryException("descriptor_invalid", $"semantics_id \"{semanticsId}\" 不符合模板 {expectedSemanticsId}。");
        }

        var allocatable = value.TryGetProperty("allocatable", out var al) && al.ValueKind == JsonValueKind.True;

        // geometry：role → {min,max,families}；特殊键 combined_min（合计下限）。
        var geometryRoles = new List<GeometryRoleDeclaration>();
        long? combinedMin = null;
        if (value.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object)
        {
            foreach (var role in geometry.EnumerateObject())
            {
                if (string.Equals(role.Name, "combined_min", StringComparison.Ordinal))
                {
                    combinedMin = role.Value.GetInt64();
                    continue;
                }

                if (role.Value.ValueKind != JsonValueKind.Object
                    || !role.Value.TryGetProperty("min", out var min) || min.ValueKind != JsonValueKind.Number
                    || !role.Value.TryGetProperty("families", out var families) || families.ValueKind != JsonValueKind.Array)
                {
                    throw new RegistryException("descriptor_invalid", $"geometry role \"{role.Name}\"（{typeKey}）缺少 min/families。");
                }

                var familyList = families.EnumerateArray()
                    .Select(f => f.GetString() ?? throw new RegistryException("descriptor_invalid", $"geometry family 非字符串（{typeKey}.{role.Name}）。"))
                    .ToArray();
                if (familyList.Length == 0)
                {
                    throw new RegistryException("descriptor_invalid", $"geometry role \"{role.Name}\"（{typeKey}）families 为空。");
                }

                long? max = role.Value.TryGetProperty("max", out var maxElement) && maxElement.ValueKind == JsonValueKind.Number
                    ? maxElement.GetInt64()
                    : null;
                var min1 = min.GetInt64();
                if (min1 < 0 || (max is not null && max < min1))
                {
                    throw new RegistryException("descriptor_invalid", $"geometry role \"{role.Name}\"（{typeKey}）基数区间非法。");
                }

                geometryRoles.Add(new GeometryRoleDeclaration { Role = role.Name, Min = min1, Max = max, Families = familyList });
            }
        }
        else
        {
            throw new RegistryException("descriptor_invalid", $"descriptor \"{typeKey}\" 缺少 geometry 对象。");
        }

        // definition：字段 → 声明。
        var definitionFields = new List<DefinitionFieldDeclaration>();
        if (value.TryGetProperty("definition", out var definition) && definition.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in definition.EnumerateObject())
            {
                definitionFields.Add(ParseDefinitionField(typeKey, field.Name, field.Value));
            }
        }
        else
        {
            throw new RegistryException("descriptor_invalid", $"descriptor \"{typeKey}\" 缺少 definition 对象。");
        }

        // runtime：字段 → 状态字符串（V1 全部 disabled_pending_*）。
        var runtimeFields = new List<string>();
        if (value.TryGetProperty("runtime", out var runtime) && runtime.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in runtime.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.String)
                {
                    throw new RegistryException("descriptor_invalid", $"runtime 字段 \"{field.Name}\"（{typeKey}）必须为字符串状态。");
                }

                runtimeFields.Add(field.Name);
            }
        }

        // invariants：字符串数组。
        var invariants = new List<string>();
        if (value.TryGetProperty("invariants", out var invariantsElement) && invariantsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in invariantsElement.EnumerateArray())
            {
                invariants.Add(item.GetString() ?? throw new RegistryException("descriptor_invalid", $"invariant 非字符串（{typeKey}）。"));
            }
        }

        // initial_state_policy.includes。
        var initialStateIncludes = new List<string>();
        if (value.TryGetProperty("initial_state_policy", out var policy) && policy.ValueKind == JsonValueKind.Object
            && policy.TryGetProperty("includes", out var includes) && includes.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in includes.EnumerateArray())
            {
                initialStateIncludes.Add(item.GetString() ?? throw new RegistryException("descriptor_invalid", $"initial_state include 非字符串（{typeKey}）。"));
            }
        }

        // command_capabilities：键为 command 名，值为结构化策略对象（原样保留）。
        var commandCapabilities = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (value.TryGetProperty("command_capabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Object)
        {
            foreach (var capability in capabilities.EnumerateObject())
            {
                commandCapabilities[capability.Name] = capability.Value.Clone();
            }
        }

        // P2-09-41 采纳扩展块：definition schema 引用（工件名）+ child aggregate 段。
        string? definitionSchemaRef = null;
        if (value.TryGetProperty("definition_schema_ref", out var schemaRef) && schemaRef.ValueKind == JsonValueKind.String)
        {
            definitionSchemaRef = schemaRef.GetString();
        }

        var childSections = new List<string>();
        if (value.TryGetProperty("child_aggregate_contract", out var childContract)
            && childContract.ValueKind == JsonValueKind.Object
            && childContract.TryGetProperty("sections", out var sections)
            && sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in sections.EnumerateArray())
            {
                childSections.Add(section.GetString() ?? throw new RegistryException("descriptor_invalid", $"child_aggregate 段非字符串（{typeKey}）。"));
            }
        }

        return new ObjectTypeDescriptor
        {
            TypeKey = typeKey,
            SchemaVersion = schemaVersion,
            SemanticsId = semanticsId,
            Allocatable = allocatable,
            GeometryRoles = geometryRoles,
            GeometryCombinedMin = combinedMin,
            DefinitionFields = definitionFields,
            RuntimeFields = runtimeFields,
            Invariants = invariants,
            InitialStateIncludes = initialStateIncludes,
            CommandCapabilities = commandCapabilities,
            DefinitionSchemaRef = definitionSchemaRef,
            ChildAggregateSections = childSections,
        };
    }

    private static DefinitionFieldDeclaration ParseDefinitionField(string typeKey, string field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var declaration = value.GetString();
            return declaration switch
            {
                "required string" => new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.RequiredString },
                "optional string" => new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.OptionalString },
                "non_negative_decimal" => new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.NonNegativeDecimal },
                "required stable reference" => new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.RequiredStableReference },
                "optional immutable Asset reference" => new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.OptionalAssetReference },
                _ => throw new RegistryException(
                    "descriptor_invalid",
                    $"definition 字段 \"{field}\"（{typeKey}）使用未知声明 \"{declaration}\"，fail-closed。"),
            };
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var values = value.EnumerateArray()
                .Select(item => item.GetString() ?? throw new RegistryException("descriptor_invalid", $"枚举值非字符串（{typeKey}.{field}）。"))
                .ToArray();
            if (values.Length == 0 || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            {
                throw new RegistryException("descriptor_invalid", $"枚举 \"{field}\"（{typeKey}）为空或含重复值。");
            }

            return new DefinitionFieldDeclaration { Field = field, Kind = DefinitionFieldKind.Enum, AllowedValues = values };
        }

        throw new RegistryException("descriptor_invalid", $"definition 字段 \"{field}\"（{typeKey}）声明形式非法。");
    }

    private static void ValidateFieldValue(string typeKey, string field, DefinitionFieldDeclaration declaration, JsonElement value)
    {
        switch (declaration.Kind)
        {
            case DefinitionFieldKind.RequiredString:
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    throw new RegistryException("definition_invalid", $"字段 \"{field}\"（{typeKey}）必须为非空白字符串。");
                }

                break;
            case DefinitionFieldKind.OptionalString:
            case DefinitionFieldKind.OptionalAssetReference:
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    throw new RegistryException("definition_invalid", $"字段 \"{field}\"（{typeKey}）出现时必须为非空白字符串。");
                }

                break;
            case DefinitionFieldKind.Enum:
                if (value.ValueKind != JsonValueKind.String
                    || declaration.AllowedValues!.All(v => !string.Equals(v, value.GetString(), StringComparison.Ordinal)))
                {
                    throw new RegistryException(
                        "definition_invalid",
                        $"字段 \"{field}\"（{typeKey}）必须为已声明枚举值之一（收到 \"{value.GetRawText()}\"）。");
                }

                break;
            case DefinitionFieldKind.NonNegativeDecimal:
                if (value.ValueKind != JsonValueKind.Number || value.GetDouble() < 0)
                {
                    throw new RegistryException("definition_invalid", $"字段 \"{field}\"（{typeKey}）必须为 ≥0 的数值。");
                }

                break;
            case DefinitionFieldKind.RequiredStableReference:
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    throw new RegistryException("definition_invalid", $"字段 \"{field}\"（{typeKey}）必须为非空白稳定引用。");
                }

                break;
            default:
                throw new RegistryException("definition_invalid", $"字段 \"{field}\"（{typeKey}）声明类别不可识别。");
        }
    }

    private static string RequireString(JsonElement value, string property, string errorCode)
    {
        if (value.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new RegistryException(errorCode, $"registry/descriptor 缺少合法字符串属性 \"{property}\"。");
    }

    private static string NormalizeTypeKey(string typeKey) =>
        string.IsNullOrWhiteSpace(typeKey) ? typeKey : typeKey.Trim();
}
