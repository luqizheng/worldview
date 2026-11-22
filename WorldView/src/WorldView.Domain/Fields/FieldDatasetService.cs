using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WorldView.Domain.Assets;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>
/// Core Domain authority for Map-owned source Field Datasets. Staging and byte publication
/// happen outside this service; this service validates the formal owner boundary and asks
/// the database to atomically append an immutable revision and advance its current pointer.
/// </summary>
public static class FieldDatasetService
{
    public static FieldRevisionRecord PublishRevision(
        ICoreDomainDatabase database,
        PublishFieldRevisionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(plan);

        if (!FieldDescriptorRegistry.TryGetSemanticsId(
                plan.FieldKey, plan.DescriptorSchemaVersion, out var expectedSemanticsId))
            throw new CoreDomainConflictException(
                $"Field descriptor {plan.FieldKey}@{plan.DescriptorSchemaVersion} is not registered in the Field Descriptor registry.");
        if (!string.Equals(plan.SemanticsId, expectedSemanticsId, StringComparison.Ordinal))
            throw new CoreDomainConflictException(
                $"Field descriptor semantics_id must be {expectedSemanticsId} for {plan.FieldKey}@{plan.DescriptorSchemaVersion}.");
        if (plan.FieldDatasetId == Guid.Empty || plan.FieldRevisionId == Guid.Empty
            || plan.FieldRevisionAssetId == Guid.Empty || plan.ProjectId == Guid.Empty || plan.MapId == Guid.Empty)
            throw new ArgumentException("Field Dataset、Revision、Asset、Project 和 Map ID 均必须为非空 UUID。", nameof(plan));

        var fingerprint = AssetRules.NormalizeContentHash(plan.ContentFingerprint);
        // The Field Package manifest has an explicit RFC 8785 contract. Do not reuse
        // CanonicalJson here: that legacy canonicalizer has a different numeric contract
        // and remains authoritative for existing Core Domain fingerprints.
        var manifest = FieldManifestCanonicalizer.Canonicalize(plan.ManifestJson);
        // Raw package-byte equality (BOM/whitespace/trailing newline) remains the reader's responsibility.

        var provenance = CanonicalJson.Canonicalize(plan.ProvenanceJson);
        ValidateManifestIdentity(manifest, plan);
        var metadata = FieldRevisionMetadata.FromValidatedManifest(manifest);

        var map = database.FindMap(plan.ProjectId, plan.MapId)
            ?? throw new CoreDomainConflictException($"Map {plan.MapId} 不存在或不属于项目 {plan.ProjectId}。");
        if (string.IsNullOrWhiteSpace(map.SpatialReferenceId))
            throw new CoreDomainConflictException("Map 尚未绑定 Spatial Reference，不能发布 Field Revision。");
        var spatialReference = SpatialReferenceService.FindDefinition(
            database, plan.ProjectId, map.SpatialReferenceId)
            ?? throw new CoreDomainConflictException("Map 的 Spatial Reference 定义不存在或不属于当前 Project。");
        SpatialReferenceRules.ValidateFieldOwnerContext(map, spatialReference, metadata);

        var asset = database.FindAsset(plan.ProjectId, plan.FieldRevisionAssetId)
            ?? throw new CoreDomainConflictException(
                $"Field Revision Asset {plan.FieldRevisionAssetId} 不存在或不属于项目 {plan.ProjectId}。");
        if (asset.IntegrityState != AssetIntegrityState.Verified)
            throw new CoreDomainConflictException(
                $"Field Revision Asset {asset.AssetId} 必须先通过字节完整性验证；当前状态为 {asset.IntegrityState}。");

        var current = database.FindFieldDataset(plan.ProjectId, plan.MapId, plan.FieldKey);
        if (current is not null)
        {
            if (current.FieldDatasetId != plan.FieldDatasetId
                || current.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                || !string.Equals(current.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                throw new CoreDomainDataException("Field Dataset identity 或 descriptor pin 与既有权威行不一致。");
            if (current.CurrentFieldRevisionId != plan.ExpectedCurrentFieldRevisionId)
                throw new CoreDomainConflictException("Field Revision publication CAS 冲突：current revision 已变化。");
        }
        else if (plan.ExpectedCurrentFieldRevisionId is not null)
        {
            throw new CoreDomainConflictException("Field Dataset 尚不存在；首次 publication 的 expected current revision 必须为 null。");
        }

        return database.PublishFieldRevision(plan with
        {
            ContentFingerprint = fingerprint,
            ManifestJson = manifest,
            Metadata = metadata,
            ProvenanceJson = provenance,
        });
    }

    private static void ValidateManifestIdentity(
        string canonicalManifest,
        PublishFieldRevisionPlan plan)
    {
        using var document = JsonDocument.Parse(canonicalManifest);
        var root = document.RootElement;
        RequireObject(root, "manifest");
        EnsureNoDuplicateKeys(root);
        RequireProperties(root,
            "byte_order", "canonicalization", "codec_registry", "coverage", "field", "format",
            "grid", "manifest_schema", "map_edge_semantics", "no_data", "spatial", "tile_hash", "value_encoding");

        RequireEqual(RequiredString(root, "byte_order"), "little_endian", "byte_order");
        RequireEqual(RequiredString(root, "canonicalization"), "RFC8785-JCS-UTF8", "canonicalization");

        var schema = RequiredObject(root, "manifest_schema");
        RequireProperties(schema, "id", "version");
        RequireEqual(RequiredString(schema, "id"), "world-view.field-package-manifest", "manifest_schema.id");
        RequireEqual(RequiredInt32(schema, "version"), 1, "manifest_schema.version");

        var format = RequiredObject(root, "format");
        RequireProperties(format, "major", "minor");
        RequireEqual(RequiredInt32(format, "major"), 1, "format.major");
        RequireEqual(RequiredInt32(format, "minor"), 0, "format.minor");

        var field = RequiredObject(root, "field");
        var fieldProperties = plan.FieldKey == "elevation"
            ? new[] { "field_key", "schema_version", "semantics_id", "unit" }
            : new[] { "field_key", "schema_version", "semantics_id" };
        RequireProperties(field, fieldProperties);
        RequireEqual(RequiredString(field, "field_key"), plan.FieldKey, "field.field_key");
        RequireEqual(RequiredInt32(field, "schema_version"), plan.DescriptorSchemaVersion, "field.schema_version");
        var manifestSemanticsId = RequiredString(field, "semantics_id");
        RequireLength(manifestSemanticsId, 1, 160, "field.semantics_id");
        RequireEqual(manifestSemanticsId, plan.SemanticsId, "field.semantics_id");
        if (plan.FieldKey == "elevation")
            RequireOneOf(RequiredString(field, "unit"), "m", "ft");

        var spatial = RequiredObject(root, "spatial");
        var spatialProperties = plan.FieldKey == "elevation"
            ? new[] { "map_space_id", "spatial_reference_id", "coordinate_precision_profile_id", "vertical_reference_id" }
            : new[] { "map_space_id", "spatial_reference_id", "coordinate_precision_profile_id" };
        RequireProperties(spatial, spatialProperties);
        RequireLength(RequiredString(spatial, "map_space_id"), 1, 256, "spatial.map_space_id");
        RequireLength(RequiredString(spatial, "spatial_reference_id"), 1, 512, "spatial.spatial_reference_id");
        RequireLength(RequiredString(spatial, "coordinate_precision_profile_id"), 1, 160,
            "spatial.coordinate_precision_profile_id");
        if (plan.FieldKey == "elevation")
            RequireLength(RequiredString(spatial, "vertical_reference_id"), 1, 512,
                "spatial.vertical_reference_id");

        var grid = RequiredObject(root, "grid");
        RequireProperties(grid,
            "width", "height", "tile_width", "tile_height", "origin_x", "origin_y",
            "spacing_x", "spacing_y", "axis_orientation", "cell_registration");
        if (RequiredInt64(grid, "width") is < 1 or > 9007199254740991
            || RequiredInt64(grid, "height") is < 1 or > 9007199254740991)
            throw new CoreDomainConflictException("Field manifest grid dimensions must be positive safe integers.");
        if (RequiredInt32(grid, "tile_width") is < 1 or > 65535
            || RequiredInt32(grid, "tile_height") is < 1 or > 65535)
            throw new CoreDomainConflictException("Field manifest tile dimensions must be in the uint16 range.");
        if (!DecimalPattern.IsMatch(RequiredString(grid, "origin_x"))
            || !DecimalPattern.IsMatch(RequiredString(grid, "origin_y"))
            || !PositiveDecimalPattern.IsMatch(RequiredString(grid, "spacing_x"))
            || !PositiveDecimalPattern.IsMatch(RequiredString(grid, "spacing_y")))
            throw new CoreDomainConflictException("Field manifest grid coordinates/spacing must use canonical decimal strings.");
        RequireOneOf(RequiredString(grid, "axis_orientation"),
            "x_east_y_north", "x_east_y_south", "x_west_y_north", "x_west_y_south");
        RequireOneOf(RequiredString(grid, "cell_registration"), "area", "point");

        var coverage = RequiredObject(root, "coverage");
        var coverageMode = RequiredString(coverage, "mode");
        if (coverageMode == "full_grid")
            RequireProperties(coverage, "mode");
        else if (coverageMode == "explicit_mask")
        {
            RequireProperties(coverage, "mode", "mask_plane_name");
            RequireLength(RequiredString(coverage, "mask_plane_name"), 1, 64, "coverage.mask_plane_name");
        }
        else
            throw new CoreDomainConflictException("Field manifest coverage.mode is unsupported.");

        RequireOneOf(RequiredString(root, "map_edge_semantics"),
            "bounded_non_wrapping", "wrap_x", "wrap_y", "wrap_xy", "unknown");

        var noData = RequiredObject(root, "no_data");
        RequireProperties(noData, "policy", "mask_plane");
        RequireEqual(RequiredString(noData, "policy"), "explicit_validity_mask", "no_data.policy");
        var mask = RequiredObject(noData, "mask_plane");
        RequireProperties(mask, "plane_name", "valid_value", "invalid_value");
        RequireEqual(RequiredString(mask, "plane_name"), "validity_mask", "no_data.mask_plane.plane_name");
        RequireEqual(RequiredInt32(mask, "valid_value"), 0, "no_data.mask_plane.valid_value");
        RequireEqual(RequiredInt32(mask, "invalid_value"), 1, "no_data.mask_plane.invalid_value");

        var encoding = RequiredObject(root, "value_encoding");
        var allowedEncodingProperties = plan.FieldKey == "land_water"
            ? new[] { "profile_id", "profile_scope", "planes", "category_codes" }
            : new[] { "profile_id", "profile_scope", "planes" };
        RequireProperties(encoding, allowedEncodingProperties);
        RequireLength(RequiredString(encoding, "profile_id"), 1, 160, "value_encoding.profile_id");
        RequireOneOf(RequiredString(encoding, "profile_scope"), "golden_fixture_only", "production");
        var planes = RequiredArray(encoding, "planes");
        if (planes.GetArrayLength() is < 1 or > 8)
            throw new CoreDomainConflictException("Field manifest value_encoding.planes count is outside registered bounds.");
        var planeFormats = new Dictionary<string, (string Format, int Bytes)>(StringComparer.Ordinal);
        foreach (var plane in planes.EnumerateArray())
        {
            RequireObject(plane, "value_encoding plane");
            RequireProperties(plane, "name", "sample_format", "bytes_per_cell");
            var name = RequiredString(plane, "name");
            RequireLength(name, 1, 64, "value_encoding.planes.name");
            var sampleFormat = RequiredString(plane, "sample_format");
            RequireOneOf(sampleFormat,
                "uint8", "uint8_registered_category", "int16_le", "uint16_le",
                "int32_le", "uint32_le", "float32_ieee754_le", "float64_ieee754_le");
            var bytesPerCell = RequiredInt32(plane, "bytes_per_cell");
            var expectedBytes = sampleFormat switch
            {
                "uint8" or "uint8_registered_category" => 1,
                "int16_le" or "uint16_le" => 2,
                "int32_le" or "uint32_le" or "float32_ieee754_le" => 4,
                "float64_ieee754_le" => 8,
                _ => throw new CoreDomainConflictException($"Unsupported sample format {sampleFormat}."),
            };
            if (bytesPerCell != expectedBytes)
                throw new CoreDomainConflictException($"Field manifest plane {name} bytes_per_cell does not match {sampleFormat}.");
            if (!planeFormats.TryAdd(name, (sampleFormat, bytesPerCell)))
                throw new CoreDomainConflictException($"Field manifest contains duplicate value plane {name}.");
        }

        if (!planeFormats.TryGetValue("validity_mask", out var validityMask)
            || validityMask != ("uint8", 1))
            throw new CoreDomainConflictException("Field manifest requires a uint8 validity_mask plane.");
        if (coverageMode == "explicit_mask"
            && !planeFormats.ContainsKey(RequiredString(coverage, "mask_plane_name")))
            throw new CoreDomainConflictException("Field manifest coverage mask plane is not declared in value_encoding.planes.");

        if (plan.FieldKey == "elevation")
        {
            if (!planeFormats.TryGetValue("samples", out var samples)
                || samples.Format is "uint8" or "uint8_registered_category")
                throw new CoreDomainConflictException("elevation requires a numeric samples plane.");
        }

        if (plan.FieldKey == "land_water")
        {
            if (!planeFormats.TryGetValue("categories", out var categoryPlane)
                || categoryPlane != ("uint8_registered_category", 1))
                throw new CoreDomainConflictException("land_water requires a uint8_registered_category categories plane.");
            var categories = RequiredObject(encoding, "category_codes");
            RequireProperties(categories, "land", "water", "unknown", "outside_coverage");
            var codes = new[]
            {
                RequiredInt32(categories, "land"),
                RequiredInt32(categories, "water"),
                RequiredInt32(categories, "unknown"),
                RequiredInt32(categories, "outside_coverage"),
            };
            if (codes.Any(code => code is < 0 or > 255) || codes.Distinct().Count() != 4)
                throw new CoreDomainConflictException("land_water category codes must be distinct uint8 values.");
        }

        var codecs = RequiredArray(root, "codec_registry");
        if (codecs.GetArrayLength() is < 1 or > 32)
            throw new CoreDomainConflictException("Field manifest codec_registry count is outside registered bounds.");
        var codecIds = new HashSet<int>();
        var hasNoneCodec = false;
        foreach (var codec in codecs.EnumerateArray())
        {
            RequireObject(codec, "codec_registry entry");
            RequireProperties(codec, "codec_id", "codec_name", "version", "parameters");
            var codecId = RequiredInt32(codec, "codec_id");
            var codecName = RequiredString(codec, "codec_name");
            RequireLength(codecName, 1, 64, "codec_registry.codec_name");
            RequireLength(RequiredString(codec, "version"), 1, 64, "codec_registry.version");
            RequireObject(RequiredObject(codec, "parameters"), "codec parameters");
            if (codecId is < 0 or > 65535 || !codecIds.Add(codecId))
                throw new CoreDomainConflictException("Field manifest codec_registry contains an invalid or duplicate codec_id.");
            if (codecId == 0)
            {
                if (!string.Equals(codecName, "none", StringComparison.Ordinal))
                    throw new CoreDomainConflictException("Field manifest codec_id 0 must be named none.");
                hasNoneCodec = true;
            }
        }
        if (!hasNoneCodec)
            throw new CoreDomainConflictException("Field manifest codec_registry must include codec 0 (none).");

        var tileHash = RequiredObject(root, "tile_hash");
        RequireProperties(tileHash, "algorithm", "version");
        RequireEqual(RequiredString(tileHash, "algorithm"), "SHA-256", "tile_hash.algorithm");
        RequireEqual(RequiredInt32(tileHash, "version"), 1, "tile_hash.version");

    }

    private static readonly Regex DecimalPattern = new(
        "^(0|[1-9][0-9]*|-[1-9][0-9]*|-?0\\.[0-9]*[1-9]|-[1-9][0-9]*\\.[0-9]*[1-9])$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PositiveDecimalPattern = new(
        "^(0\\.[0-9]*[1-9]|[1-9][0-9]*(\\.[0-9]*[1-9])?)$",
        RegexOptions.CultureInvariant);

    private static void EnsureNoDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new CoreDomainConflictException($"Field manifest contains duplicate JSON property {property.Name}.");
                EnsureNoDuplicateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                EnsureNoDuplicateKeys(item);
        }
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new CoreDomainConflictException($"Field manifest {name} must be a JSON object.");
    }

    private static JsonElement RequiredObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new CoreDomainConflictException($"Field manifest requires object property {name}.");
        return value;
    }

    private static JsonElement RequiredArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new CoreDomainConflictException($"Field manifest requires array property {name}.");
        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new CoreDomainConflictException($"Field manifest requires non-empty string property {name}.");
        return value.GetString()!;
    }

    private static int RequiredInt32(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number))
            throw new CoreDomainConflictException($"Field manifest requires integer property {name}.");
        return number;
    }

    private static long RequiredInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var number))
            throw new CoreDomainConflictException($"Field manifest requires integer property {name}.");
        return number;
    }

    private static void RequireProperties(JsonElement value, params string[] expectedNames)
    {
        var expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!expected.Remove(property.Name))
                throw new CoreDomainConflictException($"Field manifest contains unknown property {property.Name}.");
        if (expected.Count != 0)
            throw new CoreDomainConflictException($"Field manifest is missing required property {expected.First()}.");
    }

    private static void RequireLength(string value, int minimum, int maximum, string name)
    {
        if (value.Length < minimum || value.Length > maximum)
            throw new CoreDomainConflictException(
                $"Field manifest {name} length must be between {minimum} and {maximum} characters.");
    }

    private static void RequireEqual<T>(T actual, T expected, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new CoreDomainConflictException($"Field manifest {name} must equal {expected}.");
    }

    private static void RequireOneOf(string actual, params string[] allowed)
    {
        if (!allowed.Contains(actual, StringComparer.Ordinal))
            throw new CoreDomainConflictException($"Field manifest value {actual} is not in the registered vocabulary.");
    }
}
