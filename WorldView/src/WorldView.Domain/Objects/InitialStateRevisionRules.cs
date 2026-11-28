using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace WorldView.Domain.Objects;

/// <summary>
/// Deterministic rules shared by the baseline revision service and persistence adapter.
/// This class deliberately contains no storage or UI cursor state.
/// </summary>
public static class InitialStateRevisionRules
{
    /// <summary>唯一认可的编辑操作种类（FOUNDATION-01B §4：一编辑一 revision 一操作描述）。</summary>
    public const string SnapshotReplaceOperationKind = "snapshot_replace";

    /// <summary>
    /// Build the canonical audit diff for a complete child snapshot. The snapshot itself
    /// remains authoritative; this payload is descriptive metadata, never replay input.
    /// </summary>
    public static string BuildOperationPayload(
        string parentFingerprint,
        string childFingerprint,
        IReadOnlyList<InitialObjectRecord> parentMembers,
        IReadOnlyList<InitialObjectWrite> childMembers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(childFingerprint);
        ArgumentNullException.ThrowIfNull(parentMembers);
        ArgumentNullException.ThrowIfNull(childMembers);

        EnsureUniqueIdentities(parentMembers.Select(x => x.ObjectIdentityId));
        EnsureUniqueIdentities(childMembers.Select(x => x.ObjectIdentityId));

        var parent = parentMembers.ToDictionary(x => x.ObjectIdentityId, SnapshotMember.From);
        var child = childMembers.ToDictionary(x => x.ObjectIdentityId, SnapshotMember.From);

        var added = child.Keys.Except(parent.Keys).OrderBy(IdKey, StringComparer.Ordinal).Select(IdKey).ToArray();
        var removed = parent.Keys.Except(child.Keys).OrderBy(IdKey, StringComparer.Ordinal).Select(IdKey).ToArray();
        var updated = parent.Keys.Intersect(child.Keys)
            .Where(id => parent[id] != child[id])
            .OrderBy(IdKey, StringComparer.Ordinal)
            .Select(IdKey)
            .ToArray();

        var payload = new OperationPayload
        {
            Added = added,
            Removed = removed,
            Updated = updated,
            ParentFingerprint = parentFingerprint,
            ChildFingerprint = childFingerprint,
        };

        return CanonicalJson.Canonicalize(JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    /// <summary>Validate that a candidate redo target is a direct child of the active revision.</summary>
    public static void ValidateDirectChild(
        Guid activeRevisionId,
        Guid childRevisionId,
        Guid? childParentRevisionId)
    {
        if (childRevisionId == Guid.Empty
            || childParentRevisionId != activeRevisionId)
        {
            throw new Persistence.CoreDomainConflictException(
                "Redo target must be a direct child of the active baseline revision.");
        }
    }

    /// <summary>Resolve an implicit Redo only when the active revision has exactly one child.</summary>
    public static Guid ResolveRedoTarget(
        IReadOnlyList<MapInitialStateRevisionRecord> directChildren,
        Guid? selectedChildRevisionId)
    {
        ArgumentNullException.ThrowIfNull(directChildren);

        if (selectedChildRevisionId is { } selected)
        {
            var match = directChildren.SingleOrDefault(x => x.RevisionId == selected);
            if (match is null)
            {
                throw new Persistence.CoreDomainConflictException(
                    "Selected redo revision is not a direct child of the active revision.");
            }

            return selected;
        }

        return directChildren.Count switch
        {
            1 => directChildren[0].RevisionId,
            0 => throw new Persistence.CoreDomainConflictException(
                "No redo revision exists for the active baseline revision."),
            _ => throw new Persistence.CoreDomainConflictException(
                "Multiple redo branches exist; an explicit child revision must be selected."),
        };
    }

    private static void EnsureUniqueIdentities(IEnumerable<Guid> identities)
    {
        var seen = new HashSet<Guid>();
        foreach (var identity in identities)
        {
            if (identity == Guid.Empty || !seen.Add(identity))
            {
                throw new Persistence.CoreDomainConflictException(
                    "A baseline revision snapshot must contain unique, non-empty Object identities.");
            }
        }
    }

    private static string IdKey(Guid id) => id.ToString("D");

    private sealed record SnapshotMember(
        string MembershipPayloadJson,
        string? PlacementPayloadJson,
        long? PlacementSchemaVersion)
    {
        public static SnapshotMember From(InitialObjectRecord member) => new(
            CanonicalJson.Canonicalize(member.MembershipPayloadJson),
            member.PlacementPayloadJson is null ? null : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
            member.PlacementSchemaVersion);

        public static SnapshotMember From(InitialObjectWrite member) => new(
            CanonicalJson.Canonicalize(member.MembershipPayloadJson),
            member.PlacementPayloadJson is null ? null : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
            member.PlacementSchemaVersion);
    }

    private sealed record OperationPayload
    {
        public required string[] Added { get; init; }
        public required string[] Removed { get; init; }
        public required string[] Updated { get; init; }
        public required string ParentFingerprint { get; init; }
        public required string ChildFingerprint { get; init; }
    }
}
