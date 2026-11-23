using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WorldView.Infrastructure.Assets;
using Xunit;

namespace WorldView.Infrastructure.Tests.Assets;

public sealed class FileAssetStoreTests
{
    [Fact]
    public async Task PutAsync_PublishesContentAddressedImmutableBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wv-file-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var store = new FileAssetStore(root);
            var bytes = Encoding.UTF8.GetBytes("asset payload");
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            var result = await store.PutAsync(new MemoryStream(bytes));

            Assert.True(result.Success, result.Diagnostic);
            Assert.Equal(expectedHash, result.ContentHash);
            Assert.True(store.Exists(expectedHash));
            await using var read = await store.OpenReadAsync(expectedHash);
            Assert.NotNull(read);
            using var copy = new MemoryStream();
            await read!.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PutAsync_ExistingHashPathWithTamperedBytes_IsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wv-file-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var store = new FileAssetStore(root);
            var bytes = Encoding.UTF8.GetBytes("original");
            var first = await store.PutAsync(new MemoryStream(bytes));
            Assert.True(first.Success, first.Diagnostic);

            await File.WriteAllTextAsync(Path.Combine(store.RootPath, first.ContentHash!), "tampered");
            var second = await store.PutAsync(new MemoryStream(bytes));

            Assert.False(second.Success);
            Assert.Contains("哈希名不符", second.Diagnostic);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenReadAsync_RejectsUnsafeHash()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wv-file-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var store = new FileAssetStore(root);
            await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync("../outside"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
