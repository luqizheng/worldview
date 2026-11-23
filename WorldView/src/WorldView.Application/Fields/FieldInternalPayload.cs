using System;
using System.Collections.Generic;

namespace WorldView.Application.Fields;

/// <summary>
/// Identity of the internal Field Payload V1 layout: the decoded tile bytes of one Field
/// Revision, concatenated in canonical (tile_y, tile_x) order with each tile's planes in
/// manifest declaration order, no separators, no package framing. This payload — not the
/// WVFLDPK interchange file — is the authoritative runtime content referenced by payload
/// metadata (ME-05-12 §6).
/// </summary>
public static class FieldInternalPayloadFormat
{
    public const string PayloadFormat = "application/vnd.worldview.field-payload";
    public const string FormatVersion = "1";
}

/// <summary>Canonical byte range of one tile inside an internal Field Payload.</summary>
public sealed record FieldInternalTileSlice(
    uint TileX,
    uint TileY,
    ushort ValidWidth,
    ushort ValidHeight,
    long ByteLength);

/// <summary>
/// Checked-arithmetic layout plan for one internal Field Payload, derived only from the
/// validated typed manifest projection (grid dimensions, tile dimensions, plane declaration
/// order and bytes-per-cell). The plan fixes the canonical tile sequence, the per-tile byte
/// count and the total payload length, so import and export cannot drift apart on layout.
/// </summary>
public sealed class FieldInternalPayloadPlan
{
    private FieldInternalPayloadPlan(IReadOnlyList<FieldInternalTileSlice> tiles, long totalByteLength)
    {
        Tiles = tiles;
        TotalByteLength = totalByteLength;
    }

    /// <summary>Every tile in canonical (tile_y, tile_x) order; no gaps, no duplicates.</summary>
    public IReadOnlyList<FieldInternalTileSlice> Tiles { get; }

    /// <summary>Exact concatenated payload length over all tiles; checked during construction.</summary>
    public long TotalByteLength { get; }

    /// <summary>
    /// Builds the plan from a validated typed projection. Dimensions come from the manifest
    /// the publication authority already validated; every arithmetic step is checked so an
    /// impossible grid fails closed instead of allocating.
    /// </summary>
    public static FieldInternalPayloadPlan FromMetadata(
        Domain.Fields.FieldGridMetadata grid,
        IReadOnlyList<Domain.Fields.FieldValuePlaneMetadata> planes)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(planes);
        if (planes.Count == 0)
            throw new ArgumentException("Internal Field Payload plan requires at least one declared plane.", nameof(planes));

        var bytesPerCell = 0L;
        foreach (var plane in planes)
            bytesPerCell = checked(bytesPerCell + plane.BytesPerCell);
        if (bytesPerCell <= 0)
            throw new ArgumentException("Internal Field Payload plan requires positive bytes per cell.", nameof(planes));

        if (grid.Width < 1 || grid.Height < 1 || grid.TileWidth < 1 || grid.TileHeight < 1)
            throw new ArgumentException("Internal Field Payload plan requires positive grid and tile dimensions.", nameof(grid));
        if (grid.TileWidth > ushort.MaxValue || grid.TileHeight > ushort.MaxValue)
            throw new ArgumentException("Internal Field Payload plan tile dimensions must stay in the uint16 range.", nameof(grid));

        var columns = checked((grid.Width + grid.TileWidth - 1) / grid.TileWidth);
        var rows = checked((grid.Height + grid.TileHeight - 1) / grid.TileHeight);
        var tileCount = checked(columns * rows);
        if (tileCount > int.MaxValue)
            throw new ArgumentException("Internal Field Payload plan exceeds the addressable tile count.", nameof(grid));

        var tiles = new List<FieldInternalTileSlice>(checked((int)tileCount));
        var total = 0L;
        for (long y = 0; y < rows; y++)
        {
            for (long x = 0; x < columns; x++)
            {
                var validWidth = (int)Math.Min(grid.TileWidth, grid.Width - checked(x * grid.TileWidth));
                var validHeight = (int)Math.Min(grid.TileHeight, grid.Height - checked(y * grid.TileHeight));
                var sliceBytes = checked((long)validWidth * validHeight * bytesPerCell);
                total = checked(total + sliceBytes);
                tiles.Add(new FieldInternalTileSlice(
                    (uint)x, (uint)y, (ushort)validWidth, (ushort)validHeight, sliceBytes));
            }
        }

        return new FieldInternalPayloadPlan(tiles, total);
    }
}
