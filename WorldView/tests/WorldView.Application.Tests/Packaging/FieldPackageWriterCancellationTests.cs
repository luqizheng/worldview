using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using WorldView.Application.Packaging;
using Xunit;

namespace WorldView.Application.Tests.Packaging;

/// <summary>
/// WRT-007 closure: the writer API takes a CancellationToken and cancellation is checked
/// between tile ingestion/spool, payload copy and post-write validation stages, and before
/// the atomic publication rename. Every cancelled write leaves no final output, no temporary
/// output and no spool files. Cancellation is triggered deterministically (no threads) by
/// cancel-on-position tile sources.
/// </summary>
public sealed class FieldPackageWriterCancellationTests
{
    private static byte[] ManifestForTwoTiles() => ReadResource("edge-elevation-3x2.manifest.json");

    /// <summary>First edge tile: 2x2, 4 float32 + 4 mask bytes; second: 1x2 edge.</summary>
    private static FieldPackageTileInput Tile(uint x, uint y, ushort validWidth, ushort validHeight) =>
        new(x, y, validWidth, validHeight, new byte[checked(validWidth * validHeight * 5)]);

    /// <summary>
    /// Yields the two canonical edge tiles and cancels the token after the tile at
    /// <paramref name="cancelAfterIndex"/> has been consumed by the writer.
    /// </summary>
    private static IEnumerable<FieldPackageTileInput> TwoTilesCancellingAfter(
        int cancelAfterIndex, CancellationTokenSource cancellationTokenSource)
    {
        for (var i = 0; i < 2; i++)
        {
            yield return i switch
            {
                0 => Tile(0, 0, 2, 2),
                _ => Tile(1, 0, 1, 2),
            };
            if (i == cancelAfterIndex)
                cancellationTokenSource.Cancel();
        }
    }

    [Fact]
    public void Write_CancelledDuringTileIngestionThrowsAndCleansSpool()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        using var destination = new MemoryStream();
        using var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            // Cancel fires after the first tile is spooled; the second tile must not be ingested.
            var error = Assert.Throws<OperationCanceledException>(() =>
                FieldPackageWriter.Write(destination, ManifestForTwoTiles(),
                    TwoTilesCancellingAfter(cancelAfterIndex: 0, cancellationTokenSource),
                    new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory },
                    cancellationTokenSource.Token));

            Assert.Equal(cancellationTokenSource.Token, error.CancellationToken);
            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory, "*.spool"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Write_CancelledAfterLastTileFailsBeforePackageCompletion()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        using var destination = new MemoryStream();
        using var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            // Cancel fires once the tile source is exhausted: the payload copy stage (checked
            // per chunk) must observe it before the package is completed.
            var error = Assert.Throws<OperationCanceledException>(() =>
                FieldPackageWriter.Write(destination, ManifestForTwoTiles(),
                    TwoTilesCancellingAfter(cancelAfterIndex: 1, cancellationTokenSource),
                    new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory },
                    cancellationTokenSource.Token));

            Assert.Equal(cancellationTokenSource.Token, error.CancellationToken);
            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory, "*.spool"));
            // A cancelled write never finalizes a package: the WVFLDPK magic header is only
            // written by the completion stage after all payload bytes have been copied.
            Assert.NotEqual(Magic, destination.ToArray().AsSpan(0, Magic.Length).ToArray());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteFileAtomically_CancelledDuringTileIngestionLeavesNoFinalOrTempOutput()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var finalPath = Path.Combine(temporaryDirectory, "out.wvfld");
        using var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
                FieldPackageWriter.WriteFileAtomically(finalPath, ManifestForTwoTiles(),
                    TwoTilesCancellingAfter(cancelAfterIndex: 0, cancellationTokenSource),
                    new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory },
                    cancellationTokenSource.Token));

            Assert.Equal(cancellationTokenSource.Token, error.CancellationToken);
            Assert.False(File.Exists(finalPath));
            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory)); // no .tmp, no .spool
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteFileAtomically_CancelledAfterLastTileLeavesNoFinalOrTempOutput()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"wvfldp-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var finalPath = Path.Combine(temporaryDirectory, "out.wvfld");
        using var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            // Cancel fires after the last tile: the writer's post-ingestion stages observe it
            // before the atomic rename, so the formal destination never appears and the
            // temporary file plus spool are removed.
            var error = Assert.Throws<OperationCanceledException>(() =>
                FieldPackageWriter.WriteFileAtomically(finalPath, ManifestForTwoTiles(),
                    TwoTilesCancellingAfter(cancelAfterIndex: 1, cancellationTokenSource),
                    new FieldPackageWriterOptions { TemporaryDirectory = temporaryDirectory },
                    cancellationTokenSource.Token));

            Assert.Equal(cancellationTokenSource.Token, error.CancellationToken);
            Assert.False(File.Exists(finalPath));
            Assert.Empty(Directory.EnumerateFiles(temporaryDirectory));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static readonly byte[] Magic = { (byte)'W', (byte)'V', (byte)'F', (byte)'L', (byte)'D', (byte)'P', (byte)'K', 0 };

    private static byte[] ReadResource(string suffix)
    {
        var assembly = typeof(FieldPackageWriterCancellationTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))!;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
