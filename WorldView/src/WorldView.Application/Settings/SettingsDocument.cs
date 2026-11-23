using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WorldView.Application.Settings;

/// <summary>Stable V1 format identifiers for external Project and Map settings documents.</summary>
public static class SettingsDocumentFormat
{
    public const string Project = "worldview.project-settings";
    public const string Map = "worldview.map-settings";
    public const int InitialSchemaVersion = 1;
    public const int MaximumDocumentBytes = 16 * 1024 * 1024;

    public static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Culture = CultureInfo.InvariantCulture,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        FloatFormatHandling = FloatFormatHandling.DefaultValue,
        NullValueHandling = NullValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Error,
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
    };
}

/// <summary>
/// Validated settings document envelope. The settings object is feature-owned and intentionally
/// extensible; this persistence contract does not invent preference keys.
/// </summary>
public sealed record SettingsDocument
{
    private SettingsDocument(
        string format,
        int schemaVersion,
        Guid projectId,
        Guid? mapId,
        JObject settings)
    {
        Format = format;
        SchemaVersion = schemaVersion;
        ProjectId = projectId;
        MapId = mapId;
        Settings = settings;
    }

    public string Format { get; }
    public int SchemaVersion { get; }
    public Guid ProjectId { get; }
    public Guid? MapId { get; }
    public JObject Settings { get; }

    public static SettingsDocument ForProject(Guid projectId, JObject settings) =>
        Create(SettingsDocumentFormat.Project, projectId, null, settings);

    public static SettingsDocument ForMap(Guid projectId, Guid mapId, JObject settings) =>
        Create(SettingsDocumentFormat.Map, projectId, mapId, settings);

    public byte[] ToUtf8Bytes()
    {
        Validate();
        var root = new JObject
        {
            ["format"] = Format,
            ["schemaVersion"] = SchemaVersion,
            ["projectId"] = ProjectId.ToString("D"),
        };
        if (MapId is { } mapId)
            root["mapId"] = mapId.ToString("D");
        root["settings"] = (JObject)Settings.DeepClone();

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetBytes(JsonConvert.SerializeObject(root, Formatting.None, SettingsDocumentFormat.SerializerSettings));
    }

    public static SettingsDocument Parse(
        ReadOnlySpan<byte> utf8,
        Guid expectedProjectId,
        Guid? expectedMapId = null)
    {
        if (utf8.IsEmpty)
            throw new JsonSerializationException("Settings document is empty.");
        if (utf8.Length > SettingsDocumentFormat.MaximumDocumentBytes)
            throw new JsonSerializationException("Settings document exceeds the V1 size limit.");

        // Strict UTF-8 decoding rejects malformed byte sequences. A BOM is not part of V1.
        var text = new UTF8Encoding(false, true).GetString(utf8);
        if (text.Length > 0 && text[0] == '\uFEFF')
            throw new JsonSerializationException("Settings document must be UTF-8 without a BOM.");

        using var stringReader = new System.IO.StringReader(text);
        using var jsonReader = new JsonTextReader(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
            Culture = CultureInfo.InvariantCulture,
        };

        var root = JObject.Load(jsonReader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Load,
            LineInfoHandling = LineInfoHandling.Load,
        });

        if (jsonReader.Read())
            throw new JsonSerializationException("Settings document contains trailing JSON content.");

        if (ContainsComment(root))
            throw new JsonSerializationException("Comments are not permitted in settings documents.");

        var expectedFormat = expectedMapId.HasValue ? SettingsDocumentFormat.Map : SettingsDocumentFormat.Project;
        var allowed = expectedMapId.HasValue
            ? new[] { "format", "schemaVersion", "projectId", "mapId", "settings" }
            : new[] { "format", "schemaVersion", "projectId", "settings" };

        foreach (var property in root.Properties())
        {
            if (Array.IndexOf(allowed, property.Name) < 0)
                throw new JsonSerializationException($"Unknown top-level settings property '{property.Name}'.");
        }

        if (root["format"]?.Type != JTokenType.String
            || !string.Equals((string?)root["format"], expectedFormat, StringComparison.Ordinal))
            throw new JsonSerializationException("Settings document format identifier does not match its owner.");

        if (root["schemaVersion"]?.Type != JTokenType.Integer
            || (int?)root["schemaVersion"] != SettingsDocumentFormat.InitialSchemaVersion)
            throw new JsonSerializationException("Unsupported settings document schema version.");

        var projectId = ParseCanonicalGuid(root["projectId"], "projectId");
        if (projectId != expectedProjectId)
            throw new JsonSerializationException("Settings document Project ID does not match its SQLite owner.");

        Guid? mapId = null;
        if (expectedMapId is { } requiredMapId)
        {
            mapId = ParseCanonicalGuid(root["mapId"], "mapId");
            if (mapId != requiredMapId)
                throw new JsonSerializationException("Settings document Map ID does not match its SQLite owner.");
        }

        if (root["settings"] is not JObject settings)
            throw new JsonSerializationException("The settings property must be a JSON object.");

        var result = new SettingsDocument(expectedFormat, SettingsDocumentFormat.InitialSchemaVersion,
            projectId, mapId, (JObject)settings.DeepClone());
        result.Validate();
        return result;
    }

    private static SettingsDocument Create(string format, Guid projectId, Guid? mapId, JObject settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = new SettingsDocument(format, SettingsDocumentFormat.InitialSchemaVersion,
            projectId, mapId, (JObject)settings.DeepClone());
        result.Validate();
        return result;
    }

    private void Validate()
    {
        if (ProjectId == Guid.Empty)
            throw new JsonSerializationException("Settings document Project ID cannot be empty.");
        if (SchemaVersion != SettingsDocumentFormat.InitialSchemaVersion)
            throw new JsonSerializationException("Unsupported settings document schema version.");
        if (string.Equals(Format, SettingsDocumentFormat.Map, StringComparison.Ordinal) != MapId.HasValue)
            throw new JsonSerializationException("Settings document format and owner identity do not agree.");
        if (MapId == Guid.Empty)
            throw new JsonSerializationException("Settings document Map ID cannot be empty.");
        if (Settings is null)
            throw new JsonSerializationException("Settings payload cannot be null.");
        ValidateSettingsToken(Settings);
    }

    private static void ValidateSettingsToken(JToken token)
    {
        if (token.Type is JTokenType.Comment or JTokenType.Raw or JTokenType.Undefined or JTokenType.Constructor)
            throw new JsonSerializationException($"Unsupported JSON token type '{token.Type}' in settings payload.");

        if (token.Type == JTokenType.Float)
        {
            var value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new JsonSerializationException("Non-finite numbers are not valid settings JSON.");
        }

        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
                ValidateSettingsToken(property.Value);
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
                ValidateSettingsToken(item);
        }
    }

    private static bool ContainsComment(JToken token)
    {
        if (token.Type == JTokenType.Comment)
            return true;
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
                if (ContainsComment(property.Value)) return true;
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
                if (ContainsComment(item)) return true;
        }
        return false;
    }

    private static Guid ParseCanonicalGuid(JToken? token, string propertyName)
    {
        if (token?.Type != JTokenType.String)
            throw new JsonSerializationException($"Settings document {propertyName} must be a canonical GUID string.");

        var value = (string?)token;
        if (!Guid.TryParseExact(value, "D", out var parsed)
            || !string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal)
            || parsed == Guid.Empty)
            throw new JsonSerializationException($"Settings document {propertyName} must be a non-empty lowercase canonical GUID.");

        return parsed;
    }
}
