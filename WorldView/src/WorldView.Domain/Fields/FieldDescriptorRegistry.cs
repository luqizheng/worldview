namespace WorldView.Domain.Fields;

/// <summary>Static V1 Core Domain Field Descriptor Registry seed.</summary>
public static class FieldDescriptorRegistry
{
    public static bool TryGetSemanticsId(string fieldKey, int schemaVersion, out string semanticsId)
    {
        semanticsId = (fieldKey, schemaVersion) switch
        {
            ("elevation", 1) => "world-view.field.elevation.v1",
            ("land_water", 1) => "world-view.field.land_water.v1",
            _ => string.Empty,
        };
        return semanticsId.Length != 0;
    }
}
