using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WorldView.Application.Settings;
using WorldView.Infrastructure.Settings;

namespace WorldView.Infrastructure.Tests.Settings;

public sealed class SettingsDocumentFileStoreTests
{
    [Fact]
    public async Task PutAndOpenRead_PublishesExactBytesAtContentAddressedPath()
    {
        var root = CreatePackageRoot();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("{\"format\":\"worldview.project-settings\",\"settings\":{}}");
            var store = new SettingsDocumentFileStore(root);
            await using var input = new MemoryStream(bytes);
            var result = await store.PutAsync(input);

            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(expectedHash, result.ContentHash);
            Assert.Equal(bytes.LongLength, result.ByteLength);
            Assert.Equal(SettingsDocumentPaths.GetRelativePath(expectedHash), result.RelativePath);
            Assert.Single(store.ListPublishedContentHashes());

            await using var read = await store.OpenReadAsync(expectedHash);
            Assert.NotNull(read);
            using var output = new MemoryStream();
            await read!.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PutAsync_ReusesVerifiedExistingContentAddressedFile()
    {
        var root = CreatePackageRoot();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("{\"settings\":{\"a\":1}}");
            var store = new SettingsDocumentFileStore(root);
            await using var first = new MemoryStream(bytes);
            await using var second = new MemoryStream(bytes);

            var published = await store.PutAsync(first);
            var reused = await store.PutAsync(second);

            Assert.Equal(published.ContentHash, reused.ContentHash);
            Assert.Equal(published.RelativePath, reused.RelativePath);
            Assert.Single(store.ListPublishedContentHashes());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PutAsync_FailsClosedWhenExistingHashPathContainsCorruptBytes()
    {
        var root = CreatePackageRoot();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("{\"settings\":{\"a\":1}}");
            var store = new SettingsDocumentFileStore(root);
            await using var input = new MemoryStream(bytes);
            var published = await store.PutAsync(input);
            var path = Path.Combine(root, published.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            await File.WriteAllTextAsync(path, "corrupt");

            await using var retry = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.PutAsync(retry));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CleanupStagingFiles_RemovesOnlyStagingFiles()
    {
        var root = CreatePackageRoot();
        try
        {
            var store = new SettingsDocumentFileStore(root);
            var staging = Path.Combine(root, "Settings", ".staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, "abandoned.tmp"), "partial");
            File.WriteAllText(Path.Combine(staging, "notes.txt"), "keep");

            Assert.Equal(1, store.CleanupStagingFiles());
            Assert.False(File.Exists(Path.Combine(staging, "abandoned.tmp")));
            Assert.True(File.Exists(Path.Combine(staging, "notes.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreatePackageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "worldview-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
