using System;
using System.Collections.Generic;
using System.Text.Json;
using WorldView.Domain.Registry;
using Xunit;

namespace WorldView.Domain.Tests.Registry;

/// <summary>
/// CD-02-E S3 有界 JSON Schema 子集：条件关键字（if/then/else/not）遵循
/// Draft 2020-12 语义，未知关键字 fail-closed；并使用真实 Building/Cave
/// payload 验证实际 schema 集成行为。
/// </summary>
public sealed class JsonSchemaSubsetTests
{
    [Fact]
    public void IfThen_WhenConditionMatches_AppliesThenSchema()
    {
        const string schema = """
            {
              "type": "object",
              "if": {
                "properties": { "field_key": { "const": "land_water" } },
                "required": ["field_key"]
              },
              "then": { "required": ["category_codes"] }
            }
            """;

        Assert.Empty(Validate(schema, """{"field_key":"land_water","category_codes":{}}"""));
        var invalid = Validate(schema, """{"field_key":"land_water"}""");
        Assert.Contains(invalid, v => v.Keyword == "required" && v.PropertyName == "category_codes");
    }

    [Fact]
    public void IfThen_WhenConditionDoesNotMatch_DoesNotApplyThenSchema()
    {
        const string schema = """
            {
              "type": "object",
              "if": {
                "properties": { "field_key": { "const": "land_water" } },
                "required": ["field_key"]
              },
              "then": { "required": ["category_codes"] }
            }
            """;

        Assert.Empty(Validate(schema, """{"field_key":"elevation"}"""));
    }

    [Fact]
    public void IfElse_WhenConditionDoesNotMatch_AppliesElseSchema()
    {
        const string schema = """
            {
              "type": "object",
              "if": {
                "properties": { "field_key": { "const": "land_water" } },
                "required": ["field_key"]
              },
              "then": { "required": ["category_codes"] },
              "else": { "not": { "required": ["category_codes"] } }
            }
            """;

        var invalid = Validate(schema, """{"field_key":"elevation","category_codes":{}}""");
        Assert.Contains(invalid, v => v.Keyword == "not");
        Assert.Empty(Validate(schema, """{"field_key":"elevation"}"""));
    }

    [Fact]
    public void Not_WhenSubschemaMatches_RejectsInstance()
    {
        Assert.Contains(
            Validate("""{"not":{"required":["category_codes"]}}""", """{"category_codes":{}}"""),
            v => v.Keyword == "not");
    }

    [Fact]
    public void Not_WhenSubschemaDoesNotMatch_AcceptsInstance()
    {
        Assert.Empty(Validate("""{"not":{"required":["category_codes"]}}""", """{"field_key":"elevation"}"""));
    }

    [Fact]
    public void ConditionalSubschema_WithUnsupportedKeyword_FailsClosed()
    {
        const string schema = """{"if":{"contains":{"const":1}},"then":{"type":"array"}}""";
        using var schemaDocument = JsonDocument.Parse(schema);
        using var instanceDocument = JsonDocument.Parse("[]");

        var error = Assert.Throws<RegistryException>(
            () => JsonSchemaSubset.Validate(schemaDocument.RootElement, instanceDocument.RootElement));

        Assert.Contains("contains", error.Message);
    }

    // ---- 真实 Building / Cave payload 与条件 schema 的集成 ----

    [Fact]
    public void BuildingPayload_ConditionalRule_MatchesRealSchemaShape()
    {
        // 基于真实 Building 根结构：residential 时要求 vertical_frame，非 residential 不要求。
        const string schema = """
            {
              "type": "object",
              "required": ["schema_version", "canonical_name", "building_class", "geometry_components"],
              "properties": {
                "building_class": { "enum": ["residential", "commercial", "civic", "other"] },
                "geometry_components": { "type": "array" }
              },
              "if": {
                "properties": { "building_class": { "const": "residential" } },
                "required": ["building_class"]
              },
              "then": { "required": ["vertical_frame"] }
            }
            """;

        var residential = BuildingPayload.Replace("\"vertical_frame\":", "\"removed_vertical_frame\":");
        Assert.Contains(
            Validate(schema, residential),
            v => v.Keyword == "required" && v.PropertyName == "vertical_frame");

        Assert.Empty(Validate(schema, BuildingPayload));

        var commercial = BuildingPayload.Replace("\"residential\"", "\"commercial\"");
        Assert.Empty(Validate(schema, commercial.Replace("\"vertical_frame\":", "\"removed_vertical_frame\":")));
    }

    [Fact]
    public void BuildingTypedAggregateSchema_RealPayload_Valid()
    {
        ObjectTypeRegistry.ValidateDefinition("building", BuildingPayload);
    }

    [Fact]
    public void BuildingTypedAggregateSchema_MissingRequiredFrame_Rejected()
    {
        var broken = BuildingPayload.Replace("\"vertical_frame\":", "\"unexpected_vertical_frame\":");
        var ex = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinition("building", broken));
        Assert.Equal("definition_invalid", ex.ErrorCode);
    }

    [Fact]
    public void CaveTypedAggregateSchema_RealPayload_Valid()
    {
        ObjectTypeRegistry.ValidateDefinition("cave_system", CavePayload);
    }

    [Fact]
    public void CaveTypedAggregateSchema_MissingRequiredConnection_Rejected()
    {
        var broken = CavePayload.Replace("\"connections\":", "\"unexpected_connections\":");
        var ex = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinition("cave_system", broken));
        Assert.Equal("definition_invalid", ex.ErrorCode);
    }

    private static IReadOnlyList<SchemaViolation> Validate(string schema, string instance)
    {
        using var schemaDocument = JsonDocument.Parse(schema);
        using var instanceDocument = JsonDocument.Parse(instance);
        return JsonSchemaSubset.Validate(schemaDocument.RootElement, instanceDocument.RootElement);
    }

    private const string BuildingPayload = """
        {"schema_version":1,"canonical_name":"Demo Hall","building_class":"residential","description":"demo building","horizontal_frame":{"frame_id":"hframe-1","coordinate_kind":"local_cartesian","unit":"m","origin":{"x":0,"y":0},"orientation_degrees":0,"precision_profile_id":"hp-1"},"vertical_frame":{"vertical_frame_id":"vframe-1","unit":"m","positive_axis":"up","precision_profile_id":"vp-1"},"levels":[{"level_id":"level-ground","level_label":"Ground Floor","level_kind":"ground","display_order":0,"elevation_local":0,"status":"active"}],"spaces":[{"space_id":"space-hall","space_kind":"room","label":"Hall","status":"active"}],"plans":[],"boundaries":[],"access":{"entrances":[],"openings":[],"connections":[],"vertical_connectors":[],"exterior_anchors":[]},"geometry_components":[]}
        """;

    private const string CavePayload = """
        {"schema_version":1,"canonical_name":"Limestone Cave System","cave_class":"limestone","survey_completeness":"partial","survey_frames":[{"survey_frame_id":"survey-frame-1","horizontal_context_kind":"cave_local_cartesian","horizontal_context_id":"cave-local-1","unit":"m","local_vertical_origin":0,"positive_axis":"up","precision_profile_id":"cave-precision-1","survey_method":"manual_survey","frame_version":1,"horizontal_origin":{"x":0,"y":0},"orientation_degrees":0}],"chambers":[{"chamber_id":"chamber-a","chamber_kind":"chamber","label":"Main Chamber","status":"active","survey_confidence":"measured"}],"passages":[{"passage_id":"passage-a","passage_kind":"horizontal","label":"North Passage","status":"active","width_observation":1.4,"height_observation":1.8,"measurement_unit":"m","survey_confidence":"estimated"}],"entrances":[{"entrance_id":"cave-entrance-a","entrance_kind":"natural_opening","label":"North Entrance","status":"active","internal_endpoint_kind":"chamber","internal_endpoint_id":"chamber-a"}],"survey_stations":[{"station_id":"station-a","survey_frame_id":"survey-frame-1","label":"A0","vertical_value":-2,"horizontal_uncertainty":0.05,"vertical_uncertainty":0.1,"geometry_component_id":"cave-station-a"}],"connections":[{"cave_connection_id":"cave-conn-a","from_endpoint":{"kind":"chamber","id":"chamber-a"},"to_endpoint":{"kind":"passage","id":"passage-a"},"connection_kind":"passage_continuity","confidence":"measured"}],"geometry_components":[{"geometry_family":"polygon","dimension":2,"coordinate_context_id":"survey-frame-1","child_context":{"chamber_id":"chamber-a"},"asset_id":"asset-chamber","geometry_component_id":"cave-chamber-plan-a","geometry_role":"chamber_plan","content_hash":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","spatial_reference_id":"spatial-ref-cave-local-1","geometry_schema_version":"worldview.geometry-json/1","provenance":{"source_kind":"authored"},"validity_metadata":{"status":"valid","validation_profile_id":"world-view.geometry.v1","issue_codes":[]}},{"geometry_family":"line_string","dimension":2,"coordinate_context_id":"survey-frame-1","child_context":{"passage_id":"passage-a"},"asset_id":"asset-passage","geometry_component_id":"cave-passage-plan-a","geometry_role":"passage_plan","content_hash":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee","spatial_reference_id":"spatial-ref-cave-local-1","geometry_schema_version":"worldview.geometry-json/1","provenance":{"source_kind":"authored"},"validity_metadata":{"status":"valid","validation_profile_id":"world-view.geometry.v1","issue_codes":[]}},{"geometry_family":"point","dimension":2,"coordinate_context_id":"survey-frame-1","child_context":{"station_id":"station-a"},"asset_id":"asset-station","geometry_component_id":"cave-station-a","geometry_role":"survey_station","content_hash":"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff","spatial_reference_id":"spatial-ref-cave-local-1","geometry_schema_version":"worldview.geometry-json/1","provenance":{"source_kind":"authored"},"validity_metadata":{"status":"valid","validation_profile_id":"world-view.geometry.v1","issue_codes":[]}}]}
        """;
}
