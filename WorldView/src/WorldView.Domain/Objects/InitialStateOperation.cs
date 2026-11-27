using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace WorldView.Domain.Objects;

/// <summary>
/// Deterministic summary of the difference between a parent full snapshot and its child.
/// The immutable child snapshot remains authoritative; this payload explains the edit.
/// </summary>
public static class InitialStateOperation
{
    public const string SnapshotReplaceKind = "snapshot_replace";

    public static string ComputePayload(
        IReadOnlyList<InitialObjectRecord> parentMembers,
        IReadOnlyList<InitialObjectWrite> childMembers,
        string parentFingerprint,
        string childFingerprint)
    {
        ArgumentNullException.ThrowIfNull(parentMembers);
        ArgumentNullException.ThrowIfNull(childMembers);

        var parent = parentMembers.ToDictionary(x => x.ObjectIdentityId);
        var child = childMembers.ToDictionary(x => x.ObjectIdentityId);

        var added = child.Keys.Except(parent.Keys).Select(Format).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var removed = parent.Keys.Except(child.Keys).Select(Format).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var updated = parent.Keys.Intersect(child.Keys)
            .Where(id => !SameContent(parent[id], child[id]))
            .Select(Format)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var payload = JsonSerializer.Serialize(new
        {
            parent_fingerprint = parentFingerprint,
            child_fingerprint = childFingerprint,
            added,
            removed,
            updated,
        });
        return CanonicalJson.Canonicalize(payload);
    }

    private static bool SameContent(InitialObjectRecord before, InitialObjectWrite after) =>
        string.Equals(CanonicalJson.Canonicalize(before.MembershipPayloadJson),
            CanonicalJson.Canonicalize(after.MembershipPayloadJson), StringComparison.Ordinal)
        && string.Equals(
            before.PlacementPayloadJson is null ? null : CanonicalJson.Canonicalize(before.PlacementPayloadJson),
            after.PlacementPayloadJson is null ? null : CanonicalJson.Canonicalize(after.PlacementPayloadJson),
            StringComparison.Ordinal)
        && before.PlacementSchemaVersion == after.PlacementSchemaVersion;

    private static string Format(Guid id) => id.ToString("D");
}
