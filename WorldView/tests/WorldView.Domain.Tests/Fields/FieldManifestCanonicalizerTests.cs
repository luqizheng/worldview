using System;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;
using Xunit;

namespace WorldView.Domain.Tests.Fields;

public sealed class FieldManifestCanonicalizerTests
{
    [Fact]
    public void Canonicalize_UsesRfc8785Binary64NumberSerialization()
    {
        Assert.Equal(
            """{"n":333333333.3333333}""",
            FieldManifestCanonicalizer.Canonicalize("""{"n":333333333.33333329}"""));
    }

    [Fact]
    public void Canonicalize_SortsPropertyNamesByUtf16CodeUnits()
    {
        const string input = """{"":2,"😀":1}""";
        const string expected = """{"😀":1,"":2}""";

        Assert.Equal(expected, FieldManifestCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_PreservesUnicodeWithoutNormalization()
    {
        const string input = """{"value":"A\u030A"}""";
        const string expected = """{"value":"Å"}""";

        Assert.Equal(expected, FieldManifestCanonicalizer.Canonicalize(input));
    }

    [Theory]
    [InlineData("""{"value":-0}""")]
    [InlineData("""{"value":-0.0}""")]
    [InlineData("""{"value":1e400}""")]
    [InlineData("""{"outer":{"value":1,"\u0076alue":2}}""")]
    public void Canonicalize_RejectsJcsInputViolations(string input)
    {
        Assert.Throws<CoreDomainDataException>(
            () => FieldManifestCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_RejectsUnpairedSurrogate()
    {
        var input = "{\"value\":\"" + '\uD800' + "\"}";

        Assert.Throws<CoreDomainDataException>(
            () => FieldManifestCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_PreservesExactDecimalCoordinateStrings()
    {
        const string coordinate = "0.12345678901234567890123456789";
        var input = "{\"origin_x\":\"" + coordinate + "\"}";

        Assert.Equal(input, FieldManifestCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        const string input = """{"z":1e0,"a":[true,{"b":2,"a":3}]}""";
        var canonical = FieldManifestCanonicalizer.Canonicalize(input);

        Assert.Equal(canonical, FieldManifestCanonicalizer.Canonicalize(canonical));
    }
}
