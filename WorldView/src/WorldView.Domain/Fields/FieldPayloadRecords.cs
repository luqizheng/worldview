using System;

namespace WorldView.Domain.Fields;

/// <summary>
/// Physical storage authority for one immutable Field Revision payload. Payload bytes are
/// deliberately not exposed on this metadata record; readers obtain them through the
/// Application storage port. A revision owns exactly one payload record.
/// </summary>
public sealed record FieldPayloadRecord
{
    public required Guid FieldPayloadId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid FieldDatasetId { get; init; }
    public required Guid FieldRevisionId { get; init; }
    public required FieldPayloadStorageKind StorageKind { get; init; }
    public required string PayloadFormat { get; init; }
    public required string FormatVersion { get; init; }
    public required string HashAlgorithm { get; init; }
    public required FieldPayloadIntegrityState IntegrityState { get; init; }
    public required long ByteLength { get; init; }
    /// <summary>SHA-256 of the exact unwrapped payload bytes, lowercase hexadecimal.</summary>
    public required string ContentHash { get; init; }
    /// <summary>
    /// Canonical package-relative path for ManagedFile; null for SqliteBlob.
    /// Never an absolute path and never resolved by Domain.
    /// </summary>
    public string? RelativePath { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>Physical placement of immutable Field payload bytes.</summary>
public enum FieldPayloadStorageKind
{
    /// <summary>Exact raw bytes stored in a SQLite BLOB column.</summary>
    SqliteBlob = 1,
    /// <summary>Exact raw bytes stored in the package-managed payload directory.</summary>
    ManagedFile = 2,
}

/// <summary>Integrity state of a registered immutable Field Payload.</summary>
public enum FieldPayloadIntegrityState
{
    Verified = 1,
    Corrupt = 2,
    Missing = 3,
}
