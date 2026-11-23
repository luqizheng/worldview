using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WorldView.Application.Packaging;

namespace WorldView.Application.Tests.Packaging;

public sealed class FieldPackageWriterTests
{
    [Theory]
    [InlineData("minimal-elevation-2x2.wvfld")]
    [InlineData("land-water-4x2-two-tiles.wvfld")]
    [InlineData("edge-elevation-3x2.wvfld")]
    public void Write_ReproducesCanonicalGoldenPackageByteForByte(string packageName)
    {
        var inventory = JsonDocument.Parse(ReadResource("golden-fixtures.json"));
        var fixture = inventory.RootElement.GetProperty("fixtures").EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("package_file").GetString(), packageName, StringComparison.Ordinal));
        var manifest = ReadResource(fixture.GetProperty("manifest_file").GetString()!);
        var expectedPackage = ReadResource(packageName);
        var expectedHash = fixture.GetProperty("package_sha256").GetString()!;
        var tiles = ReadTiles(fixture.GetProperty("tiles"));

        using var destination = new MemoryStream();
        var result = FieldPackageWriter.Write(destination, manifest, tiles);

        Assert.Equal(expectedPackage, destination.ToArray());
        Assert.Equal(expectedHash, result.PackageSha256);
        Assert.Equal((long)expectedPackage.Length, result.PackageLength);
        Assert.Equal(tiles.Count, result.TileCount);
    }

    [Fact]
    public void WriteFileAtomically_PublishesOnlyValidatedPackage()
    {
        var inventory = JsonDocument.Parse(ReadResource("golden-fixtures.json"));
        var fixture = inventory.RootElement.GetProperty("fixtures").EnumerateArray().First();
        var manifest = ReadResource(fixture.GetProperty("manifest_file").GetString()!);
        var expectedPackage = ReadResource(fixture.GetProperty("package_file").GetString()!);
        var tiles = ReadTiles(fixture.GetProperty("tiles"));
        var path = Path.Combine(Path.GetTempPath(), $"worldview-{Guid.NewGuid():N}.wvfld");

        try
        {
            var result = FieldPackageWriter.WriteFileAtomically(path, manifest, tiles);
            Assert.True(File.Exists(path));
            Assert.Equal(expectedPackage, File.ReadAllBytes(path));
            Assert.Equal(fixture.GetProperty("package_sha256").GetString(), result.PackageSha256);
            Assert.Throws<IOException>(() => FieldPackageWriter.WriteFileAtomically(path, manifest, tiles));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Write_RejectsMissingTileInsteadOfPublishingSparseGrid()
    {
        var manifest = ReadResource("minimal-elevation-2x2.manifest.json");
        using var destination = new MemoryStream();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            FieldPackageWriter.Write(destination, manifest, Array.Empty<FieldPackageTileInput>()));

        Assert.Equal(FieldPackageBinaryErrorCodes.Directory, error.Code);
    }

    private static List<FieldPackageTileInput> ReadTiles(JsonElement tiles)
        => tiles.EnumerateArray().Select(tile => new FieldPackageTileInput(
            checked((uint)tile.GetProperty("tile_x").GetInt32()),
            checked((uint)tile.GetProperty("tile_y").GetInt32()),
            checked((ushort)tile.GetProperty("valid_width").GetInt32()),
            checked((ushort)tile.GetProperty("valid_height").GetInt32()),
            Convert.FromHexString(tile.GetProperty("decoded_hex").GetString()!))).ToList();

    private static byte[] ReadResource(string suffix)
    {
        var assembly = typeof(FieldPackageWriterTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))!;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
