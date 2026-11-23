using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;
using Xunit;

namespace WorldView.Application.Tests.Packaging;

/// <summary>
/// Field Package conformance, fuzz and resource-limit suite (P2-09-33 / P2-09-36 mapping).
/// Test names carry the stable vector IDs (RDR/TIL/WRT/INT); vectors already exercised by
/// FieldPackageReaderTests, FieldPackageWriterTests, FieldPackageManifestValidatorTests and
/// FieldPackageIntegrityVerifierTests are listed in
/// docs/development/local-progress/field-package/004-conformance-tests.md and not duplicated.
/// Fuzz loops are deterministic (fixed seed), bounded (small inputs, fixed iteration counts)
/// and assert that only typed FieldPackageValidationException errors ever escape.
/// </summary>
public sealed class FieldPackageConformanceTests
{
    private const ulong FuzzSeed = 20261123UL;

    // ---- Shared fixture and builder helpers ----

    private static byte[] GoldenMinimalManifest() => ReadResource("minimal-elevation-2x2.manifest.json");
    private static byte[] GoldenMinimalPackage() => ReadResource("minimal-elevation-2x2.wvfld");
    private static byte[] GoldenEdgeManifest() => ReadResource("edge-elevation-3x2.manifest.json");
    private static byte[] GoldenEdgePackage() => ReadResource("edge-elevation-3x2.wvfld");

    /// <summary>The canonical decoded tile bytes recorded by the golden fixture inventory.</summary>
    private static byte[] GoldenMinimalTile()
    {
        using var inventory = System.Text.Json.JsonDocument.Parse(ReadResource("golden-fixtures.json"));
        var fixture = inventory.RootElement.GetProperty("fixtures").EnumerateArray()
            .Single(item => string.Equals(
                item.GetProperty("package_file").GetString(), "minimal-elevation-2x2.wvfld", StringComparison.Ordinal));
        return Convert.FromHexString(
            fixture.GetProperty("tiles").EnumerateArray().First().GetProperty("decoded_hex").GetString()!);
    }

    /// <summary>20 bytes: 4 float32 samples + 4 valid (0) mask bytes.</summary>
    private static byte[] ElevationTile(float sample0 = 1.5f) =>
        BuildTile(stackalloc float[] { sample0, 2.5f, 3.5f, 4.5f });

    private static byte[] BuildTile(ReadOnlySpan<float> samples)
    {
        var tile = new byte[samples.Length * 5];
        for (var i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(tile.AsSpan(i * 4), samples[i]);
        return tile; // mask bytes remain 0 = valid
    }

    /// <summary>Elevation manifest with grid.width mutated and re-canonicalized.</summary>
    private static byte[] ElevationManifest(int gridWidth, int gridHeight = 2)
    {
        var mutated = Encoding.UTF8.GetString(GoldenMinimalManifest())
            .Replace("\"width\":2", $"\"width\":{gridWidth}", StringComparison.Ordinal)
            .Replace("\"height\":2", $"\"height\":{gridHeight}", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(mutated));
    }

    /// <summary>
    /// Canonical sequential codec-0 package for a 2-high elevation grid. Tile dimensions are
    /// 2x2; the caller supplies exactly ceil(width/2) decoded tiles in row-major order.
    /// </summary>
    private static byte[] BuildPackage(byte[] manifest, IReadOnlyList<byte[]> tiles, int tileWidth = 2, int tileHeight = 2)
    {
        var width = ReadManifestGridDimension(manifest, "width");
        var height = ReadManifestGridDimension(manifest, "height");
        var columns = (width + tileWidth - 1) / tileWidth;
        var rows = (height + tileHeight - 1) / tileHeight;
        Assert.Equal(columns * rows, tiles.Count);

        const int headerLength = 72;
        var directoryLength = 16 + 72 * tiles.Count; // exact length; the reader enforces it
        var directoryOffset = Align8(headerLength + manifest.Length);
        var payloadOffset = Align8(directoryOffset + directoryLength);

        var payloadLengths = tiles.Select(t => (long)t.Length).ToArray();
        var payloadOffsets = new long[tiles.Count];
        long cursor = payloadOffset;
        for (var i = 0; i < tiles.Count; i++)
        {
            cursor = (cursor + 7) & ~7L;
            payloadOffsets[i] = cursor;
            cursor += payloadLengths[i];
        }
        var packageLength = checked((int)cursor);
        var result = new byte[packageLength];

        Encoding.ASCII.GetBytes("WVFLDPK").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(24), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(32), (ulong)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(40), (ulong)directoryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(48), (ulong)directoryLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(56), (ulong)payloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(64), (ulong)packageLength);
        manifest.CopyTo(result, headerLength);

        var directory = result.AsSpan(directoryOffset, directoryLength);
        BinaryPrimitives.WriteUInt16LittleEndian(directory, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.Slice(4), 72);
        BinaryPrimitives.WriteUInt64LittleEndian(directory.Slice(8), (ulong)tiles.Count);
        for (var i = 0; i < tiles.Count; i++)
        {
            var tileY = i / columns;
            var tileX = i % columns;
            var validWidth = Math.Min(tileWidth, width - tileX * tileWidth);
            var validHeight = Math.Min(tileHeight, height - tileY * tileHeight);
            var entry = directory.Slice(16 + i * 72, 72);
            BinaryPrimitives.WriteUInt32LittleEndian(entry, (uint)tileX);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(4), (uint)tileY);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(8), (ushort)validWidth);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(10), (ushort)validHeight);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(12), 0);
            BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(16), (ulong)payloadOffsets[i]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(24), (ulong)payloadLengths[i]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(32), (ulong)payloadLengths[i]);
            SHA256.HashData(tiles[i]).CopyTo(entry.Slice(40));
            tiles[i].CopyTo(result, checked((int)payloadOffsets[i]));
        }
        return result;
    }

    private static int ReadManifestGridDimension(byte[] manifest, string name)
    {
        using var document = System.Text.Json.JsonDocument.Parse(manifest);
        return document.RootElement.GetProperty("grid").GetProperty(name).GetInt32();
    }

    private static int DirectoryOffset(byte[] package) =>
        checked((int)BinaryPrimitives.ReadUInt64LittleEndian(package.AsSpan(40)));

    private static Span<byte> DirectoryEntry(byte[] package, int index) =>
        package.AsSpan(DirectoryOffset(package) + 16 + index * 72, 72);

    private static void OpenThrows(byte[] package, string expectedCode, FieldPackageReaderOptions? options = null)
    {
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(package), options));
        Assert.Equal(expectedCode, error.Code);
    }

    private static int Align8(int value) => checked((value + 7) & ~7);

    private static byte[] ReadResource(string suffix)
    {
        var assembly = typeof(FieldPackageConformanceTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))!;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    // ---- RDR: header / manifest / directory / payload conformance ----

    [Theory]
    [InlineData("version_minor", 10)]
    [InlineData("header_length", 73)]
    [InlineData("flags", 1)]
    [InlineData("reserved", 2)]
    public void RDR_003_RejectsInvalidHeaderFields(string field, int value)
    {
        var package = GoldenMinimalPackage();
        switch (field)
        {
            case "version_minor": BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(10), (ushort)value); break;
            case "header_length": BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), (uint)value); break;
            case "flags": BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), (uint)value); break;
            case "reserved": BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)value); break;
        }

        OpenThrows(package, field == "version_minor"
            ? FieldPackageBinaryErrorCodes.Version
            : FieldPackageBinaryErrorCodes.Header);
    }

    [Fact]
    public void RDR_005_RejectsManifestOffsetAndSizePolicy()
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), 80); // offset must be exactly 72
        OpenThrows(package, FieldPackageBinaryErrorCodes.Limit);

        package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(32), 0); // zero manifest length
        OpenThrows(package, FieldPackageBinaryErrorCodes.Limit);

        package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(32),
            (ulong)FieldPackageManifestValidator.MaximumManifestBytes + 1); // over-policy manifest
        OpenThrows(package, FieldPackageBinaryErrorCodes.Limit);
    }

    [Fact]
    public void RDR_007_RejectsDirectoryArithmeticOverflow()
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(48), ulong.MaxValue - 16); // length overflows
        OpenThrows(package, FieldPackageBinaryErrorCodes.Overflow);

        package = GoldenMinimalPackage();
        var directoryOffset = DirectoryOffset(package);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(directoryOffset + 8), ulong.MaxValue); // count overflow
        OpenThrows(package, FieldPackageBinaryErrorCodes.Overflow);
    }

    [Theory]
    [InlineData(0)] // directory version != 1
    [InlineData(2)]
    public void RDR_008_RejectsDirectoryVersionAndEntrySize(ushort directoryVersion)
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(DirectoryOffset(package)), directoryVersion);
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);

        package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(DirectoryOffset(package) + 4), 64);
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);
    }

    [Fact]
    public void RDR_010_RejectsTileCountAboveConfiguredPolicy()
    {
        // Edge fixture expects two directory entries; policy admits one.
        OpenThrows(GoldenEdgePackage(), FieldPackageBinaryErrorCodes.Limit,
            new FieldPackageReaderOptions { MaximumDirectoryEntries = 1 });
    }

    [Fact]
    public void RDR_011_RejectsDuplicateMissingAndOutOfGridCoordinates()
    {
        var manifest = ElevationManifest(gridWidth: 4); // two tiles: (0,0) and (1,0)
        var package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });

        // Duplicate: second entry rewritten to (0,0) breaks canonical order.
        BinaryPrimitives.WriteUInt32LittleEndian(DirectoryEntry(package, 1), 0);
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);

        // Out-of-grid: second entry x=2 while columns=2.
        package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });
        BinaryPrimitives.WriteUInt32LittleEndian(DirectoryEntry(package, 1), 2);
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);

        // Out-of-order: entries swapped.
        package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });
        var entry0 = DirectoryEntry(package, 0).ToArray();
        var entry1 = DirectoryEntry(package, 1).ToArray();
        entry1.AsSpan().CopyTo(DirectoryEntry(package, 0));
        entry0.AsSpan().CopyTo(DirectoryEntry(package, 1));
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);
    }

    [Fact]
    public void RDR_012_RejectsEdgeTileDimensionMismatch()
    {
        // Edge grid 3x2: tile (1,0) is valid_width 1; declaring 2 violates the native grid.
        var package = GoldenEdgePackage();
        var entry = DirectoryEntry(package, 1);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(8)));
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(8), 2);
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);
    }

    [Fact]
    public void RDR_013_RejectsDecodedLengthInconsistentWithManifestPlanes()
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(DirectoryEntry(package, 0).Slice(32), 19); // planes require 20
        OpenThrows(package, FieldPackageBinaryErrorCodes.DecodedLength);
    }

    [Fact]
    public void RDR_014_RejectsInvalidCodecRegistry()
    {
        // Codec 0 renamed: registry must identify codec 0 as the parameter-free none codec v1.
        var manifest = Encoding.UTF8.GetString(GoldenMinimalManifest())
            .Replace("\"codec_name\":\"none\"", "\"codec_name\":\"zstd\"", StringComparison.Ordinal);
        var package = BuildPackage(
            Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(manifest)),
            new[] { ElevationTile() });
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);

        // Codec 0 with parameters.
        manifest = Encoding.UTF8.GetString(GoldenMinimalManifest())
            .Replace("\"parameters\":{}", "\"parameters\":{\"level\":3}", StringComparison.Ordinal);
        package = BuildPackage(
            Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(manifest)),
            new[] { ElevationTile() });
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);
    }

    [Fact]
    public void RDR_015_RejectsTileCodecOutsideRegistry()
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt16LittleEndian(DirectoryEntry(package, 0).Slice(12), 9);
        OpenThrows(package, FieldPackageBinaryErrorCodes.CodecUnsupported);
    }

    [Fact]
    public void RDR_016_RejectsMisalignedAndOverlappingPayloadRanges()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });

        // Misaligned offset (payload offsets must be 8-byte aligned).
        var entry = DirectoryEntry(package, 1);
        var offset = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(16));
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(16), offset + 1);
        OpenThrows(package, FieldPackageBinaryErrorCodes.TileRange);

        // Overlapping ranges: second tile reuses the first tile's offset.
        package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });
        var firstOffset = BinaryPrimitives.ReadUInt64LittleEndian(DirectoryEntry(package, 0).Slice(16));
        BinaryPrimitives.WriteUInt64LittleEndian(DirectoryEntry(package, 1).Slice(16), firstOffset);
        OpenThrows(package, FieldPackageBinaryErrorCodes.TileRange);
    }

    [Fact]
    public void RDR_017_RejectsNonzeroPayloadGapAndTrailingBytes()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });

        // Nonzero gap padding between the two tiles.
        var gapStart = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(DirectoryEntry(package, 0).Slice(16)))
            + ElevationTile().Length;
        Assert.Equal(0, package[gapStart]);
        package[gapStart] = 0xFF;
        OpenThrows(package, FieldPackageBinaryErrorCodes.Header);

        // Trailing unreferenced bytes after the final tile: extend the file and its declared length.
        package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });
        var extended = new byte[package.Length + 8];
        package.CopyTo(extended, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(extended.AsSpan(64), (ulong)extended.Length);
        OpenThrows(extended, FieldPackageBinaryErrorCodes.Length);
    }

    // ---- TIL: tile read conformance ----

    [Fact]
    public void TIL_001_ReadsOnlyTheRequestedTilePayloadRange()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var package = BuildPackage(manifest, new[] { ElevationTile(), ElevationTile(2.5f) });
        var secondTileOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(DirectoryEntry(package, 1).Slice(16)));
        var secondTileEnd = secondTileOffset + ElevationTile(2.5f).Length;

        using var tracking = new TrackingReadStream(package);
        using var reader = FieldPackageReader.Open(tracking, leaveOpen: true);
        tracking.RecordedRanges.Clear();

        _ = reader.ReadTile(1, 0);

        Assert.NotEmpty(tracking.RecordedRanges);
        foreach (var (start, end) in tracking.RecordedRanges)
        {
            Assert.True(start >= secondTileOffset, $"Read touched {start} before tile payload {secondTileOffset}.");
            Assert.True(end <= secondTileEnd, $"Read touched {end} beyond tile payload {secondTileEnd}.");
        }
    }

    [Fact]
    public void TIL_003_RejectsTruncatedStoredTile()
    {
        var package = GoldenMinimalPackage();
        using var stream = new TruncatableStream(package);
        using var reader = FieldPackageReader.Open(stream, leaveOpen: true);

        stream.TruncateTail(6); // physical bytes now end inside the stored tile payload
        var error = Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(0, 0));
        Assert.Equal(FieldPackageBinaryErrorCodes.Truncated, error.Code);
    }

    [Fact]
    public void TIL_004_RejectsCodecVersionPinnedOtherThanNoneV1()
    {
        var manifest = Encoding.UTF8.GetString(GoldenMinimalManifest())
            .Replace("\"version\":\"1\"", "\"version\":\"2\"", StringComparison.Ordinal);
        var package = BuildPackage(
            Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(manifest)),
            new[] { ElevationTile() });
        OpenThrows(package, FieldPackageBinaryErrorCodes.Directory);
    }

    [Fact]
    public void TIL_005_RejectsStoredLengthDifferentFromDecodedLengthForCodecZero()
    {
        var package = GoldenMinimalPackage();
        BinaryPrimitives.WriteUInt64LittleEndian(DirectoryEntry(package, 0).Slice(24), 21); // stored != decoded
        OpenThrows(package, FieldPackageBinaryErrorCodes.Limit);
    }

    [Fact]
    public void TIL_011_RejectsDecodedTileAboveConfiguredBudget()
    {
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new MemoryStream(GoldenMinimalPackage()),
                new FieldPackageReaderOptions { MaximumDecodedTileBytes = 8 }));
        Assert.Equal(FieldPackageBinaryErrorCodes.DecodedLength, error.Code);
    }

    // ---- Stream behavior ----

    [Fact]
    public void Stream_PartialReadsAreLoopedUntilComplete()
    {
        using var partial = new PartialReadStream(GoldenMinimalPackage(), maxChunk: 5);
        using var reader = FieldPackageReader.Open(partial, leaveOpen: true);
        Assert.Equal(GoldenMinimalTile(), reader.ReadTile(0, 0));
    }

    [Fact]
    public void Stream_NonSeekableSourceIsRejected()
    {
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new NonSeekableStream(GoldenMinimalPackage())));
        Assert.Equal(FieldPackageBinaryErrorCodes.Io, error.Code);
    }

    [Fact]
    public void Stream_FailingSourceSurfacesTypedIoError()
    {
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageReader.Open(new FailingReadStream(GoldenMinimalPackage())));
        Assert.Equal(FieldPackageBinaryErrorCodes.Io, error.Code);
    }

    // ---- Resource limits ----

    [Fact]
    public void Limits_ZeroPolicyRejectsEvenMinimalDirectory()
    {
        OpenThrows(GoldenMinimalPackage(), FieldPackageBinaryErrorCodes.Limit,
            new FieldPackageReaderOptions { MaximumDirectoryEntries = 0 });
    }

    [Fact]
    public void Limits_WriterMaximumPackageBytesIsEnforcedDuringSpooling()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var tiles = new[] { TileInput(0, 0, ElevationTile()), TileInput(1, 0, ElevationTile(2.5f)) };

        // Probe the exact package size, then re-write with a cap between the section offsets
        // and the final length so planning passes and the spool loop must enforce the policy.
        using var probe = new MemoryStream();
        var probeResult = FieldPackageWriter.Write(probe, manifest, tiles);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(new MemoryStream(), manifest, tiles,
                new FieldPackageWriterOptions { MaximumPackageBytes = probeResult.PackageLength - 8 }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Limit, error.Code);
    }

    [Fact]
    public void Limits_WriterRejectsDecodedTileAboveBudget()
    {
        var manifest = GoldenMinimalManifest();
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(new MemoryStream(), manifest,
                new[] { TileInput(0, 0, ElevationTile()) },
                new FieldPackageWriterOptions { MaximumDecodedTileBytes = 8 }));
        Assert.Equal(FieldPackageBinaryErrorCodes.DecodedLength, error.Code);
    }

    private static FieldPackageTileInput TileInput(uint x, uint y, byte[] bytes) =>
        new(x, y, 2, 2, bytes);

    // ---- WRT: writer conformance ----

    [Fact]
    public void WRT_001_RepeatedWritesAreByteIdentical()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var tiles = new[] { TileInput(0, 0, ElevationTile()), TileInput(1, 0, ElevationTile(2.5f)) };

        using var first = new MemoryStream();
        var firstResult = FieldPackageWriter.Write(first, manifest, tiles);
        using var second = new MemoryStream();
        var secondResult = FieldPackageWriter.Write(second, manifest, tiles);

        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.Equal(firstResult.PackageSha256, secondResult.PackageSha256);
        Assert.Equal(firstResult.PackageLength, secondResult.PackageLength);
    }

    [Fact]
    public void WRT_004_WriterOutputReopensThroughProductionReader()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        var tiles = new[] { TileInput(0, 0, ElevationTile()), TileInput(1, 0, ElevationTile(2.5f)) };
        using var destination = new MemoryStream();
        FieldPackageWriter.Write(destination, manifest, tiles);

        using var reader = FieldPackageReader.Open(new MemoryStream(destination.ToArray()), leaveOpen: true);
        Assert.Equal(ElevationTile(), reader.ReadTile(0, 0));
        Assert.Equal(ElevationTile(2.5f), reader.ReadTile(1, 0));
    }

    [Fact]
    public void WRT_005_RejectsDuplicateOutOfOrderOutOfGridAndExtraTiles()
    {
        var manifest = ElevationManifest(gridWidth: 4);
        using var destination = new MemoryStream();

        // Out of order: (1,0) before (0,0).
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest,
                new[] { TileInput(1, 0, ElevationTile(2.5f)), TileInput(0, 0, ElevationTile()) }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);

        // Out of grid: three tiles for a two-tile grid.
        error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest,
                new[]
                {
                    TileInput(0, 0, ElevationTile()), TileInput(1, 0, ElevationTile(2.5f)),
                    TileInput(2, 0, ElevationTile()),
                }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);

        // Duplicate coordinate: second entry expected (1,0) but supplies (0,0).
        error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest,
                new[]
                {
                    TileInput(0, 0, ElevationTile()), TileInput(0, 0, ElevationTile(2.5f)),
                    TileInput(1, 0, ElevationTile()),
                }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);
    }

    [Fact]
    public void WRT_006_RejectsCodecZeroWithParameters()
    {
        var manifest = Encoding.UTF8.GetBytes(FieldManifestCanonicalizer.Canonicalize(
            Encoding.UTF8.GetString(GoldenMinimalManifest())
                .Replace("\"parameters\":{}", "\"parameters\":{\"level\":3}", StringComparison.Ordinal)));
        using var destination = new MemoryStream();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest, new[] { TileInput(0, 0, ElevationTile()) }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);
    }

    [Fact]
    public void WRT_007_WRT_008_FailingWriteLeavesNoTemporaryOrFinalOutput()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-conformance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var finalPath = Path.Combine(temporaryDirectory, "out.wvfld");
        try
        {
            var manifest = ElevationManifest(gridWidth: 4);
            // Second tile carries an invalid mask byte: the write fails after the first tile
            // has been spooled, exercising the cleanup of the partial temporary output.
            var invalidSecondTile = ElevationTile(2.5f);
            invalidSecondTile[^1] = 2; // validity mask value must be 0 or 1
            var error = Assert.Throws<FieldPackageValidationException>(() =>
                FieldPackageWriter.WriteFileAtomically(finalPath, manifest,
                    new[] { TileInput(0, 0, ElevationTile()), TileInput(1, 0, invalidSecondTile) },
                    new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory }));
            Assert.Equal(FieldPackageBinaryErrorCodes.Mask, error.Code);

            Assert.False(File.Exists(finalPath));
            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory)); // no *.tmp, no *.spool
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void WRT_008_DiskFullDestinationSurfacesTypedIoError()
    {
        var manifest = GoldenMinimalManifest();
        using var destination = new DiskFullStream();
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest, new[] { TileInput(0, 0, ElevationTile()) }));
        Assert.Equal(FieldPackageBinaryErrorCodes.Io, error.Code);
    }

    [Fact]
    public void WRT_003_WriterSpoolFilesNeverSurviveACompletedWrite()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-conformance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var manifest = ElevationManifest(gridWidth: 4);
            using var destination = new MemoryStream();
            FieldPackageWriter.Write(destination, manifest,
                new[] { TileInput(0, 0, ElevationTile()), TileInput(1, 0, ElevationTile(2.5f)) },
                new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory });

            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory, "*.spool"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    // ---- INT: integrity and lifetime ----

    [Fact]
    public void INT_002_PackageHashMismatchIsDistinctFromTileHashMismatch()
    {
        var package = GoldenMinimalPackage();
        package[^1] ^= 0x01; // flips one payload byte: tile hash AND package hash both differ

        // Whole-package verification (authoritative digest from Core Asset metadata).
        var integrityError = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageIntegrityVerifier.Verify(
                new MemoryStream(package),
                Convert.ToHexString(SHA256.HashData(GoldenMinimalPackage())).ToLowerInvariant()));
        Assert.Equal("WVPK_PACKAGE_HASH", integrityError.Code);

        // Tile-level read of the same corrupt package surfaces the tile-hash code instead.
        using var reader = FieldPackageReader.Open(new MemoryStream(package));
        var tileError = Assert.Throws<FieldPackageValidationException>(() => reader.ReadTile(0, 0));
        Assert.Equal(FieldPackageBinaryErrorCodes.TileHash, tileError.Code);
    }

    [Fact]
    public void INT_004_DisposedReaderRejectsFurtherUse()
    {
        var reader = FieldPackageReader.Open(new MemoryStream(GoldenMinimalPackage()));
        reader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reader.ReadTile(0, 0));
    }

    // ---- Bounded deterministic fuzz ----

    [Fact]
    public void Fuzz_MutatedPackagesAndManifestsOnlyProduceTypedErrors()
    {
        var rng = new Random(unchecked((int)FuzzSeed));
        var golden = new[] { GoldenMinimalPackage(), GoldenEdgePackage(), ReadResource("land-water-4x2-two-tiles.wvfld") };
        var allowed = new HashSet<string>(typeof(FieldPackageBinaryErrorCodes)
            .GetFields()
            .Select(f => (string)f.GetValue(null)!))
        {
            FieldPackageErrorCodes.ManifestEncoding,
            FieldPackageErrorCodes.ManifestCanonical,
            FieldPackageErrorCodes.ManifestSchema,
            FieldPackageErrorCodes.ResourceLimit,
        };

        for (var iteration = 0; iteration < 240; iteration++)
        {
            var source = golden[rng.Next(golden.Length)];
            var mutant = (byte[])source.Clone();
            var mutations = 1 + rng.Next(4);
            for (var m = 0; m < mutations; m++)
                mutant[rng.Next(mutant.Length)] = (byte)rng.Next(256);

            try
            {
                using var reader = FieldPackageReader.Open(new MemoryStream(mutant), leaveOpen: true);
                // A mutant that still opens must expose every tile without leaking a foreign
                // exception; corrupt tiles must fail typed.
                var columns = (reader.GridWidth + reader.TileWidth - 1) / reader.TileWidth;
                var rows = (reader.GridHeight + reader.TileHeight - 1) / reader.TileHeight;
                for (uint y = 0; y < rows; y++)
                    for (uint x = 0; x < columns; x++)
                        _ = reader.ReadTile(x, y);
            }
            catch (FieldPackageValidationException error)
            {
                Assert.True(allowed.Contains(error.Code),
                    $"Iteration {iteration} produced unknown error code {error.Code}: {error.Message}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Fuzz iteration {iteration} escaped the typed package error contract.", ex);
            }
        }
    }

    [Fact]
    public void Fuzz_TruncatedInputsOnlyProduceTypedErrors()
    {
        var source = GoldenMinimalPackage();
        var rng = new Random(unchecked((int)FuzzSeed));

        // Every deterministic prefix length plus randomized cuts; truncation must never
        // succeed (the declared length no longer matches) and never escape the typed contract.
        for (var length = 0; length <= source.Length; length += 7)
        {
            var truncated = source.AsSpan(0, length).ToArray();
            try
            {
                using var reader = FieldPackageReader.Open(new MemoryStream(truncated), leaveOpen: true);
                _ = reader.ReadTile(0, 0);
                throw new InvalidOperationException($"Truncated input of {length} bytes opened successfully.");
            }
            catch (FieldPackageValidationException)
            {
                // Expected: truncation is always rejected.
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Truncation fuzz at {length} bytes escaped the typed package error contract.", ex);
            }
        }

        for (var iteration = 0; iteration < 60; iteration++)
        {
            var cut = 1 + rng.Next(source.Length - 1);
            var truncated = source.AsSpan(0, cut).ToArray();
            try
            {
                FieldPackageReader.Open(new MemoryStream(truncated));
                throw new InvalidOperationException($"Random truncation at {cut} bytes opened successfully.");
            }
            catch (FieldPackageValidationException)
            {
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Random truncation fuzz at {cut} bytes escaped the typed package error contract.", ex);
            }
        }
    }

    // ---- Test doubles ----

    /// <summary>Records every (start, end) byte range served by Read after arming.</summary>
    private sealed class TrackingReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly List<(long Start, long End)> _ranges = new();

        public TrackingReadStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public List<(long Start, long End)> RecordedRanges => _ranges;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var start = _inner.Position;
            var read = _inner.Read(buffer, offset, count);
            _ranges.Add((start, start + read));
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Wraps a buffer whose physical tail can be removed after the reader opened it.</summary>
    private sealed class TruncatableStream : Stream
    {
        private byte[] _bytes;
        private long _length;

        public TruncatableStream(byte[] bytes)
        {
            _bytes = bytes;
            _length = bytes.Length;
        }

        public void TruncateTail(int removedBytes) => _length -= removedBytes;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _length - Position;
            if (remaining <= 0)
                return 0;
            var read = (int)Math.Min(count, remaining);
            Array.Copy(_bytes, Position, buffer, offset, read);
            Position += read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) =>
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Serves at most maxChunk bytes per Read call.</summary>
    private sealed class PartialReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _maxChunk;

        public PartialReadStream(byte[] bytes, int maxChunk)
        {
            _inner = new MemoryStream(bytes);
            _maxChunk = maxChunk;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, Math.Min(count, _maxChunk));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FailingReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private int _reads;

        public FailingReadStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (++_reads > 2)
                throw new IOException("Simulated package source failure.");
            return _inner.Read(buffer, offset, count);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Accepts a bounded number of bytes, then fails like an exhausted disk.</summary>
    private sealed class DiskFullStream : MemoryStream
    {
        private long _written;

        public override void Write(byte[] buffer, int offset, int count)
        {
            _written += count;
            if (_written > 256)
                throw new IOException("Simulated disk full.");
            base.Write(buffer, offset, count);
        }
    }
}
