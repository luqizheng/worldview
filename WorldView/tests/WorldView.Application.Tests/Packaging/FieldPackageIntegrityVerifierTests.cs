using System;
using System.IO;
using System.Security.Cryptography;
using WorldView.Application.Packaging;

namespace WorldView.Application.Tests.Packaging;

public sealed class FieldPackageIntegrityVerifierTests
{
    [Fact]
    public void Verify_MatchesExpectedHashOfEntirePackage_AndRestoresPosition()
    {
        var packageBytes = new byte[] { 0x57, 0x56, 0x46, 0x4c, 0x44, 0x50, 0x4b, 0x00, 1, 2, 3, 4 };
        var expected = Convert.ToHexString(SHA256.HashData(packageBytes));
        using var source = new MemoryStream(packageBytes);
        source.Position = 5;

        FieldPackageIntegrityVerifier.Verify(source, expected);

        Assert.Equal(5, source.Position);
    }

    [Fact]
    public void Verify_RejectsMismatch_AndRestoresPosition()
    {
        var packageBytes = new byte[] { 0x57, 0x56, 0x46, 0x4c, 0x44, 0x50, 0x4b, 0x00, 1, 2, 3, 4 };
        using var source = new MemoryStream(packageBytes);
        source.Position = 3;

        var error = Assert.Throws<FieldPackageValidationException>(
            () => FieldPackageIntegrityVerifier.Verify(source, new string('0', 64)));

        Assert.Equal("WVPK_PACKAGE_HASH", error.Code);
        Assert.Equal(3, source.Position);
    }

    [Fact]
    public void Verify_RejectsMalformedExpectedDigest()
    {
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });

        Assert.Throws<ArgumentException>(
            () => FieldPackageIntegrityVerifier.Verify(source, "not-a-sha256"));
    }
}
