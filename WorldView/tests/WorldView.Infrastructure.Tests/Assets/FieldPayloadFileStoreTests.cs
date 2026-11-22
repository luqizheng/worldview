using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WorldView.Infrastructure.Assets;
using Xunit;

namespace WorldView.Infrastructure.Tests.Assets;

public sealed class FieldPayloadFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wv-field-payload-{Guid.NewGuid():N}");

    [Fact]
    public async Task PutAsync_StreamsNonSeekableBytesAndPublishesVerifiedContentAddressedFile()
    {
        Directory.CreateDirectory(_root);
        var store = new FieldPayloadFileStore(_root);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 300_000));
        await using var input = new NonSeekableReadStream(bytes);

        var result = await store.PutAsync(input);

        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expectedHash, result.ContentHash);
        Assert.Equal(bytes.LongLength, result.ByteLength);
        Assert.Equal($"FieldPayloads/sha256/{expectedHash}", result.RelativePath);
        Assert.True(store.Exists(expectedHash));

        await using var opened = await store.OpenReadAsync(expectedHash);
        Assert.NotNull(opened);
        using var copy = new MemoryStream();
        await opened!.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task CleanupStagingFiles_RemovesInterruptedWritesButKeepsPublishedPayloads()
    {
        Directory.CreateDirectory(_root);
        var store = new FieldPayloadFileStore(_root);
        var published = await store.PutAsync(new MemoryStream(Encoding.UTF8.GetBytes("published")));
        var stalePath = Path.Combine(store.RootPath, ".staging-crash.tmp");
        await File.WriteAllTextAsync(stalePath, "partial");

        var removed = store.CleanupStagingFiles();

        Assert.Equal(1, removed);
        Assert.False(File.Exists(stalePath));
        Assert.True(store.Exists(published.ContentHash));
    }

    [Fact]
    public async Task PutAsync_IsIdempotentForIdenticalBytes()
    {
        Directory.CreateDirectory(_root);
        var store = new FieldPayloadFileStore(_root);
        var bytes = Encoding.UTF8.GetBytes("immutable-field-payload");

        var first = await store.PutAsync(new MemoryStream(bytes));
        var second = await store.PutAsync(new MemoryStream(bytes));

        Assert.Equal(first, second);
        Assert.Empty(Directory.GetFiles(store.RootPath, ".staging-*.tmp"));
    }

    [Fact]
    public async Task PutAsync_RejectsEmptyPayloadAndLeavesNoStagingFile()
    {
        Directory.CreateDirectory(_root);
        var store = new FieldPayloadFileStore(_root);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.PutAsync(new MemoryStream(Array.Empty<byte>())));

        Assert.Empty(Directory.GetFiles(store.RootPath, ".staging-*.tmp"));
    }

    [Fact]
    public async Task OpenReadAsync_RejectsUnsafeHash()
    {
        Directory.CreateDirectory(_root);
        var store = new FieldPayloadFileStore(_root);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.OpenReadAsync("../outside"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
