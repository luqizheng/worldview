using System;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorldView.Application.Settings;

namespace WorldView.Application.Tests.Settings;

public sealed class SettingsDocumentTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MapId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ProjectDocument_RoundTripsWithExactFormatAndNumericValues()
    {
        var settings = new JObject
        {
            ["display"] = new JObject { ["scale"] = 1.25m },
            ["enabled"] = true,
        };
        var document = SettingsDocument.ForProject(ProjectId, settings);

        var bytes = document.ToUtf8Bytes();
        var parsed = SettingsDocument.Parse(bytes, ProjectId);

        Assert.Equal(SettingsDocumentFormat.Project, parsed.Format);
        Assert.Equal(1, parsed.SchemaVersion);
        Assert.Equal(ProjectId, parsed.ProjectId);
        Assert.Null(parsed.MapId);
        Assert.Equal(JTokenType.Float, parsed.Settings["display"]?["scale"]?.Type);
        Assert.Equal(1.25m, (decimal?)parsed.Settings["display"]?["scale"]);
        Assert.Equal(JTokenType.Boolean, parsed.Settings["enabled"]?.Type);
    }

    [Fact]
    public void MapDocument_RoundTripsWithBothOwnerIds()
    {
        var document = SettingsDocument.ForMap(ProjectId, MapId, new JObject());
        var parsed = SettingsDocument.Parse(document.ToUtf8Bytes(), ProjectId, MapId);

        Assert.Equal(SettingsDocumentFormat.Map, parsed.Format);
        Assert.Equal(ProjectId, parsed.ProjectId);
        Assert.Equal(MapId, parsed.MapId);
        Assert.Empty(parsed.Settings.Properties());
    }

    [Fact]
    public void Parse_RejectsDuplicatePropertiesAtNestedDepth()
    {
        const string json = """
            {"format":"worldview.project-settings","schemaVersion":1,
             "projectId":"11111111-1111-1111-1111-111111111111",
             "settings":{"theme":"light","theme":"dark"}}
            """;

        Assert.ThrowsAny<JsonException>(() =>
            SettingsDocument.Parse(Encoding.UTF8.GetBytes(json), ProjectId));
    }

    [Fact]
    public void Parse_RejectsUnknownTopLevelProperties()
    {
        const string json = """
            {"format":"worldview.project-settings","schemaVersion":1,
             "projectId":"11111111-1111-1111-1111-111111111111",
             "settings":{},"unexpected":true}
            """;

        Assert.Throws<JsonSerializationException>(() =>
            SettingsDocument.Parse(Encoding.UTF8.GetBytes(json), ProjectId));
    }

    [Fact]
    public void Parse_RejectsOwnerMismatchAndTrailingContent()
    {
        var wrongOwner = """
            {"format":"worldview.project-settings","schemaVersion":1,
             "projectId":"33333333-3333-3333-3333-333333333333","settings":{}}
            """;
        Assert.Throws<JsonSerializationException>(() =>
            SettingsDocument.Parse(Encoding.UTF8.GetBytes(wrongOwner), ProjectId));

        var trailing = Encoding.UTF8.GetBytes(
            "{\"format\":\"worldview.project-settings\",\"schemaVersion\":1,"
            + "\"projectId\":\"11111111-1111-1111-1111-111111111111\",\"settings\":{}} {}");
        Assert.ThrowsAny<JsonException>(() =>
            SettingsDocument.Parse(trailing, ProjectId));
    }

    [Fact]
    public void Parse_RejectsBomAndNonObjectSettings()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(
            Encoding.UTF8.GetBytes("{\"format\":\"worldview.project-settings\",\"schemaVersion\":1,"
                + "\"projectId\":\"11111111-1111-1111-1111-111111111111\",\"settings\":{}}")).ToArray();
        Assert.Throws<JsonSerializationException>(() => SettingsDocument.Parse(bom, ProjectId));

        const string arraySettings = """
            {"format":"worldview.project-settings","schemaVersion":1,
             "projectId":"11111111-1111-1111-1111-111111111111","settings":[]}
            """;
        Assert.Throws<JsonSerializationException>(() =>
            SettingsDocument.Parse(Encoding.UTF8.GetBytes(arraySettings), ProjectId));
    }

    [Fact]
    public void Paths_UseCanonicalHashLayoutAndRejectTraversal()
    {
        const string hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var relative = SettingsDocumentPaths.GetRelativePath(hash);

        Assert.Equal($"Settings/sha256/ab/cd/{hash}.json", relative);
        Assert.True(SettingsDocumentPaths.TryResolvePublishedPath(
            Path.GetTempPath(), relative, hash, out var absolute));
        Assert.EndsWith(hash + ".json", absolute, StringComparison.Ordinal);

        Assert.False(SettingsDocumentPaths.TryResolvePublishedPath(
            Path.GetTempPath(), "../outside.json", hash, out _));
        Assert.False(SettingsDocumentPaths.TryResolvePublishedPath(
            Path.GetTempPath(), relative.Replace('/', '\\'), hash, out _));
    }
}
