using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorldView.Application.Packaging;

/// <summary>One canonical, decoded tile supplied to the sequential WVFLDPK writer.</summary>
public sealed record FieldPackageTileInput(
    uint TileX,
    uint TileY,
    ushort ValidWidth,
    ushort ValidHeight,
    ReadOnlyMemory<byte> DecodedBytes);

/// <summary>Resource policy for deterministic WVFLDPK writing.</summary>
public sealed record FieldPackageWriterOptions
{
    public int MaximumDirectoryEntries { get; init; } = 4_000_000;
    public long MaximumStoredTileBytes { get; init; } = 256L * 1024 * 1024;
    public long MaximumDecodedTileBytes { get; init; } = 256L * 1024 * 1024;
    public long MaximumPackageBytes { get; init; } = long.MaxValue;
    public string? TemporaryDirectory { get; init; }
}

/// <summary>Result for a completed and reader-validated package.</summary>
public sealed record FieldPackageWriteResult(long PackageLength, int TileCount, string PackageSha256);

/// <summary>
/// Deterministic codec-0 WVFLDPK 1.0 writer. Tiles are consumed once in canonical
/// (tile_y, tile_x) order and spooled to disk; the complete field is never held in memory.
/// The writer is sequential and not thread-safe.
/// </summary>
public static class FieldPackageWriter
{
    private const int HeaderLength = 72;
    private const int DirectoryHeaderLength = 16;
    private const int DirectoryEntryLength = 72;
    private static readonly byte[] Magic = { (byte)'W', (byte)'V', (byte)'F', (byte)'L', (byte)'D', (byte)'P', (byte)'K', 0 };

    public static FieldPackageWriteResult Write(
        Stream destination,
        ReadOnlyMemory<byte> manifestUtf8,
        IEnumerable<FieldPackageTileInput> tiles,
        FieldPackageWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(tiles);
        options ??= new FieldPackageWriterOptions();
        ValidateOptions(options);
        if (!destination.CanRead || !destination.CanWrite || !destination.CanSeek)
            throw new ArgumentException("Package destination must be readable, writable and seekable for post-write validation.", nameof(destination));

        using (var validator = new FieldPackageManifestValidator())
            validator.Validate(manifestUtf8);

        using var manifestDocument = JsonDocument.Parse(manifestUtf8);
        var root = manifestDocument.RootElement;
        var grid = root.GetProperty("grid");
        var width = PositiveInt(grid, "width");
        var height = PositiveInt(grid, "height");
        var tileWidth = PositiveInt(grid, "tile_width");
        var tileHeight = PositiveInt(grid, "tile_height");
        if (tileWidth > ushort.MaxValue || tileHeight > ushort.MaxValue)
            throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile dimensions exceed the directory representation.");

        var columns = DivideRoundUp(width, tileWidth);
        var rows = DivideRoundUp(height, tileHeight);
        var expectedCount = checked((long)columns * rows);
        if (expectedCount > options.MaximumDirectoryEntries || expectedCount > int.MaxValue)
            throw Error(FieldPackageBinaryErrorCodes.Limit, "Tile directory exceeds the configured writer policy.");

        var planes = root.GetProperty("value_encoding").GetProperty("planes").EnumerateArray()
            .Select(p => new Plane(
                p.GetProperty("name").GetString()!,
                PositiveInt(p, "bytes_per_cell"),
                p.GetProperty("sample_format").GetString()!))
            .ToArray();
        if (planes.Length == 0 || planes.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != planes.Length)
            throw Error(FieldPackageBinaryErrorCodes.Directory, "Manifest plane names must be non-empty and unique.");

        var validityName = root.GetProperty("no_data").GetProperty("mask_plane").GetProperty("plane_name").GetString();
        RequireByteMask(planes, validityName, "validity");
        string? coverageName = null;
        var coverage = root.GetProperty("coverage");
        if (string.Equals(coverage.GetProperty("mode").GetString(), "explicit_mask", StringComparison.Ordinal))
        {
            coverageName = coverage.GetProperty("mask_plane_name").GetString();
            if (string.Equals(coverageName, validityName, StringComparison.Ordinal))
                throw Error(FieldPackageBinaryErrorCodes.Mask, "Coverage and validity masks must be distinct planes.");
            RequireByteMask(planes, coverageName, "coverage");
        }

        var codecs = root.GetProperty("codec_registry").EnumerateArray().ToArray();
        var codecIds = codecs.Select(c => c.GetProperty("codec_id").GetInt32()).ToArray();
        if (codecIds.Distinct().Count() != codecIds.Length || !codecIds.Contains(0))
            throw Error(FieldPackageBinaryErrorCodes.Directory, "Codec registry must contain unique IDs and codec 0.");
        var noCodec = codecs.Single(c => c.GetProperty("codec_id").GetInt32() == 0);
        if (!string.Equals(noCodec.GetProperty("codec_name").GetString(), "none", StringComparison.Ordinal) ||
            !string.Equals(noCodec.GetProperty("version").GetString(), "1", StringComparison.Ordinal) ||
            noCodec.GetProperty("parameters").EnumerateObject().Any())
            throw Error(FieldPackageBinaryErrorCodes.Directory, "Codec 0 must identify the parameter-free none codec version 1.");

        var bytesPerCell = planes.Aggregate(0L, (sum, plane) => checked(sum + plane.BytesPerCell));
        var manifestLength = manifestUtf8.Length;
        var directoryOffset = Align8(checked(HeaderLength + (long)manifestLength));
        var directoryLength = checked(DirectoryHeaderLength + (long)expectedCount * DirectoryEntryLength);
        var payloadOffset = Align8(checked(directoryOffset + directoryLength));
        var plannedPayloadCursor = payloadOffset;
        if (payloadOffset > options.MaximumPackageBytes)
            throw Error(FieldPackageBinaryErrorCodes.Limit, "Package header, manifest and directory exceed the configured writer package-size policy.");

        var temporaryDirectory = options.TemporaryDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(temporaryDirectory);
        var spoolPath = Path.Combine(temporaryDirectory, $"wvfldp-{Guid.NewGuid():N}.spool");

        try
        {
            using var spool = new FileStream(spoolPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                64 * 1024, FileOptions.SequentialScan);
            var entries = new List<DirectoryEntry>(checked((int)expectedCount));
            foreach (var tile in tiles)
            {
                if (entries.Count >= expectedCount)
                    throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile source contains more entries than the dense native grid.");

                var expectedX = (uint)(entries.Count % columns);
                var expectedY = (uint)(entries.Count / columns);
                if (tile.TileX != expectedX || tile.TileY != expectedY)
                    throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile inputs must be complete and ordered by tile_y then tile_x.");

                var validWidth = Math.Min(tileWidth, width - checked((int)tile.TileX * tileWidth));
                var validHeight = Math.Min(tileHeight, height - checked((int)tile.TileY * tileHeight));
                if (tile.ValidWidth != validWidth || tile.ValidHeight != validHeight)
                    throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile valid dimensions do not match its native-grid position.");

                var expectedDecodedLength = checked((long)validWidth * validHeight * bytesPerCell);
                var decodedLength = tile.DecodedBytes.Length;
                if (decodedLength != expectedDecodedLength || decodedLength > options.MaximumDecodedTileBytes)
                    throw Error(FieldPackageBinaryErrorCodes.DecodedLength, "Decoded tile length does not match manifest planes or writer policy.");
                if (decodedLength <= 0 || decodedLength > options.MaximumStoredTileBytes)
                    throw Error(FieldPackageBinaryErrorCodes.Limit, "Stored tile length exceeds writer policy.");

                plannedPayloadCursor = Align8(plannedPayloadCursor);
                plannedPayloadCursor = checked(plannedPayloadCursor + decodedLength);
                if (plannedPayloadCursor > options.MaximumPackageBytes)
                    throw Error(FieldPackageBinaryErrorCodes.Limit, "Package exceeds the configured writer package-size policy.");

                ValidateMaskBytes(tile.DecodedBytes.Span, planes, validityName, coverageName, validWidth, validHeight);
                var hash = SHA256.HashData(tile.DecodedBytes.Span);
                var spoolOffset = spool.Position;
                spool.Write(tile.DecodedBytes.Span);
                entries.Add(new DirectoryEntry(tile.TileX, tile.TileY, tile.ValidWidth, tile.ValidHeight,
                    spoolOffset, decodedLength, hash));
            }

            if (entries.Count != expectedCount)
                throw Error(FieldPackageBinaryErrorCodes.Directory, "Tile source is missing one or more native-grid coordinates.");
            spool.Flush();

            var payloadCursor = payloadOffset;
            foreach (var entry in entries)
            {
                payloadCursor = Align8(payloadCursor);
                entry.PayloadOffset = payloadCursor;
                payloadCursor = checked(payloadCursor + entry.StoredLength);
            }
            var packageLength = payloadCursor;
            if (packageLength != plannedPayloadCursor)
                throw Error(FieldPackageBinaryErrorCodes.Length, "Planned and indexed payload lengths differ.");

            destination.SetLength(0);
            destination.Position = 0;
            WriteZeros(destination, HeaderLength);
            destination.Write(manifestUtf8.Span);
            WriteZeros(destination, directoryOffset - destination.Position);

            Span<byte> directoryHeader = stackalloc byte[DirectoryHeaderLength];
            BinaryPrimitives.WriteUInt16LittleEndian(directoryHeader, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(directoryHeader.Slice(2), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(directoryHeader.Slice(4), DirectoryEntryLength);
            BinaryPrimitives.WriteUInt64LittleEndian(directoryHeader.Slice(8), (ulong)entries.Count);
            destination.Write(directoryHeader);

            Span<byte> directoryEntry = stackalloc byte[DirectoryEntryLength];
            foreach (var entry in entries)
            {
                directoryEntry.Clear();
                BinaryPrimitives.WriteUInt32LittleEndian(directoryEntry, entry.TileX);
                BinaryPrimitives.WriteUInt32LittleEndian(directoryEntry.Slice(4), entry.TileY);
                BinaryPrimitives.WriteUInt16LittleEndian(directoryEntry.Slice(8), entry.ValidWidth);
                BinaryPrimitives.WriteUInt16LittleEndian(directoryEntry.Slice(10), entry.ValidHeight);
                BinaryPrimitives.WriteUInt16LittleEndian(directoryEntry.Slice(12), 0);
                BinaryPrimitives.WriteUInt16LittleEndian(directoryEntry.Slice(14), 0);
                BinaryPrimitives.WriteUInt64LittleEndian(directoryEntry.Slice(16), (ulong)entry.PayloadOffset);
                BinaryPrimitives.WriteUInt64LittleEndian(directoryEntry.Slice(24), (ulong)entry.StoredLength);
                BinaryPrimitives.WriteUInt64LittleEndian(directoryEntry.Slice(32), (ulong)entry.StoredLength);
                entry.Sha256.CopyTo(directoryEntry.Slice(40));
                destination.Write(directoryEntry);
            }

            WriteZeros(destination, payloadOffset - destination.Position);
            var copyBuffer = new byte[64 * 1024];
            foreach (var entry in entries)
            {
                WriteZeros(destination, entry.PayloadOffset - destination.Position);
                spool.Position = entry.SpoolOffset;
                CopyExactly(spool, destination, entry.StoredLength, copyBuffer);
            }
            if (destination.Position != packageLength)
                throw Error(FieldPackageBinaryErrorCodes.Length, "Writer output length differs from the calculated package length.");

            Span<byte> header = stackalloc byte[HeaderLength];
            header.Clear();
            Magic.CopyTo(header);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(8), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(10), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12), HeaderLength);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20), 0);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(24), HeaderLength);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(32), (ulong)manifestLength);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(40), (ulong)directoryOffset);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(48), (ulong)directoryLength);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(56), (ulong)payloadOffset);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(64), (ulong)packageLength);
            destination.Position = 0;
            destination.Write(header);
            destination.Position = packageLength;
            destination.Flush();

            using (var reader = FieldPackageReader.Open(destination, leaveOpen: true))
            {
                foreach (var entry in entries)
                {
                    var decoded = reader.ReadTile(entry.TileX, entry.TileY);
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(decoded), entry.Sha256))
                        throw Error(FieldPackageBinaryErrorCodes.TileHash, "Writer self-validation found a tile hash mismatch.");
                }
            }

            var originalPosition = destination.Position;
            try
            {
                destination.Position = 0;
                var packageHash = Convert.ToHexString(SHA256.HashData(destination)).ToLowerInvariant();
                return new FieldPackageWriteResult(packageLength, entries.Count, packageHash);
            }
            finally
            {
                destination.Position = originalPosition;
            }
        }
        catch (FieldPackageValidationException)
        {
            throw;
        }
        catch (OverflowException ex)
        {
            throw Error(FieldPackageBinaryErrorCodes.Overflow, "Checked writer arithmetic overflowed.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Error(FieldPackageBinaryErrorCodes.Io, "Field Package writing failed.", ex);
        }
        finally
        {
            try { File.Delete(spoolPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Publishes a completed package by same-directory rename. Existing destination files are
    /// never overwritten; a failed write leaves no publishable partial destination.
    /// </summary>
    public static FieldPackageWriteResult WriteFileAtomically(
        string destinationPath,
        ReadOnlyMemory<byte> manifestUtf8,
        IEnumerable<FieldPackageTileInput> tiles,
        FieldPackageWriterOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        if (File.Exists(fullPath))
            throw new IOException("Field Package export destination already exists; refusing to overwrite an immutable artifact.");

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            FieldPackageWriteResult result;
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                64 * 1024, FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                result = Write(stream, manifestUtf8, tiles, options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: false);
            return result;
        }
        catch
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static void ValidateOptions(FieldPackageWriterOptions options)
    {
        if (options.MaximumDirectoryEntries <= 0 || options.MaximumStoredTileBytes <= 0 ||
            options.MaximumDecodedTileBytes <= 0 || options.MaximumPackageBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "All writer resource limits must be positive.");
    }

    private static void RequireByteMask(Plane[] planes, string? name, string role)
    {
        var plane = planes.SingleOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        if (plane is null || plane.BytesPerCell != 1 || plane.SampleFormat != "uint8")
            throw Error(FieldPackageBinaryErrorCodes.Mask, $"Manifest {role} mask must be a one-byte uint8 plane.");
    }

    private static void ValidateMaskBytes(ReadOnlySpan<byte> bytes, Plane[] planes, string? validityName,
        string? coverageName, int width, int height)
    {
        var offset = 0;
        foreach (var plane in planes)
        {
            var length = checked(width * height * plane.BytesPerCell);
            if (string.Equals(plane.Name, validityName, StringComparison.Ordinal) ||
                string.Equals(plane.Name, coverageName, StringComparison.Ordinal))
            {
                for (var i = offset; i < offset + length; i++)
                    if (bytes[i] is not (0 or 1))
                        throw Error(FieldPackageBinaryErrorCodes.Mask, "Validity and coverage masks may contain only 0 or 1.");
            }
            offset += length;
        }
    }

    private static int PositiveInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result) || result <= 0)
            throw Error(FieldPackageBinaryErrorCodes.Limit, $"Manifest {name} is outside supported integer bounds.");
        return result;
    }

    private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);
    private static long Align8(long value) => checked((value + 7) & ~7L);
    private static void WriteZeros(Stream destination, long count)
    {
        if (count < 0) throw Error(FieldPackageBinaryErrorCodes.Overflow, "Negative padding length.");
        Span<byte> zeros = stackalloc byte[8];
        zeros.Clear();
        while (count > 0)
        {
            var write = (int)Math.Min(zeros.Length, count);
            destination.Write(zeros.Slice(0, write));
            count -= write;
        }
    }

    private static void CopyExactly(Stream source, Stream destination, long count, byte[] buffer)
    {
        while (count > 0)
        {
            var requested = (int)Math.Min(buffer.Length, count);
            var read = source.Read(buffer, 0, requested);
            if (read == 0) throw new EndOfStreamException("Tile spool ended before its declared length.");
            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    private sealed record Plane(string Name, int BytesPerCell, string SampleFormat);
    private sealed class DirectoryEntry(
        uint tileX, uint tileY, ushort validWidth, ushort validHeight,
        long spoolOffset, long storedLength, byte[] sha256)
    {
        public uint TileX { get; } = tileX;
        public uint TileY { get; } = tileY;
        public ushort ValidWidth { get; } = validWidth;
        public ushort ValidHeight { get; } = validHeight;
        public long SpoolOffset { get; } = spoolOffset;
        public long StoredLength { get; } = storedLength;
        public byte[] Sha256 { get; } = sha256;
        public long PayloadOffset { get; set; }
    }

    private static FieldPackageValidationException Error(string code, string message, Exception? inner = null)
        => new(code, message, inner);
}
