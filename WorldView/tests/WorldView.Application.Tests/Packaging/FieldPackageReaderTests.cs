using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using WorldView.Domain.Fields;
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

    [Theory]
    [InlineData("minimal-elevation-2x2.wvfld", 1, 2, "856b2ddabb55c66fec374320a17db26bec3148e27790b83adcbc090ba643bca1")]
    [InlineData("land-water-4x2-two-tiles.wvfld", 2, 4, "2587b03e0c68636b9f6056f80974a3a946d432a975a5410088b67d490e0c0d49")]
    [InlineData("edge-elevation-3x2.wvfld", 2, 3, "92f239252f0f94ca2e1958333b814520bd53424f2a518ba5bf0ee712018aa440")]
    public void OpenAndReadTiles_ValidatesCanonicalGoldenPackages(string packageName, int expectedTileCount, int expectedGridWidth, string expectedPackageSha256)
    {
        var packageBytes = ReadResource(packageName);
        using var package = new MemoryStream(packageBytes);
        using var reader = FieldPackageReader.Open(package, leaveOpen: true);
        var manifestName = packageName.Replace(".wvfld", ".manifest.json", StringComparison.Ordinal);

        Assert.Equal(ReadResource(manifestName), reader.ManifestUtf8.ToArray());
        FieldPackageIntegrityVerifier.Verify(package, expectedPackageSha256);
        Assert.Equal(expectedGridWidth, reader.GridWidth);
        Assert.Equal(expectedTileCount, reader.TileCount);
        for (uint y = 0; y < 1; y++)
            for (uint x = 0; x < expectedTileCount; x++)
                Assert.NotEmpty(reader.ReadTile(x, y));
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
    public void ReadTile_RejectsInvalidValidityMaskAfterHashVerification()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var decoded = Convert.FromHexString("000000000000c03f000010c00000284200000000");
        decoded[^1] = 2;
        using var package = new MemoryStream(BuildPackage(manifest, decoded));
        using var reader = FieldPackageReader.Open(package, leaveOpen: true);

        var error = Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(0, 0));
        Assert.Equal(FieldPackageBinaryErrorCodes.Mask, error.Code);
    }

    [Fact]
    public void ReadTile_AcceptsDistinctExplicitCoverageMask()
    {
        var manifest = WithExplicitCoverageMask();
        var decoded = Convert.FromHexString("000000000000c03f000010c0000028420000000000010100");
        using var package = new MemoryStream(BuildPackage(manifest, decoded));
        using var reader = FieldPackageReader.Open(package, leaveOpen: true);

        Assert.Equal(decoded, reader.ReadTile(0, 0));
    }

    [Fact]
    public void ReadTile_RejectsInvalidExplicitCoverageMaskValue()
    {
        var manifest = WithExplicitCoverageMask();
        var decoded = Convert.FromHexString("000000000000c03f000010c0000028420000000000010102");
        using var package = new MemoryStream(BuildPackage(manifest, decoded));
        using var reader = FieldPackageReader.Open(package, leaveOpen: true);

        var error = Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(0, 0));
        Assert.Equal(FieldPackageBinaryErrorCodes.Mask, error.Code);
    }

    [Fact]
    public void Open_RejectsCoverageMaskAliasingValidityMask()
    {
        var node = JsonNode.Parse(Encoding.UTF8.GetString(ReadResource("minimal-elevation-2x2.manifest.json")))!;
        node["coverage"] = new JsonObject
        {
            ["mask_plane_name"] = "validity_mask",
            ["mode"] = "explicit_mask",
        };
        var manifest = Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(node.ToJsonString()));
        var decoded = Convert.FromHexString("000000000000c03f000010c00000284200000000");

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(BuildPackage(manifest, decoded))));
        Assert.Equal(FieldPackageBinaryErrorCodes.Mask, error.Code);
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

    [Fact]
    public void Open_RejectsTruncatedHeader()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes[..71])));
        Assert.Equal(FieldPackageBinaryErrorCodes.Truncated, error.Code);
    }

    [Fact]
    public void Open_RejectsDeclaredPhysicalLengthMismatch()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(64), (ulong)bytes.Length + 1);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Length, error.Code);
    }

    [Fact]
    public void Open_RejectsNonzeroManifestAlignmentPadding()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        var manifestEnd = 72 + manifest.Length;
        var directoryOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)));
        Assert.True(directoryOffset > manifestEnd);
        bytes[manifestEnd] = 0x7f;

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Header, error.Code);
    }

    [Fact]
    public void Open_RejectsNonzeroDirectoryEntryFlags()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        var directoryOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(directoryOffset + 16 + 14), 1);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);
    }

    [Fact]
    public void Open_RejectsDirectoryEntryCountMismatch()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        var directoryOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)));
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(directoryOffset + 8), 2);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);
    }

    [Fact]
    public void Open_RejectsTilePayloadRangeOutsidePackage()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        var directoryOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)));
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(directoryOffset + 16 + 16), (ulong)bytes.Length + 8);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.TileRange, error.Code);
    }

    [Fact]
    public void Open_RejectsUnsupportedTileCodec()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        var bytes = BuildPackage(manifest, Convert.FromHexString("000000000000c03f000010c00000284200000000"));
        var directoryOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(directoryOffset + 16 + 12), 1);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(bytes)));
        Assert.Equal(FieldPackageBinaryErrorCodes.CodecUnsupported, error.Code);
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

    private static byte[] WithExplicitCoverageMask()
    {
        var node = JsonNode.Parse(Encoding.UTF8.GetString(ReadResource("minimal-elevation-2x2.manifest.json")))!;
        node["value_encoding"]!["planes"]!.AsArray().Add(new JsonObject
        {
            ["bytes_per_cell"] = 1,
            ["name"] = "coverage_mask",
            ["sample_format"] = "uint8",
        });
        node["coverage"] = new JsonObject
        {
            ["mask_plane_name"] = "coverage_mask",
            ["mode"] = "explicit_mask",
        };
        return Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(node.ToJsonString()));
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
