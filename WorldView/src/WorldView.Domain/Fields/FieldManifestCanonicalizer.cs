using System;
using System.Text;
using System.Linq;
using MackySoft.Json.Canonicalization;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>
/// RFC 8785 JCS canonicalization for Field Package manifests.
/// Kept separate from the legacy Core Domain payload canonicalizer because their
/// numeric contracts differ and existing fingerprints must remain stable.
/// </summary>
public static class FieldManifestCanonicalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Converts a manifest JSON string to its RFC 8785 canonical JSON representation.
    /// Coordinate and spacing values are schema-defined strings and therefore retain
    /// their exact lexical precision.
    /// </summary>
    public static string Canonicalize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            var input = StrictUtf8.GetBytes(json);
            var canonicalBytes = Rfc8785JsonCanonicalizer.Canonicalize(input);
            return StrictUtf8.GetString(canonicalBytes);
        }
        catch (EncoderFallbackException ex)
        {
            throw new CoreDomainDataException(
                "Field manifest contains invalid Unicode and cannot be encoded as strict UTF-8.", ex);
        }
        catch (JsonCanonicalizationException ex)
        {
            throw new CoreDomainDataException(
                $"Field manifest violates the RFC 8785 JCS input contract ({ex.FailureKind}).", ex);
        }
    }

    /// <summary>
    /// Validates that raw package manifest bytes are already strict RFC 8785 canonical UTF-8.
    /// Unlike <see cref="Canonicalize(string)"/>, this method rejects rather than normalizes
    /// BOMs, whitespace, trailing newlines, and any other byte-level difference.
    /// </summary>
    public static byte[] RequireCanonicalUtf8(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var canonicalBytes = Rfc8785JsonCanonicalizer.Canonicalize(utf8Json);
            if (!utf8Json.SequenceEqual(canonicalBytes))
                throw new CoreDomainDataException(
                    "Field package manifest bytes are valid JSON but are not byte-identical RFC 8785 canonical UTF-8.");

            return canonicalBytes;
        }
        catch (JsonCanonicalizationException ex)
        {
            throw new CoreDomainDataException(
                $"Field package manifest bytes violate the RFC 8785 JCS input contract ({ex.FailureKind}).", ex);
        }
    }
}
