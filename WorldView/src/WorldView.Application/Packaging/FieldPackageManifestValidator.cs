using System;
using System.IO;
using System.Reflection;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Packaging;

/// <summary>Stable machine-readable package validation failure codes.</summary>
public static class FieldPackageErrorCodes
{
    public const string ManifestEncoding = "WVPK_MANIFEST_ENCODING";
    public const string ManifestCanonical = "WVPK_MANIFEST_CANONICAL";
    public const string ManifestSchema = "WVPK_MANIFEST_SCHEMA";
    /// <summary>Descriptor/owner metadata mismatch (ME-05-10 §10 taxonomy).</summary>
    public const string DescriptorMismatch = "WVPK_DESCRIPTOR_MISMATCH";
    public const string ResourceLimit = "WVPK_LIMIT";
}

/// <summary>A typed Field Package validation failure; callers must branch on Code, not Message.</summary>
public sealed class FieldPackageValidationException : Exception
{
    public FieldPackageValidationException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Validates the canonical Field Package manifest using the repository-owned JCS contract
/// and the embedded Draft 2020-12 schema. One instance is used sequentially by a package reader.
/// </summary>
public sealed class FieldPackageManifestValidator : IDisposable
{
    public const int MaximumManifestBytes = 1024 * 1024;
    private const string SchemaResourceName =
        "WorldView.Application.Packaging.Schemas.field-package-manifest.schema.json";

    private readonly JsonSchemaEvaluator _schemaEvaluator;
    private bool _disposed;

    public FieldPackageManifestValidator()
    {
        var schemaBytes = ReadEmbeddedSchema();
        _schemaEvaluator = JsonSchemaEvaluator.Compile(schemaBytes, new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = JsonSchemaDialect.Draft202012,
            CodeGeneration = JsonSchemaCodeGeneration.Disabled,
            CompileRegularExpressions = false,
        });
    }

    public void Validate(ReadOnlyMemory<byte> manifestUtf8)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (manifestUtf8.IsEmpty)
            throw new FieldPackageValidationException(FieldPackageErrorCodes.ManifestEncoding,
                "Field Package manifest is empty.");
        if (manifestUtf8.Length > MaximumManifestBytes)
            throw new FieldPackageValidationException(FieldPackageErrorCodes.ResourceLimit,
                "Field Package manifest exceeds the 1 MiB V1 limit.");

        try
        {
            _ = FieldManifestCanonicalizer.RequireCanonicalUtf8(manifestUtf8.Span);
        }
        catch (CoreDomainDataException ex)
        {
            throw new FieldPackageValidationException(FieldPackageErrorCodes.ManifestCanonical,
                "Field Package manifest is not valid byte-identical RFC 8785 canonical UTF-8.", ex);
        }

        try
        {
            using var document = ParsedJsonDocument<JsonElement>.Parse(manifestUtf8);
            var root = document.RootElement;
            if (!_schemaEvaluator.Evaluate(in root))
                throw new FieldPackageValidationException(FieldPackageErrorCodes.ManifestSchema,
                    "Field Package manifest does not conform to the canonical Draft 2020-12 schema.");
        }
        catch (FieldPackageValidationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or JsonException)
        {
            throw new FieldPackageValidationException(FieldPackageErrorCodes.ManifestEncoding,
                "Field Package manifest could not be parsed as one strict UTF-8 JSON value.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _schemaEvaluator.Dispose();
        _disposed = true;
    }

    private static byte[] ReadEmbeddedSchema()
    {
        using var stream = typeof(FieldPackageManifestValidator).Assembly
            .GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded Field Package schema resource '{SchemaResourceName}' is missing.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
