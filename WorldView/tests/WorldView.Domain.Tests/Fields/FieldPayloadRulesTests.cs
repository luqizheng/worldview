using System;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;
using Xunit;

namespace WorldView.Domain.Tests.Fields;

public sealed class FieldPayloadRulesTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static RegisterFieldPayloadPlan Plan(FieldPayloadStorageKind kind) => new()
    {
        FieldPayloadId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        FieldDatasetId = Guid.NewGuid(),
        FieldRevisionId = Guid.NewGuid(),
        StorageKind = kind,
        ByteLength = 128,
        ContentHash = Hash,
        RelativePath = kind == FieldPayloadStorageKind.ManagedFile ? $"FieldPayloads/sha256/{Hash}" : null,
        CreatedAtUtcMs = 1,
    };

    [Fact]
    public void Create_AcceptsRawSqliteBlobMetadataWithoutPath()
    {
        var record = FieldPayloadRules.Create(Plan(FieldPayloadStorageKind.SqliteBlob));

        Assert.Equal(FieldPayloadStorageKind.SqliteBlob, record.StorageKind);
        Assert.Null(record.RelativePath);
        Assert.Equal(Hash, record.ContentHash);
        Assert.Equal(128, record.ByteLength);
    }

    [Fact]
    public void Create_AcceptsCanonicalManagedFilePath()
    {
        var record = FieldPayloadRules.Create(Plan(FieldPayloadStorageKind.ManagedFile));

        Assert.Equal($"FieldPayloads/sha256/{Hash}", record.RelativePath);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute/path")]
    [InlineData("C:\\outside")]
    [InlineData("FieldPayloads/sha256/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void Create_RejectsNonCanonicalManagedPath(string path)
    {
        var plan = Plan(FieldPayloadStorageKind.ManagedFile) with { RelativePath = path };

        Assert.Throws<CoreDomainDataException>(() => FieldPayloadRules.Create(plan));
    }

    [Fact]
    public void Create_RejectsPathOnSqliteBlob()
    {
        var plan = Plan(FieldPayloadStorageKind.SqliteBlob) with { RelativePath = $"FieldPayloads/sha256/{Hash}" };

        Assert.Throws<CoreDomainDataException>(() => FieldPayloadRules.Create(plan));
    }

    [Fact]
    public void Create_RejectsEmptyPayload()
    {
        var plan = Plan(FieldPayloadStorageKind.SqliteBlob) with { ByteLength = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => FieldPayloadRules.Create(plan));
    }

    [Fact]
    public void Create_RejectsUnknownStorageKind()
    {
        var plan = Plan(FieldPayloadStorageKind.SqliteBlob) with { StorageKind = (FieldPayloadStorageKind)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => FieldPayloadRules.Create(plan));
    }
}
