using System;
using System.Collections.Generic;
using System.Text.Json;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using Xunit;

namespace WorldView.Domain.Tests.Objects;

public sealed class InitialStateRevisionRulesTests
{
    private static InitialObjectRecord Existing(Guid id, string membership, string? placement = null, long? placementVersion = null) => new()
    {
        ProjectId = Guid.NewGuid(),
        MapId = Guid.NewGuid(),
        InitialStateId = Guid.NewGuid(),
        ObjectIdentityId = id,
        MembershipPayloadJson = membership,
        PlacementPayloadJson = placement,
        PlacementSchemaVersion = placementVersion,
        CreatedAtUtcMs = 1,
    };

    private static InitialObjectWrite Desired(Guid id, string membership, string? placement = null, long? placementVersion = null) => new()
    {
        ObjectIdentityId = id,
        MembershipPayloadJson = membership,
        PlacementPayloadJson = placement,
        PlacementSchemaVersion = placementVersion,
    };

    [Fact]
    public void BuildOperationPayload_ProducesSortedDisjointDiffSets()
    {
        var retained = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var changed = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var removed = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var added = Guid.Parse("00000000-0000-0000-0000-000000000004");

        var payload = InitialStateRevisionRules.BuildOperationPayload(
            "parent-hash",
            "child-hash",
            new[]
            {
                Existing(retained, """{"a":1}"""),
                Existing(changed, """{"a":1}"""),
                Existing(removed, """{"a":1}"""),
            },
            new[]
            {
                Desired(added, """{"a":1}"""),
                Desired(changed, """{ "a" : 2 }"""),
                Desired(retained, """{ "a" : 1 }"""),
            });

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        Assert.Equal(new[] { added.ToString("D") }, Read(root, "added"));
        Assert.Equal(new[] { removed.ToString("D") }, Read(root, "removed"));
        Assert.Equal(new[] { changed.ToString("D") }, Read(root, "updated"));

        Assert.Equal("parent-hash", root.GetProperty("parentFingerprint").GetString());
        Assert.Equal("child-hash", root.GetProperty("childFingerprint").GetString());
    }

    [Fact]
    public void BuildOperationPayload_EmptyDiff_IsDeterministic()
    {
        var id = Guid.NewGuid();
        var payload1 = InitialStateRevisionRules.BuildOperationPayload(
            "same", "same",
            new[] { Existing(id, """{"a":1}""") },
            new[] { Desired(id, """{ "a": 1 }""") });
        var payload2 = InitialStateRevisionRules.BuildOperationPayload(
            "same", "same",
            new[] { Existing(id, """{ "a": 1 }""") },
            new[] { Desired(id, """{"a":1}""") });

        Assert.Equal(payload1, payload2);
        using var json = JsonDocument.Parse(payload1);
        Assert.Empty(Read(json.RootElement, "added"));
        Assert.Empty(Read(json.RootElement, "removed"));
        Assert.Empty(Read(json.RootElement, "updated"));
    }

    [Fact]
    public void ValidateDirectChild_RejectsNonChildAndAcceptsDirectChild()
    {
        var active = Guid.NewGuid();
        var child = Guid.NewGuid();

        InitialStateRevisionRules.ValidateDirectChild(active, child, active);

        Assert.Throws<CoreDomainConflictException>(
            () => InitialStateRevisionRules.ValidateDirectChild(active, child, Guid.NewGuid()));
        Assert.Throws<CoreDomainConflictException>(
            () => InitialStateRevisionRules.ValidateDirectChild(active, child, null));
    }

    [Fact]
    public void ResolveRedoTarget_ImplicitOnlyWhenExactlyOneChild()
    {
        var one = Revision(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(one.RevisionId, InitialStateRevisionRules.ResolveRedoTarget(new[] { one }, null));

        Assert.Throws<CoreDomainConflictException>(
            () => InitialStateRevisionRules.ResolveRedoTarget(Array.Empty<MapInitialStateRevisionRecord>(), null));

        var two = Revision(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<CoreDomainConflictException>(
            () => InitialStateRevisionRules.ResolveRedoTarget(new[] { one, two }, null));

        Assert.Equal(two.RevisionId,
            InitialStateRevisionRules.ResolveRedoTarget(new[] { one, two }, two.RevisionId));

        Assert.Throws<CoreDomainConflictException>(
            () => InitialStateRevisionRules.ResolveRedoTarget(new[] { one, two }, Guid.NewGuid()));
    }

    private static MapInitialStateRevisionRecord Revision(Guid id, Guid parent) => new()
    {
        RevisionId = id,
        ProjectId = Guid.NewGuid(),
        MapId = Guid.NewGuid(),
        InitialStateId = Guid.NewGuid(),
        RevisionNumber = 1,
        ParentRevisionId = parent,
        BaselineSchemaVersion = 1,
        ContentFingerprint = "fingerprint",
        CreatedAtUtcMs = 1,
    };

    private static string[] Read(JsonElement root, string name)
    {
        var values = new List<string>();
        foreach (var item in root.GetProperty(name).EnumerateArray())
        {
            values.Add(item.GetString()!);
        }

        return values.ToArray();
    }
}
