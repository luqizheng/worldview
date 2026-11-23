using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;

namespace WorldView.Domain.Aggregates;

/// <summary>单条聚合校验问题（稳定错误码 + 说明）。</summary>
public sealed record AggregateIssue
{
    /// <summary>P2-09-40 V0.6 vector_contract 使用的稳定错误码（如 CD_BLD_DUPLICATE_SPACE_ID）。</summary>
    public required string ErrorCode { get; init; }

    public required string Message { get; init; }
}

/// <summary>publication 层外部上下文（typed link 行 / core_asset 目录 / CAS 凭据）。</summary>
public sealed record AggregatePublicationContext
{
    /// <summary>typed link 行（owner scope 内；role = geometry_component）。</summary>
    public IReadOnlyList<(string AssetId, string Role)> AssetLinks { get; init; } = Array.Empty<(string, string)>();

    /// <summary>core_asset 目录（asset_id → content_hash）。</summary>
    public IReadOnlyList<(string AssetId, string ContentHash)> AssetCatalog { get; init; } = Array.Empty<(string, string)>();

    /// <summary>CAS 凭据（提供时必须与 current 一致）。</summary>
    public string? ExpectedDefinitionHash { get; init; }

    public string? CurrentDefinitionHash { get; init; }
}

/// <summary>aggregate/measurement 层外部上下文（跨 owner 对账与测量语义）。</summary>
public sealed record AggregateContext
{
    /// <summary>本聚合的 owner Object（Cave 连接端点归属对账用）。</summary>
    public string? OwnerObjectId { get; init; }

    /// <summary>被测端点声称的 owner（CAV-018 跨 owner 场景）。</summary>
    public string? EndpointOwnerObjectId { get; init; }

    /// <summary>测量语义（CAV-009/015：absolute_world_altitude 需要 datum）。</summary>
    public string? MeasurementSemantics { get; init; }

    public string? SurveyFrameId { get; init; }
}

/// <summary>
/// Building/Cave typed aggregate 校验器（CD-02-E S3/S7/S8；P2-09-37 §3.2 "strict,
/// type-specific JSON Schema or equivalent compiled validator" 的落地）。
/// 三层：schema（P2-09-38/39 V0.5 经 JsonSchemaSubset 执行，violation 映射稳定错误码）→
/// aggregate 跨记录（child ID 唯一性/段内引用解析/端点解析/组件上下文/跨 owner）→
/// publication（typed link/Asset hash/CAS 对账）。稳定错误码与 P2-09-40 V0.6
/// vector_contract 对齐；未识别 schema 关键词 fail-closed（JsonSchemaSubset）。
/// </summary>
public static class BuildingCaveAggregateValidator
{
    public static IReadOnlyList<AggregateIssue> Validate(
        string typeKey,
        string payloadJson,
        AggregatePublicationContext? publication = null,
        AggregateContext? context = null)
    {
        var descriptor = ObjectTypeRegistry.Require(typeKey);
        var issues = new List<AggregateIssue>();
        var prefix = typeKey switch
        {
            "building" => "CD_BLD",
            "cave_system" => "CD_CAVE",
            _ => throw new RegistryException("unknown_type_key", $"类型 {typeKey} 无 aggregate schema 校验路由。"),
        };

        if (!descriptor.HasTypedAggregateSchema)
        {
            throw new RegistryException("schema_invalid", $"类型 {typeKey} 未声明 typed aggregate schema（definition_schema_ref）。");
        }

        // ---- schema 层 ----
        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException ex)
        {
            return new[] { new AggregateIssue { ErrorCode = $"{prefix}_SCHEMA_INVALID", Message = $"payload 不可解析：{ex.Message}" } };
        }

        using (payload)
        {
            var schemaResource = ObjectTypeRegistry.DefinitionSchemaResourceName(typeKey)
                ?? throw new RegistryException("schema_invalid", $"类型 {typeKey} 缺少 schema 资源路由。");
            using var schemaDocument = JsonDocument.Parse(ReadSchemaResource(schemaResource));
            var violations = JsonSchemaSubset.Validate(schemaDocument.RootElement, payload.RootElement);
            foreach (var violation in MapSchemaViolations(violations, prefix, payload.RootElement))
            {
                issues.Add(violation);
            }

            if (issues.Count == 0)
            {
                // ---- aggregate 跨记录层（schema 合法时执行） ----
                issues.AddRange(ValidateAggregate(typeKey, prefix, payload.RootElement, context));
            }

            // ---- publication 层 ----
            if (publication is not null)
            {
                issues.AddRange(ValidatePublication(prefix, payload.RootElement, publication));
            }

            // ---- measurement 层 ----
            if (context?.MeasurementSemantics is not null)
            {
                issues.AddRange(ValidateMeasurement(prefix, payload.RootElement, context));
            }

            return issues;
        }
    }

    // ---- schema violation → 稳定错误码映射 ----

    private static IEnumerable<AggregateIssue> MapSchemaViolations(
        IReadOnlyList<SchemaViolation> violations, string prefix, JsonElement instance)
    {
        foreach (var violation in violations)
        {
            var code = MapViolation(violation, prefix, instance);
            if (code is not null)
            {
                yield return new AggregateIssue { ErrorCode = code, Message = $"{violation.Keyword}@{violation.InstancePath}: {violation.Message}" };
            }
            else
            {
                yield return new AggregateIssue { ErrorCode = $"{prefix}_SCHEMA_INVALID", Message = $"{violation.Keyword}@{violation.InstancePath}: {violation.Message}" };
            }
        }
    }

    private static string? MapViolation(SchemaViolation violation, string prefix, JsonElement instance)
    {
        var path = violation.InstancePath;

        switch (violation.Keyword)
        {
            case "additionalProperties":
                return prefix == "CD_BLD" ? "CD_BLD_SCHEMA_UNKNOWN_FIELD" : "CD_CAVE_SCHEMA_UNKNOWN_FIELD";

            case "const" when path.Contains("/dimension"):
                return prefix == "CD_BLD" ? "CD_BLD_2D_ONLY" : "CD_CAVE_2D_ONLY";

            case "pattern" when path.EndsWith("/content_hash", StringComparison.Ordinal) && prefix == "CD_BLD":
                return "CD_BLD_SCHEMA_HASH";

            case "required" when violation.PropertyName == "horizontal_frame":
                return "CD_BLD_FRAME_REQUIRED";

            case "minItems" when path == "/survey_frames":
                return "CD_CAVE_SURVEY_FRAME_REQUIRED";

            case "minimum" when path.Contains("_uncertainty"):
                return "CD_CAVE_UNCERTAINTY_NEGATIVE";

            case "enum" when path == "/survey_completeness":
                return "CD_CAVE_COMPLETENESS_ENUM";

            case "required" when path.Contains("/geometry_components") && violation.PropertyName is not null:
                return prefix == "CD_BLD"
                    ? violation.PropertyName switch
                    {
                        "spatial_reference_id" => "CD_BLD_SPATIAL_REFERENCE_REQUIRED",
                        "geometry_schema_version" => "CD_BLD_GEOMETRY_SCHEMA_VERSION_REQUIRED",
                        "provenance" => "CD_BLD_GEOMETRY_PROVENANCE_REQUIRED",
                        _ => null,
                    }
                    : violation.PropertyName switch
                    {
                        "validity_metadata" => "CD_CAVE_GEOMETRY_VALIDITY_REQUIRED",
                        "content_hash" => "CD_CAVE_GEOMETRY_CONTENT_HASH_REQUIRED",
                        "geometry_component_id" => "CD_CAVE_GEOMETRY_COMPONENT_ID_REQUIRED",
                        _ => null,
                    };

            case "oneOf" when path.Contains("/geometry_components"):
                return MapGeometryComponentOneOf(prefix, path, instance);

            default:
                return null;
        }
    }

    /// <summary>geometry_component oneOf 失败按 role/上下文映射（BLD-022/023/024、CAV-019/020）。</summary>
    private static string? MapGeometryComponentOneOf(string prefix, string path, JsonElement instance)
    {
        var component = TryNavigate(instance, path);
        if (component is null || component.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var role = component.Value.TryGetProperty("geometry_role", out var roleElement) ? roleElement.GetString() : null;
        var family = component.Value.TryGetProperty("geometry_family", out var familyElement) ? familyElement.GetString() : null;
        var hasContext = component.Value.TryGetProperty("child_context", out var contextElement)
            && contextElement.ValueKind == JsonValueKind.Object
            && contextElement.EnumerateObject().Any();

        if (prefix == "CD_BLD")
        {
            return role switch
            {
                "space_region" => family is "polygon" or "multi_polygon"
                    ? "CD_BLD_SPACE_REGION_CONTEXT_INCOMPLETE"
                    : "CD_BLD_ROLE_FAMILY_MISMATCH",
                "boundary" when component.Value.TryGetProperty("child_context", out var boundaryContext)
                    && boundaryContext.TryGetProperty("boundary_id", out _) => "CD_BLD_ROLE_FAMILY_MISMATCH",
                "boundary" => "CD_BLD_BOUNDARY_CONTEXT_REQUIRED",
                _ => null,
            };
        }

        return role switch
        {
            "passage_plan" => "CD_CAVE_PASSAGE_CONTEXT_REQUIRED",
            "survey_station" => "CD_CAVE_STATION_CONTEXT_REQUIRED",
            _ => null,
        };
    }

    // ---- aggregate 跨记录层 ----

    private static IEnumerable<AggregateIssue> ValidateAggregate(
        string typeKey, string prefix, JsonElement payload, AggregateContext? context)
    {
        if (typeKey == "building")
        {
            return ValidateBuildingAggregate(prefix, payload, context);
        }

        return ValidateCaveAggregate(prefix, payload, context);
    }

    private static IEnumerable<AggregateIssue> ValidateBuildingAggregate(
        string prefix, JsonElement payload, AggregateContext? context)
    {
        var levelIds = CollectIds(payload, "levels", "level_id");
        var spaceIds = CollectIds(payload, "spaces", "space_id");
        var boundaryIds = CollectIds(payload, "boundaries", "boundary_id");
        var entranceIds = CollectAccessIds(payload, "entrances", "entrance_id");
        var exteriorIds = CollectAccessIds(payload, "exterior_anchors", "anchor_id");
        var componentIds = CollectIds(payload, "geometry_components", "geometry_component_id");

        // Child ID 唯一性（段内）。
        if (spaceIds.HasDuplicate)
        {
            yield return new AggregateIssue { ErrorCode = "CD_BLD_DUPLICATE_SPACE_ID", Message = "space_id 必须在本 Building 内唯一。" };
        }

        if (componentIds.HasDuplicate)
        {
            yield return new AggregateIssue { ErrorCode = "CD_BLD_DUPLICATE_COMPONENT_ID", Message = "geometry_component_id 必须在本 Building 内唯一。" };
        }

        // 计划切片 → Level 引用。
        foreach (var slice in IterateItems(payload, "plans", "level_slices"))
        {
            var levelId = TryGetString(slice, "level_id");
            if (levelId is not null && !levelIds.Contains(levelId))
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_LEVEL_REF_UNRESOLVED", Message = $"level_slices 引用未声明的 Level \"{levelId}\"。" };
            }
        }

        // 组件 child_context 解析。
        foreach (var component in IterateItems(payload, "geometry_components"))
        {
            if (!TryGetProperty(component, "child_context", out var childContext))
            {
                continue;
            }

            if (TryGetString(childContext, "space_id") is { } sid && !spaceIds.Contains(sid)
                || TryGetString(childContext, "level_id") is { } lid && !levelIds.Contains(lid)
                || TryGetString(childContext, "slice_id") is { } zid && !CollectIds(payload, "plans", "level_slices", "slice_id").Contains(zid)
                || TryGetString(childContext, "boundary_id") is { } bid && !boundaryIds.Contains(bid)
                || TryGetString(childContext, "entrance_id") is { } eid && !entranceIds.Contains(eid))
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_COMPONENT_CONTEXT_UNRESOLVED", Message = "geometry child_context 引用未解析到本 Building 聚合。" };
            }
        }

        // boundary 参与者解析。
        foreach (var boundary in IterateItems(payload, "boundaries"))
        {
            if (!TryGetProperty(boundary, "participating_space_ids", out var participants))
            {
                continue;
            }

            foreach (var participant in participants.EnumerateArray())
            {
                var participantId = participant.GetString();
                if (participantId is not null && !spaceIds.Contains(participantId))
                {
                    yield return new AggregateIssue { ErrorCode = "CD_BLD_BOUNDARY_PARTICIPANT_UNRESOLVED", Message = $"boundary 参与者 \"{participantId}\" 未解析。" };
                }
            }
        }

        // Opening anchor 区间 + boundary 解析。
        foreach (var opening in IterateAccessItems(payload, "openings"))
        {
            var boundaryId = TryGetString(opening, "boundary_id");
            if (boundaryId is not null && !boundaryIds.Contains(boundaryId))
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_ACCESS_ENDPOINT_UNRESOLVED", Message = $"opening boundary \"{boundaryId}\" 未解析。" };
            }

            if (TryGetProperty(opening, "anchor", out var anchor)
                && TryGetNumber(anchor, "parameter_start") is { } start
                && TryGetNumber(anchor, "parameter_end") is { } end
                && start > end)
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_OPENING_ANCHOR_INTERVAL", Message = "opening anchor 区间必须有序（parameter_start ≤ parameter_end）。" };
            }
        }

        // Access connection 端点解析 + 自连接。
        foreach (var connection in IterateAccessItems(payload, "connections"))
        {
            var endpointErrors = 0;
            JsonElement? firstEndpoint = null;
            var firstSet = false;
            var selfLink = false;
            foreach (var endpoint in IterateArray(connection, "endpoints"))
            {
                if (!firstSet)
                {
                    firstEndpoint = endpoint;
                    firstSet = true;
                }
                else if (firstEndpoint is { } first && EndpointEquals(first, endpoint))
                {
                    selfLink = true;
                }

                if (!EndpointResolves(endpoint, spaceIds, entranceIds, exteriorIds))
                {
                    endpointErrors++;
                }
            }

            if (endpointErrors > 0)
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_ACCESS_ENDPOINT_UNRESOLVED", Message = "connection endpoint 引用未解析到兼容 child identity。" };
            }

            if (selfLink)
            {
                yield return new AggregateIssue { ErrorCode = "CD_BLD_CONNECTION_SELF_LINK", Message = "connection 不得连接端点到自身。" };
            }
        }
    }

    private static IEnumerable<AggregateIssue> ValidateCaveAggregate(
        string prefix, JsonElement payload, AggregateContext? context)
    {
        var frameIds = CollectIds(payload, "survey_frames", "survey_frame_id");
        var chamberIds = CollectIds(payload, "chambers", "chamber_id");
        var passageIds = CollectIds(payload, "passages", "passage_id");
        var entranceIds = CollectIds(payload, "entrances", "entrance_id");
        var stationIds = CollectIds(payload, "survey_stations", "station_id");
        var componentIds = CollectIds(payload, "geometry_components", "geometry_component_id");

        // Cave System 必须声明至少一个 Survey Frame（P2-09-37 §5.2：全部测量/组件坐标
        // 上下文以 frame 为锚；空 frames = 未声明坐标语义，CAV-002）。
        if (frameIds.Ids.Count == 0)
        {
            yield return new AggregateIssue { ErrorCode = "CD_CAVE_SURVEY_FRAME_REQUIRED", Message = "Cave System 必须声明至少一个 Survey Frame。" };
        }

        if (stationIds.HasDuplicate)
        {
            yield return new AggregateIssue { ErrorCode = "CD_CAVE_DUPLICATE_STATION_ID", Message = "station_id 必须在本 Cave System 内唯一。" };
        }

        if (componentIds.HasDuplicate)
        {
            yield return new AggregateIssue { ErrorCode = "CD_CAVE_DUPLICATE_COMPONENT_ID", Message = "geometry_component_id 必须在本 Cave System 内唯一。" };
        }

        foreach (var station in IterateItems(payload, "survey_stations"))
        {
            var frameId = TryGetString(station, "survey_frame_id");
            if (frameId is not null && !frameIds.Contains(frameId))
            {
                yield return new AggregateIssue { ErrorCode = "CD_CAVE_FRAME_REF_UNRESOLVED", Message = $"station 引用未声明的 Survey Frame \"{frameId}\"。" };
            }

            // station → point component + station 上下文匹配。
            if (TryGetString(station, "geometry_component_id") is { } componentId)
            {
                var component = FindById(payload, "geometry_components", "geometry_component_id", componentId);
                var roleOk = component is not null && TryGetString(component.Value, "geometry_role") == "survey_station";
                var contextOk = component is not null
                    && TryGetProperty(component.Value, "child_context", out var childContext)
                    && TryGetString(childContext, "station_id") == TryGetString(station, "station_id");
                if (!roleOk || !contextOk)
                {
                    yield return new AggregateIssue { ErrorCode = "CD_CAVE_STATION_COMPONENT_ROLE_MISMATCH", Message = "survey station 必须引用带匹配 station 上下文的 survey_station 组件。" };
                }
            }
        }

        foreach (var component in IterateItems(payload, "geometry_components"))
        {
            var coordinateContext = TryGetString(component, "coordinate_context_id");
            if (coordinateContext is not null && !frameIds.Contains(coordinateContext))
            {
                yield return new AggregateIssue { ErrorCode = "CD_CAVE_COORDINATE_CONTEXT_UNRESOLVED", Message = $"geometry component coordinate_context \"{coordinateContext}\" 未解析到 Survey Frame。" };
            }

            if (TryGetProperty(component, "child_context", out var childContext))
            {
                if (TryGetString(childContext, "chamber_id") is { } cid && !chamberIds.Contains(cid)
                    || TryGetString(childContext, "passage_id") is { } pid && !passageIds.Contains(pid)
                    || TryGetString(childContext, "entrance_id") is { } eid && !entranceIds.Contains(eid)
                    || TryGetString(childContext, "station_id") is { } sid && !stationIds.Contains(sid))
                {
                    yield return new AggregateIssue { ErrorCode = "CD_CAVE_COMPONENT_CONTEXT_UNRESOLVED", Message = "geometry child_context 引用未解析到本 Cave 聚合。" };
                }
            }
        }

        // 连接端点解析 + 跨 owner。
        foreach (var connection in IterateItems(payload, "connections"))
        {
            foreach (var endpointKey in new[] { "from_endpoint", "to_endpoint" })
            {
                if (!TryGetProperty(connection, endpointKey, out var endpoint))
                {
                    continue;
                }

                var endpointId = TryGetString(endpoint, "id");
                var kind = TryGetString(endpoint, "kind");
                var resolved = kind switch
                {
                    "chamber" => chamberIds.Contains(endpointId),
                    "passage" => passageIds.Contains(endpointId),
                    "station" => stationIds.Contains(endpointId),
                    "entrance" => entranceIds.Contains(endpointId),
                    _ => endpointId is not null,
                };
                if (!resolved)
                {
                    yield return new AggregateIssue { ErrorCode = "CD_CAVE_CONNECTION_ENDPOINT_UNRESOLVED", Message = $"connection {endpointKey} \"{endpointId}\" 未解析。" };
                }
            }
        }

        if (context?.OwnerObjectId is not null
            && context.EndpointOwnerObjectId is not null
            && !string.Equals(context.OwnerObjectId, context.EndpointOwnerObjectId, StringComparison.Ordinal))
        {
            yield return new AggregateIssue { ErrorCode = "CD_CAVE_CROSS_OWNER_ENDPOINT", Message = "connection 端点引用了另一 Cave System 的 child identity（跨 owner 拒绝）。" };
        }
    }

    // ---- publication 层 ----

    private static IEnumerable<AggregateIssue> ValidatePublication(
        string prefix, JsonElement payload, AggregatePublicationContext publication)
    {
        if (publication.ExpectedDefinitionHash is not null && publication.CurrentDefinitionHash is not null
            && !string.Equals(publication.ExpectedDefinitionHash, publication.CurrentDefinitionHash, StringComparison.Ordinal))
        {
            yield return new AggregateIssue
            {
                ErrorCode = $"{prefix}_STALE_DEFINITION_CAS",
                Message = "expected definition hash 与 current 不匹配（stale CAS），拒绝覆写。",
            };
        }

        var componentAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in IterateItems(payload, "geometry_components"))
        {
            var assetId = TryGetString(component, "asset_id");
            var contentHash = TryGetString(component, "content_hash");
            if (assetId is null || contentHash is null)
            {
                continue; // Typed schema validation reports missing required fields.
            }

            if (componentAssets.TryGetValue(assetId, out var existingHash))
            {
                if (!string.Equals(existingHash, contentHash, StringComparison.Ordinal))
                {
                    yield return new AggregateIssue
                    {
                        ErrorCode = $"{prefix}_ASSET_COMPONENT_HASH_CONFLICT",
                        Message = $"同一 asset_id {assetId} 被组件固定到不同 content_hash。",
                    };
                }
            }
            else
            {
                componentAssets.Add(assetId, contentHash);
            }
        }

        // publication context 表示一次正式发布尝试：即使 link 清单为空，也不得绕过
        // 与 owner manifest 引用的 Asset 集合的精确一致性校验。
        var linkAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in publication.AssetLinks)
        {
            if (!string.Equals(link.Role, "geometry_component", StringComparison.Ordinal))
            {
                yield return new AggregateIssue
                {
                    ErrorCode = $"{prefix}_ASSET_LINK_ROLE_INVALID",
                    Message = $"geometry Asset typed link role 必须为 geometry_component，实际为 {link.Role}。",
                };
            }

            if (!linkAssetIds.Add(link.AssetId))
            {
                yield return new AggregateIssue
                {
                    ErrorCode = $"{prefix}_ASSET_LINK_DUPLICATE",
                    Message = $"Asset {link.AssetId} 在同一 owner 的 geometry_component typed links 中重复。",
                };
            }
        }

        if (!linkAssetIds.SetEquals(componentAssets.Keys))
        {
            yield return new AggregateIssue
            {
                ErrorCode = $"{prefix}_ASSET_LINK_MISMATCH",
                Message = "typed link 清单与 owner payload 的组件 asset_id 集合不一致。",
            };
        }

        var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in publication.AssetCatalog)
        {
            if (!catalog.TryAdd(asset.AssetId, asset.ContentHash))
            {
                yield return new AggregateIssue
                {
                    ErrorCode = $"{prefix}_ASSET_CATALOG_DUPLICATE",
                    Message = $"core_asset 目录中 Asset {asset.AssetId} 重复。",
                };
            }
        }

        foreach (var (assetId, expectedHash) in componentAssets)
        {
            if (!catalog.TryGetValue(assetId, out var actualHash))
            {
                yield return new AggregateIssue
                {
                    ErrorCode = $"{prefix}_ASSET_MISSING",
                    Message = $"core_asset 目录缺少 manifest 引用的 Asset {assetId}。",
                };
            }
            else if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                yield return new AggregateIssue
                {
                    ErrorCode = $"{prefix}_ASSET_HASH_MISMATCH",
                    Message = $"core_asset content_hash 与组件 pinned hash 不一致（asset {assetId}）。",
                };
            }
        }
    }

    // ---- measurement 层 ----

    private static IEnumerable<AggregateIssue> ValidateMeasurement(
        string prefix, JsonElement payload, AggregateContext context)
    {
        if (!string.Equals(context.MeasurementSemantics, "absolute_world_altitude", StringComparison.Ordinal))
        {
            yield break;
        }

        var frameId = context.SurveyFrameId;
        var frame = frameId is null
            ? null
            : FindById(payload, "survey_frames", "survey_frame_id", frameId);
        if (frame is not null && (!TryGetProperty(frame.Value, "vertical_reference_id", out _) || TryGetString(frame.Value, "vertical_reference_id") is null))
        {
            yield return new AggregateIssue
            {
                ErrorCode = "CD_CAVE_ABSOLUTE_ALTITUDE_WITHOUT_DATUM",
                Message = "局部 frame 无 vertical_reference_id 时不得表达绝对世界高程（Unknown ≠ 0）。",
            };
        }
    }

    // ---- JSON 导航辅助 ----

    private static string ReadSchemaResource(string resourceName)
    {
        var assembly = typeof(BuildingCaveAggregateValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new RegistryException("schema_invalid", $"嵌入 schema 资源 {resourceName} 缺失（部署损坏）。");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static JsonElement? TryNavigate(JsonElement root, string path)
    {
        var current = root;
        if (path.Length == 0)
        {
            return current;
        }

        foreach (var segment in path.Split('/').Skip(1))
        {
            var token = segment.Replace("~1", "/").Replace("~0", "~");
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index))
            {
                var array = current.EnumerateArray().ToArray();
                if (index < 0 || index >= array.Length)
                {
                    return null;
                }

                current = array[index];
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(token, out var child))
            {
                current = child;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static IdSet CollectIds(JsonElement payload, string section, string idField)
    {
        var ids = new List<string>();
        foreach (var item in IterateItems(payload, section))
        {
            var id = TryGetString(item, idField);
            if (id is not null)
            {
                ids.Add(id);
            }
        }

        return new IdSet(ids);
    }

    private static IdSet CollectIds(JsonElement payload, string section, string arrayField, string idField)
    {
        var ids = new List<string>();
        foreach (var group in IterateItems(payload, section))
        {
            foreach (var item in IterateArray(group, arrayField))
            {
                var id = TryGetString(item, idField);
                if (id is not null)
                {
                    ids.Add(id);
                }
            }
        }

        return new IdSet(ids);
    }

    private static IdSet CollectAccessIds(JsonElement payload, string accessSection, string idField)
    {
        var ids = new List<string>();
        if (payload.TryGetProperty("access", out var access))
        {
            foreach (var item in IterateArray(access, accessSection))
            {
                var id = TryGetString(item, idField);
                if (id is not null)
                {
                    ids.Add(id);
                }
            }
        }

        return new IdSet(ids);
    }

    /// <summary>ID 收集结果：保留原始序列（重复检测）+ 去重视图（引用解析）。</summary>
    private sealed class IdSet
    {
        public IdSet(IReadOnlyList<string> ids)
        {
            Ids = ids;
            Distinct = ids.ToHashSet(StringComparer.Ordinal);
        }

        public IReadOnlyList<string> Ids { get; }

        public HashSet<string> Distinct { get; }

        public bool HasDuplicate => Ids.Count != Distinct.Count;

        public bool Contains(string? id) => id is not null && Distinct.Contains(id);
    }

    private static IEnumerable<JsonElement> IterateAccessItems(JsonElement payload, string accessSection)
    {
        if (payload.TryGetProperty("access", out var access))
        {
            foreach (var item in IterateArray(access, accessSection))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<JsonElement> IterateItems(JsonElement payload, string section)
    {
        if (payload.TryGetProperty(section, out var sectionElement) && sectionElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sectionElement.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<JsonElement> IterateItems(JsonElement payload, string section, string nested)
    {
        foreach (var item in IterateItems(payload, section))
        {
            if (TryGetProperty(item, nested, out var nestedElement) && nestedElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var nestedItem in nestedElement.EnumerateArray())
                {
                    yield return nestedItem;
                }
            }
        }
    }

    private static IEnumerable<JsonElement> IterateArray(JsonElement parent, string property)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static JsonElement? FindById(JsonElement payload, string section, string idField, string id)
    {
        foreach (var item in IterateItems(payload, section))
        {
            if (TryGetString(item, idField) == id)
            {
                return item;
            }
        }

        return null;
    }

    private static bool EndpointEquals(JsonElement a, JsonElement b) =>
        string.Equals(TryGetString(a, "kind"), TryGetString(b, "kind"), StringComparison.Ordinal)
        && string.Equals(TryGetString(a, "id") ?? TryGetString(a, "space_id") ?? TryGetString(a, "entrance_id") ?? TryGetString(a, "anchor_id"),
            TryGetString(b, "id") ?? TryGetString(b, "space_id") ?? TryGetString(b, "entrance_id") ?? TryGetString(b, "anchor_id"),
            StringComparison.Ordinal);

    private static bool EndpointResolves(JsonElement endpoint, IdSet spaceIds, IdSet entranceIds, IdSet exteriorIds)
    {
        var kind = TryGetString(endpoint, "kind");
        return kind switch
        {
            "space" => TryGetString(endpoint, "space_id") is { } sid && spaceIds.Contains(sid),
            "entrance" => TryGetString(endpoint, "entrance_id") is { } eid && entranceIds.Contains(eid),
            "exterior_anchor" => TryGetString(endpoint, "anchor_id") is { } aid && exteriorIds.Contains(aid),
            _ => false,
        };
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static double? TryGetNumber(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string property, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out value);
    }
}
