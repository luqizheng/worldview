using System;
using System.Collections.Generic;

namespace WorldView.Domain.Fields;

public enum FieldValueKind
{
    ContinuousScalar,
    CategoricalMask,
}

public sealed record FieldDescriptor
{
    public required string FieldKey { get; init; }
    public required int SchemaVersion { get; init; }
    public required string SemanticsId { get; init; }
    public required FieldValueKind ValueKind { get; init; }
    public IReadOnlySet<string> AllowedUnits { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public bool RequiresVerticalReference { get; init; }
    public IReadOnlySet<string> Categories { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>
/// Compiled V1 field semantics authority, pinned to P2-09-30 V0.1.
/// New field keys require a reviewed descriptor and validation vectors; arbitrary field properties are rejected.
/// </summary>
public static class FieldDescriptorRegistry
{
    private static readonly IReadOnlyDictionary<string, FieldDescriptor> Descriptors =
        new Dictionary<string, FieldDescriptor>(StringComparer.Ordinal)
        {
            ["elevation"] = new FieldDescriptor
            {
                FieldKey = "elevation",
                SchemaVersion = 1,
                SemanticsId = "world-view.field.elevation.v1",
                ValueKind = FieldValueKind.ContinuousScalar,
                AllowedUnits = new HashSet<string>(StringComparer.Ordinal) { "m", "ft" },
                RequiresVerticalReference = true,
            },
            ["land_water"] = new FieldDescriptor
            {
                FieldKey = "land_water",
                SchemaVersion = 1,
                SemanticsId = "world-view.field.land_water.v1",
                ValueKind = FieldValueKind.CategoricalMask,
                Categories = new HashSet<string>(StringComparer.Ordinal)
                {
                    "land", "water", "unknown", "outside_coverage",
                },
            },
        };

    public static IReadOnlyCollection<FieldDescriptor> List() =>
        (IReadOnlyCollection<FieldDescriptor>)Descriptors.Values;

    public static FieldDescriptor Require(string? fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            throw new ArgumentException("field_key 不能为空白。", nameof(fieldKey));
        }

        var key = fieldKey.Trim();
        return Descriptors.TryGetValue(key, out var descriptor)
            ? descriptor
            : throw new ArgumentException($"Field key \"{key}\" 未在 V1 Field Descriptor Registry 中注册。", nameof(fieldKey));
    }

    public static void ValidatePin(string fieldKey, int schemaVersion, string semanticsId)
    {
        var descriptor = Require(fieldKey);
        if (schemaVersion != descriptor.SchemaVersion
            || !string.Equals(semanticsId, descriptor.SemanticsId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Field descriptor pin 不匹配：{fieldKey} 需要 schema_version={descriptor.SchemaVersion}, semantics_id={descriptor.SemanticsId}。");
        }
    }
}
