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
}
