using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WorldView.Application.Packaging;

namespace WorldView.Application.Tests.Packaging;

public sealed class FieldPackageReaderTests
{
    [Fact]
    public void OpenAndReadTile_ValidatesMinimalCanonicalPackage()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var decoded = Convert.FromHexString("000000000000c03f000010c00000284200000000");
        using var package = new MemoryStream(BuildPackage(manifest, decoded));
        using var reader = FieldPackageReader.Open(package, leaveOpen: true);

        Assert.Equal(2, reader.GridWidth);
        Assert.Equal(2, reader.GridHeight);
        Assert.Equal(1, reader.TileCount);
        Assert.Equal(decoded, reader.ReadTile(0, 0));
        Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(1, 0));
    }

    [Fact]
    public void Open_RejectsInvalidMagicBeforeParsingManifest()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var decoded = Convert.FromHexString("000000000000c03f000010c00000284200000000");
        var bytes = BuildPackage(manifest, decoded);
        bytes[0] = 0;

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Magic, error.Code);
    }

    [Fact]
    public void ReadTile_RejectsPayloadHashMismatch()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var decoded = Convert.FromHexString("000000000000c03f000010c00000284200000000");
        var bytes = BuildPackage(manifest, decoded);
        bytes[^1] ^= 0x01;
        using var reader = FieldPackageReader.Open(new MemoryStream(bytes));

        var error = Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(0, 0));
        Assert.Equal(FieldPackageBinaryErrorCodes.TileHash, error.Code);
    }

    private static byte[] BuildPackage(byte[] manifest, byte[] tile)
    {
        const int headerLength = 72;
        const int directoryLength = 16 + 72;
        var directoryOffset = Align8(headerLength + manifest.Length);
        var payloadOffset = Align8(directoryOffset + directoryLength);
        var packageLength = checked(payloadOffset + tile.Length);
        var result = new byte[packageLength];

        Encoding.ASCII.GetBytes("WVFLDPK").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(24), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(32), (ulong)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(40), (ulong)directoryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(48), directoryLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(56), (ulong)payloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(64), (ulong)packageLength);
        manifest.CopyTo(result, headerLength);

        var directory = result.AsSpan(directoryOffset, directoryLength);
        BinaryPrimitives.WriteUInt16LittleEndian(directory, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.Slice(4), 72);
        BinaryPrimitives.WriteUInt64LittleEndian(directory.Slice(8), 1);
        var entry = directory.Slice(16, 72);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(10), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(16), (ulong)payloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(24), (ulong)tile.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(32), (ulong)tile.Length);
        SHA256.HashData(tile).CopyTo(entry.Slice(40));
        tile.CopyTo(result, payloadOffset);
        return result;
    }

    private static int Align8(int value) => checked((value + 7) & ~7);
    private static byte[] ReadResource(string suffix)
    {
        var assembly = typeof(FieldPackageReaderTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))!;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
