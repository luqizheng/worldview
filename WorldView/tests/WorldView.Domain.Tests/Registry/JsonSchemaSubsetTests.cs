using System;
using System.Text.Json;
using WorldView.Domain.Registry;
using Xunit;

namespace WorldView.Domain.Tests.Registry;

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

        var valid = Validate(schema, """{"field_key":"land_water","category_codes":{}}""");
        var invalid = Validate(schema, """{"field_key":"land_water"}""");

        Assert.Empty(valid);
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
        const string schema = """{"not":{"required":["category_codes"]}}""";

        var violations = Validate(schema, """{"category_codes":{}}""");

        Assert.Contains(violations, v => v.Keyword == "not");
    }

    [Fact]
    public void Not_WhenSubschemaDoesNotMatch_AcceptsInstance()
    {
        const string schema = """{"not":{"required":["category_codes"]}}""";

        Assert.Empty(Validate(schema, """{"field_key":"elevation"}"""));
    }

    [Fact]
    public void ConditionalSubschema_WithUnsupportedKeyword_FailsClosed()
    {
        const string schema = """{"if":{"contains":{"const":1}},"then":{"type":"array"}}""";
        using var schemaDocument = JsonDocument.Parse(schema);
        using var instanceDocument = JsonDocument.Parse("[]");

        var error = Assert.Throws<RegistryException>(
            () => JsonSchemaSubset.Validate(schemaDocument.RootElement, instanceDocument.RootElement));

        Assert.Equal("schema_invalid", error.Code);
        Assert.Contains("contains", error.Message);
    }

    private static System.Collections.Generic.IReadOnlyList<SchemaViolation> Validate(string schema, string instance)
    {
        using var schemaDocument = JsonDocument.Parse(schema);
        using var instanceDocument = JsonDocument.Parse(instance);
        return JsonSchemaSubset.Validate(schemaDocument.RootElement, instanceDocument.RootElement);
    }

    [Fact]
    public void P2_09_34_GoldenManifests_SatisfyCanonicalSchema()
    {
        using var schema = LoadResource(
            typeof(JsonSchemaSubset).Assembly,
            "WorldView.Domain.Registry.p2-09-34-field-package-canonical-manifest-schema-v0.1.json");
        foreach (var fixtureName in new[]
        {
            "WorldView.Domain.Tests.Fixtures.minimal-elevation-2x2.manifest.json",
            "WorldView.Domain.Tests.Fixtures.land-water-4x2-two-tiles.manifest.json",
        })
        {
            using var fixture = LoadResource(typeof(JsonSchemaSubsetTests).Assembly, fixtureName);
            Assert.Empty(JsonSchemaSubset.Validate(schema.RootElement, fixture.RootElement));
        }
    }

    [Fact]
    public void P2_09_34_LandWaterWithoutCategoryCodes_IsRejectedByConditionalRule()
    {
        using var schema = LoadResource(
            typeof(JsonSchemaSubset).Assembly,
            "WorldView.Domain.Registry.p2-09-34-field-package-canonical-manifest-schema-v0.1.json");
        using var fixture = LoadResource(
            typeof(JsonSchemaSubsetTests).Assembly,
            "WorldView.Domain.Tests.Fixtures.land-water-4x2-two-tiles.manifest.json");
        var mutable = System.Text.Json.Nodes.JsonNode.Parse(fixture.RootElement.GetRawText())!.AsObject();
        mutable["value_encoding"]!.AsObject().Remove("category_codes");
        using var invalid = JsonDocument.Parse(mutable.ToJsonString());

        var violations = JsonSchemaSubset.Validate(schema.RootElement, invalid.RootElement);

        Assert.Contains(violations, v => v.Keyword == "required" && v.PropertyName == "category_codes");
    }

    [Fact]
    public void P2_09_34_ElevationWithCategoryCodes_IsRejectedByConditionalRule()
    {
        using var schema = LoadResource(
            typeof(JsonSchemaSubset).Assembly,
            "WorldView.Domain.Registry.p2-09-34-field-package-canonical-manifest-schema-v0.1.json");
        using var fixture = LoadResource(
            typeof(JsonSchemaSubsetTests).Assembly,
            "WorldView.Domain.Tests.Fixtures.minimal-elevation-2x2.manifest.json");
        var mutable = System.Text.Json.Nodes.JsonNode.Parse(fixture.RootElement.GetRawText())!.AsObject();
        mutable["value_encoding"]!.AsObject()["category_codes"] = new System.Text.Json.Nodes.JsonObject
        {
            ["land"] = 0,
            ["water"] = 1,
            ["unknown"] = 2,
            ["outside_coverage"] = 3,
        };
        using var invalid = JsonDocument.Parse(mutable.ToJsonString());

        var violations = JsonSchemaSubset.Validate(schema.RootElement, invalid.RootElement);

        Assert.Contains(violations, v => v.Keyword == "not");
    }

    private static JsonDocument LoadResource(System.Reflection.Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        return JsonDocument.Parse(stream);
    }
}
