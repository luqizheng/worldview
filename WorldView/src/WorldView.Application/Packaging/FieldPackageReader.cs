using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorldView.Application.Packaging;

/// <summary>Stable errors raised while opening or reading a WVFLDPK 1.0 package.</summary>
public static class FieldPackageBinaryErrorCodes
{
    public const string Io = "WVPK_IO";
    public const string Truncated = "WVPK_TRUNCATED";
    public const string Magic = "WVPK_MAGIC";
    public const string Version = "WVPK_VERSION";
    public const string Header = "WVPK_HEADER";
    public const string Length = "WVPK_LENGTH";
    public const string Overflow = "WVPK_OVERFLOW";
    public const string Limit = "WVPK_LIMIT";
    public const string Directory = "WVPK_DIRECTORY";
    public const string TileRange = "WVPK_TILE_RANGE";
    public const string CodecUnsupported = "WVPK_CODEC_UNSUPPORTED";
    public const string DecodedLength = "WVPK_DECODED_LENGTH";
    public const string Mask = "WVPK_MASK";
    public const string TileHash = "WVPK_TILE_HASH";
}

/// <summary>Bounded policy for opening and reading an untrusted package.</summary>
public sealed record FieldPackageReaderOptions
{
    public int MaximumDirectoryEntries { get; init; } = 4_000_000;
    public long MaximumStoredTileBytes { get; init; } = 256L * 1024 * 1024;
    public long MaximumDecodedTileBytes { get; init; } = 256L * 1024 * 1024;
}

/// <summary>Validated immutable directory address for one native-grid tile.</summary>
public sealed record FieldPackageTileAddress(
    uint TileX, uint TileY, ushort ValidWidth, ushort ValidHeight, ushort CodecId,
    long PayloadOffset, long StoredLength, long DecodedLength, byte[] Sha256);

/// <summary>
/// Sequential, single-operation WVFLDPK 1.0 reader. The source stream must be seekable and
/// exclusively owned by this reader while operations execute; this V1 implementation is not thread-safe.
/// </summary>
public sealed class FieldPackageReader : IDisposable
{
    private static readonly byte[] Magic = { (byte)'W', (byte)'V', (byte)'F', (byte)'L', (byte)'D', (byte)'P', (byte)'K', 0 };
    private readonly Stream _source;
    private readonly bool _leaveOpen;
    private readonly Dictionary<(uint X, uint Y), FieldPackageTileAddress> _tiles;
    private bool _disposed;

    private FieldPackageReader(Stream source, bool leaveOpen, byte[] manifest, int width, int height,
        int tileWidth, int tileHeight, Dictionary<(uint X, uint Y), FieldPackageTileAddress> tiles)
    {
        _source = source;
        _leaveOpen = leaveOpen;
        ManifestUtf8 = manifest;
        GridWidth = width;
        GridHeight = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _tiles = tiles;
    }

    public ReadOnlyMemory<byte> ManifestUtf8 { get; }
    public int GridWidth { get; }
    public int GridHeight { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int TileCount => _tiles.Count;

    public static FieldPackageReader Open(Stream source, FieldPackageReaderOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new FieldPackageReaderOptions();
        if (!source.CanRead || !source.CanSeek)
            throw Error(FieldPackageBinaryErrorCodes.Io, "WVFLDPK reader requires a readable seekable stream.");
        try
        {
            var physicalLength = source.Length;
            if (physicalLength < 72) throw Error(FieldPackageBinaryErrorCodes.Truncated, "Package is shorter than the 72-byte header.");
            var header = ReadAt(source, 0, 72);
            if (!header.AsSpan(0, 8).SequenceEqual(Magic)) throw Error(FieldPackageBinaryErrorCodes.Magic, "Invalid WVFLDPK magic.");
            var major = U16(header, 8);
            var minor = U16(header, 10);
            if (major != 1 || minor != 0) throw Error(FieldPackageBinaryErrorCodes.Version, $"Unsupported WVFLDPK version {major}.{minor}.");
            if (U32(header, 12) != 72 || U32(header, 16) != 0 || U32(header, 20) != 0)
                throw Error(FieldPackageBinaryErrorCodes.Header, "Invalid header length, flags or reserved field.");

            var manifestOffset = I64(header, 24);
            var manifestLength = I64(header, 32);
            var directoryOffset = I64(header, 40);
            var directoryLength = I64(header, 48);
            var payloadOffset = I64(header, 56);
            var packageLength = I64(header, 64);
            if (packageLength != physicalLength) throw Error(FieldPackageBinaryErrorCodes.Length, "Declared package length differs from physical file length.");
            if (manifestOffset != 72 || manifestLength <= 0 || manifestLength > FieldPackageManifestValidator.MaximumManifestBytes)
                throw Error(FieldPackageBinaryErrorCodes.Limit, "Manifest offset or size is outside V1 policy.");
            var manifestEnd = Add(manifestOffset, manifestLength);
            var expectedDirectoryOffset = Align8(manifestEnd);
            if (directoryOffset != expectedDirectoryOffset || directoryOffset > physicalLength)
                throw Error(FieldPackageBinaryErrorCodes.Header, "Directory offset is not the required aligned manifest end.");
            RequireZero(ReadAt(source, manifestEnd, directoryOffset - manifestEnd), "manifest padding");

            var manifest = ReadAt(source, manifestOffset, checked((int)manifestLength));
            using (var validator = new FieldPackageManifestValidator()) validator.Validate(manifest);
            using var json = JsonDocument.Parse(manifest);
            var root = json.RootElement;
            var grid = root.GetProperty("grid");
            var width = ReadPositiveInt(grid, "width");
            var height = ReadPositiveInt(grid, "height");
            var tileWidth = ReadPositiveInt(grid, "tile_width");
            var tileHeight = ReadPositiveInt(grid, "tile_height");
            if (tileWidth > ushort.MaxValue || tileHeight > ushort.MaxValue)
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile dimensions exceed directory representation.");

            var columns = DivideRoundUp(width, tileWidth);
            var rows = DivideRoundUp(height, tileHeight);
            var expectedCount = checked((long)columns * rows);
            if (expectedCount > options.MaximumDirectoryEntries)
                throw Error(FieldPackageBinaryErrorCodes.Limit, "Tile directory exceeds configured entry limit.");

            var planes = root.GetProperty("value_encoding").GetProperty("planes").EnumerateArray()
                .Select(p => new Plane(
                    p.GetProperty("name").GetString()!,
                    ReadPositiveInt(p, "bytes_per_cell"),
                    p.GetProperty("sample_format").GetString()!))
                .ToArray();
            if (planes.Length == 0) throw Error(FieldPackageBinaryErrorCodes.Directory, "Manifest declares no value planes.");
            if (planes.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != planes.Length)
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Manifest plane names must be unique.");
            var validityPlaneName = root.GetProperty("no_data").GetProperty("mask_plane").GetProperty("plane_name").GetString();
            var validityPlane = planes.SingleOrDefault(p => string.Equals(p.Name, validityPlaneName, StringComparison.Ordinal));
            if (validityPlane is null || validityPlane.BytesPerCell != 1 || validityPlane.SampleFormat != "uint8")
                throw Error(FieldPackageBinaryErrorCodes.Mask, "Manifest must declare its validity mask as a one-byte uint8 plane.");

            string? coveragePlaneName = null;
            var coverage = root.GetProperty("coverage");
            if (string.Equals(coverage.GetProperty("mode").GetString(), "explicit_mask", StringComparison.Ordinal))
            {
                coveragePlaneName = coverage.GetProperty("mask_plane_name").GetString();
                if (string.Equals(coveragePlaneName, validityPlaneName, StringComparison.Ordinal))
                    throw Error(FieldPackageBinaryErrorCodes.Mask, "Coverage and validity masks must be distinct planes.");
                var coveragePlane = planes.SingleOrDefault(p => string.Equals(p.Name, coveragePlaneName, StringComparison.Ordinal));
                if (coveragePlane is null || coveragePlane.BytesPerCell != 1 || coveragePlane.SampleFormat != "uint8")
                    throw Error(FieldPackageBinaryErrorCodes.Mask, "Explicit coverage mask must be a distinct one-byte uint8 plane.");
            }
            var codecRegistry = root.GetProperty("codec_registry").EnumerateArray().ToArray();
            var codecIds = codecRegistry.Select(c => c.GetProperty("codec_id").GetInt32()).ToArray();
            if (codecIds.Distinct().Count() != codecIds.Length || !codecIds.Contains(0))
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Codec registry must contain unique IDs and codec 0.");
            var noCodec = codecRegistry.Single(c => c.GetProperty("codec_id").GetInt32() == 0);
            if (!string.Equals(noCodec.GetProperty("codec_name").GetString(), "none", StringComparison.Ordinal) ||
                !string.Equals(noCodec.GetProperty("version").GetString(), "1", StringComparison.Ordinal) ||
                noCodec.GetProperty("parameters").EnumerateObject().Any())
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Codec 0 must identify the parameter-free none codec version 1.");

            var directoryHeader = ReadAt(source, directoryOffset, 16);
            if (U16(directoryHeader, 0) != 1 || U16(directoryHeader, 2) != 0 || U32(directoryHeader, 4) != 72)
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Unsupported directory version, flags or entry size.");
            var entryCount = U64(directoryHeader, 8);
            var calculatedDirectoryLength = Add(16, Multiply(entryCount, 72));
            if (entryCount != (ulong)expectedCount || calculatedDirectoryLength != (ulong)directoryLength ||
                directoryLength < 16 || Add(directoryOffset, directoryLength) > physicalLength)
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Directory count or exact byte length is invalid.");
            var expectedPayloadOffset = Align8(Add(directoryOffset, directoryLength));
            if (payloadOffset != expectedPayloadOffset || payloadOffset > physicalLength)
                throw Error(FieldPackageBinaryErrorCodes.Header, "Payload offset is not the required aligned directory end.");
            RequireZero(ReadAt(source, Add(directoryOffset, directoryLength), payloadOffset - Add(directoryOffset, directoryLength)), "directory padding");

            var entriesBytes = ReadAt(source, Add(directoryOffset, 16), checked((int)Multiply(entryCount, 72)));
            var tiles = new Dictionary<(uint X, uint Y), FieldPackageTileAddress>();
            var ranges = new List<(long Start, long End)>();
            uint previousY = 0, previousX = 0;
            for (var i = 0; i < checked((int)entryCount); i++)
            {
                var e = entriesBytes.AsSpan(i * 72, 72);
                var x = U32(e, 0); var y = U32(e, 4);
                var validWidth = U16(e, 8); var validHeight = U16(e, 10);
                var codec = U16(e, 12); var flags = U16(e, 14);
                var offset = I64(e, 16); var stored = I64(e, 24); var decoded = I64(e, 32);
                var hash = e.Slice(40, 32).ToArray();
                if (flags != 0 || x >= columns || y >= rows || (i > 0 && (y < previousY || (y == previousY && x <= previousX))))
                    throw Error(FieldPackageBinaryErrorCodes.Directory, "Directory entries are not unique canonical grid order.");
                previousX = x; previousY = y;
                var expectedW = Math.Min(tileWidth, width - checked((int)x * tileWidth));
                var expectedH = Math.Min(tileHeight, height - checked((int)y * tileHeight));
                if (validWidth != expectedW || validHeight != expectedH)
                    throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile edge dimensions do not match the native grid.");
                var bytesPerCell = planes.Aggregate(0L, (sum, plane) => checked(sum + plane.BytesPerCell));
                var expectedDecoded = Multiply(Multiply(validWidth, validHeight), (ulong)bytesPerCell);
                if (decoded < 0 || (ulong)decoded != expectedDecoded || decoded > options.MaximumDecodedTileBytes)
                    throw Error(FieldPackageBinaryErrorCodes.DecodedLength, "Tile decoded length does not match manifest planes or policy.");
                if (codec != 0 || !codecIds.Contains(codec))
                    throw Error(FieldPackageBinaryErrorCodes.CodecUnsupported, $"Codec {codec} is not supported by this reader.");
                if (stored <= 0 || stored > options.MaximumStoredTileBytes || (codec == 0 && stored != decoded))
                    throw Error(FieldPackageBinaryErrorCodes.Limit, "Stored tile length is invalid or exceeds policy.");
                if (offset < payloadOffset || (offset & 7) != 0)
                    throw Error(FieldPackageBinaryErrorCodes.TileRange, "Tile payload offset is outside or not aligned.");
                var end = Add((ulong)offset, (ulong)stored);
                if (end > (ulong)packageLength) throw Error(FieldPackageBinaryErrorCodes.TileRange, "Tile payload extends beyond package length.");
                ranges.Add((offset, checked((long)end)));
                tiles.Add((x, y), new FieldPackageTileAddress(x, y, validWidth, validHeight, codec, offset, stored, decoded, hash));
            }

            ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
            var cursor = payloadOffset;
            foreach (var range in ranges)
            {
                if (range.Start < cursor) throw Error(FieldPackageBinaryErrorCodes.TileRange, "Tile payload ranges overlap.");
                RequireZero(ReadAt(source, cursor, range.Start - cursor), "tile payload padding");
                cursor = range.End;
            }
            if (cursor != packageLength) throw Error(FieldPackageBinaryErrorCodes.Length, "Trailing or unreferenced bytes follow the final tile payload.");
            return new FieldPackageReader(source, leaveOpen, manifest, width, height, tileWidth, tileHeight, tiles);
        }
        catch (FieldPackageValidationException) { throw; }
        catch (OverflowException ex) { throw Error(FieldPackageBinaryErrorCodes.Overflow, "Checked package arithmetic overflowed.", ex); }
        catch (EndOfStreamException ex) { throw Error(FieldPackageBinaryErrorCodes.Truncated, "Package ended before a required section was read.", ex); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw Error(FieldPackageBinaryErrorCodes.Io, "Package I/O failed.", ex); }
    }

    public byte[] ReadTile(uint tileX, uint tileY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_tiles.TryGetValue((tileX, tileY), out var address))
            throw Error(FieldPackageBinaryErrorCodes.Directory, "Requested tile coordinate is outside the validated grid.");
        var stored = ReadAt(_source, address.PayloadOffset, checked((int)address.StoredLength));
        var digest = SHA256.HashData(stored);
        if (!CryptographicOperations.FixedTimeEquals(digest, address.Sha256))
            throw Error(FieldPackageBinaryErrorCodes.TileHash, $"Tile ({tileX},{tileY}) SHA-256 does not match its directory entry.");
        using var json = JsonDocument.Parse(ManifestUtf8);
        var validityName = json.RootElement.GetProperty("no_data").GetProperty("mask_plane").GetProperty("plane_name").GetString();
        var coverage = json.RootElement.GetProperty("coverage");
        var coverageName = string.Equals(coverage.GetProperty("mode").GetString(), "explicit_mask", StringComparison.Ordinal)
            ? coverage.GetProperty("mask_plane_name").GetString()
            : null;
        var planes = json.RootElement.GetProperty("value_encoding").GetProperty("planes").EnumerateArray().ToArray();
        var offset = 0;
        for (var i = 0; i < planes.Length; i++)
        {
            var name = planes[i].GetProperty("name").GetString();
            var bytesPerCell = planes[i].GetProperty("bytes_per_cell").GetInt32();
            var length = checked(address.ValidWidth * address.ValidHeight * bytesPerCell);
            if (string.Equals(name, validityName, StringComparison.Ordinal) ||
                string.Equals(name, coverageName, StringComparison.Ordinal))
            {
                for (var j = offset; j < offset + length; j++)
                    if (stored[j] is not (0 or 1))
                        throw Error(FieldPackageBinaryErrorCodes.Mask, "Validity and coverage masks may contain only 0 or 1.");
            }
            offset += length;
        }
        return stored;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_leaveOpen) _source.Dispose();
    }

    private sealed record Plane(string Name, int BytesPerCell, string SampleFormat);
    private static FieldPackageValidationException Error(string code, string message, Exception? inner = null)
        => new(code, message, inner);
    private static byte[] ReadAt(Stream stream, long offset, long count)
    {
        if (count < 0 || count > int.MaxValue) throw Error(FieldPackageBinaryErrorCodes.Limit, "Requested section is too large.");
        var bytes = new byte[checked((int)count)];
        stream.Position = offset;
        var read = 0;
        while (read < bytes.Length)
        {
            var n = stream.Read(bytes, read, bytes.Length - read);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
        return bytes;
    }
    private static void RequireZero(byte[] bytes, string section)
    {
        if (bytes.Any(b => b != 0)) throw Error(FieldPackageBinaryErrorCodes.Header, $"Nonzero bytes in {section}.");
    }
    private static int ReadPositiveInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result) || result <= 0)
            throw Error(FieldPackageBinaryErrorCodes.Limit, $"Manifest {name} is outside supported integer bounds.");
        return result;
    }
    private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);
    private static ushort U16(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(o, 2));
    private static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(o, 4));
    private static ulong U64(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(o, 8));
    private static long I64(ReadOnlySpan<byte> b, int o)
    {
        var value = U64(b, o);
        if (value > long.MaxValue) throw new OverflowException();
        return (long)value;
    }
    private static long Add(long a, long b) => checked(a + b);
    private static ulong Add(ulong a, ulong b) => checked(a + b);
    private static ulong Multiply(ulong a, ulong b) => checked(a * b);
    private static long Align8(long value) => checked((value + 7) & ~7L);
}
