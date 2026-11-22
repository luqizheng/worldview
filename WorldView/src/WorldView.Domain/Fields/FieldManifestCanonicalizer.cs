using System;
using System.Text;
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
}
