using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;

namespace WorldView.Application.Tests.Packaging;

public sealed class FieldPackageManifestValidatorTests
{
    [Theory]
    [InlineData("minimal-elevation-2x2.manifest.json")]
    [InlineData("land-water-4x2-two-tiles.manifest.json")]
    [InlineData("edge-elevation-3x2.manifest.json")]
    public void Validate_AcceptsCanonicalGoldenManifestFixtures(string fileName)
    {
        using var validator = new FieldPackageManifestValidator();

        validator.Validate(ReadResource(typeof(FieldPackageManifestValidatorTests).Assembly, fileName));
    }

    [Fact]
    public void EmbeddedSchema_MatchesCanonicalRepositorySchemaBytes()
    {
        var applicationSchema = ReadResource(
            typeof(FieldPackageManifestValidator).Assembly,
            "WorldView.Application.Packaging.Schemas.field-package-manifest.schema.json");
        var testSchema = ReadResource(
            typeof(FieldPackageManifestValidatorTests).Assembly,
            "field-package-manifest.schema.json");

        Assert.Equal(testSchema, applicationSchema);
    }

    [Fact]
    public void Validate_RejectsSchemaInvalidButCanonicalManifest()
    {
        var source = Encoding.UTF8.GetString(ReadResource(
            typeof(FieldPackageManifestValidatorTests).Assembly,
            "minimal-elevation-2x2.manifest.json"));
        var invalid = source.Replace(
            "\"byte_order\":\"little_endian\"",
            "\"byte_order\":\"big_endian\"",
            StringComparison.Ordinal);
        var canonicalInvalid = FieldManifestCanonicalizer.Canonicalize(invalid);

        using var validator = new FieldPackageManifestValidator();
        var exception = Assert.Throws<FieldPackageValidationException>(() =>
            validator.Validate(Encoding.UTF8.GetBytes(canonicalInvalid)));

        Assert.Equal(FieldPackageErrorCodes.ManifestSchema, exception.Code);
    }

    [Fact]
    public void Validate_RejectsNonCanonicalWhitespaceAndDuplicateProperties()
    {
        var canonical = ReadResource(
            typeof(FieldPackageManifestValidatorTests).Assembly,
            "minimal-elevation-2x2.manifest.json");
        var nonCanonical = new byte[canonical.Length + 1];
        nonCanonical[0] = (byte)' ';
        canonical.CopyTo(nonCanonical, 1);

        using var validator = new FieldPackageManifestValidator();
        var whitespace = Assert.Throws<FieldPackageValidationException>(() => validator.Validate(nonCanonical));
        Assert.Equal(FieldPackageErrorCodes.ManifestCanonical, whitespace.Code);

        var duplicate = Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}");
        var duplicateException = Assert.Throws<FieldPackageValidationException>(() => validator.Validate(duplicate));
        Assert.Equal(FieldPackageErrorCodes.ManifestCanonical, duplicateException.Code);
    }

    [Fact]
    public void Validate_RejectsManifestOverTheConfiguredBudgetBeforeParsing()
    {
        using var validator = new FieldPackageManifestValidator();
        var bytes = new byte[FieldPackageManifestValidator.MaximumManifestBytes + 1];

        var exception = Assert.Throws<FieldPackageValidationException>(() => validator.Validate(bytes));

        Assert.Equal(FieldPackageErrorCodes.ResourceLimit, exception.Code);
    }

    private static byte[] ReadResource(Assembly assembly, string suffix)
    {
        var name = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
