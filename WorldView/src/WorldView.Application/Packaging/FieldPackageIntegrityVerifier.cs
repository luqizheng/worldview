using System;
using System.IO;
using System.Security.Cryptography;

namespace WorldView.Application.Packaging;

/// <summary>
/// Verifies the complete immutable WVFLDPK byte sequence against the expected digest
/// supplied by the caller's authoritative metadata (never by the package manifest).
/// This operation is sequential and does not mutate the source.
/// </summary>
public static class FieldPackageIntegrityVerifier
{
    /// <summary>
    /// Streams the complete package from byte zero and compares its SHA-256 digest
    /// with an externally supplied expected digest. The original stream position is restored.
    /// </summary>
    /// <exception cref="ArgumentNullException">The source or expected digest is null.</exception>
    /// <exception cref="ArgumentException">The source is not readable/seekable or the digest is not 64 hexadecimal characters.</exception>
    /// <exception cref="FieldPackageValidationException">The complete package digest does not match.</exception>
    public static void Verify(Stream source, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedSha256);

        if (!source.CanRead || !source.CanSeek)
            throw new ArgumentException("Package integrity verification requires a readable, seekable stream.", nameof(source));
        if (expectedSha256.Length != 64)
            throw new ArgumentException("Expected SHA-256 must contain exactly 64 hexadecimal characters.", nameof(expectedSha256));

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Expected SHA-256 contains non-hexadecimal characters.", nameof(expectedSha256), ex);
        }

        var originalPosition = source.Position;
        try
        {
            source.Position = 0;
            var actual = SHA256.HashData(source);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                throw new FieldPackageValidationException(
                    "WVPK_PACKAGE_HASH",
                    "Complete package SHA-256 does not match the authoritative expected digest.");
        }
        finally
        {
            source.Position = originalPosition;
        }
    }
}
