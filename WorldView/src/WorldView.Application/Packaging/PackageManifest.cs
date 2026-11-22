using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldView.Application.Packaging;

/// <summary>
/// Project Package manifest v1 模型。manifest 是包格式元数据，不是第二项目身份权威：
/// 领域身份与格式兼容性以数据库内 Bootstrap 元数据与 Core Domain 身份为准（AF-01 §3.1）。
///
/// v1 字段刻意精简；未知成员在解析时忽略以保持向前可读，兼容性由
/// packageFormatVersion 数值决定（更大版本号 = 不受支持，拒绝对其写入）。
/// </summary>
public sealed record PackageManifest
{
    /// <summary>包格式版本（当前 = 1）。</summary>
    [JsonPropertyName("packageFormatVersion")]
    public required int PackageFormatVersion { get; init; }

    /// <summary>数据库相对路径（v1 默认 project.sqlite；必须为包内安全相对路径）。</summary>
    [JsonPropertyName("database")]
    public required ManifestDatabaseEntry Database { get; init; }

    /// <summary>包创建时间（UTC，ISO-8601）。</summary>
    [JsonPropertyName("createdAtUtc")]
    public required DateTimeOffset CreatedAtUtc { get; init; }

    [JsonIgnore]
    public string DatabaseRelativePath => Database.RelativePath;

    public static PackageManifest CreateV1(DateTimeOffset createdAtUtc, string databaseRelativePath
        = ProjectPackageFormat.DatabaseRelativePath) => new()
    {
        PackageFormatVersion = ProjectPackageFormat.SupportedPackageFormatVersion,
        Database = new ManifestDatabaseEntry { RelativePath = databaseRelativePath },
        CreatedAtUtc = createdAtUtc,
    };

    public sealed record ManifestDatabaseEntry
    {
        [JsonPropertyName("relativePath")]
        public required string RelativePath { get; init; }
    }    /// <summary>manifest 解析结果。</summary>
    public sealed class ParseResult
    {
        private ParseResult()
        {
        }

        public bool Success { get; private init; }

        /// <summary>解析失败类别（失败时）。</summary>
        public PackageIssueCode FailureCode { get; private init; }

        /// <summary>诊断说明（失败时）。</summary>
        public string? Diagnostic { get; private init; }

        /// <summary>解析出的 manifest（成功时）。</summary>
        public PackageManifest? Manifest { get; private init; }

        public static ParseResult Ok(PackageManifest manifest) => new() { Success = true, Manifest = manifest };

        public static ParseResult Fail(PackageIssueCode code, string diagnostic) => new()
        {
            Success = false,
            FailureCode = code,
            Diagnostic = diagnostic,
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    /// <summary>序列化为 JSON（写入 manifest.json 使用）。</summary>
    public string ToJson() => JsonSerializer.Serialize(
        new ManifestDto
        {
            PackageFormatVersion = PackageFormatVersion,
            Database = new ManifestDatabaseDto { RelativePath = DatabaseRelativePath },
            CreatedAtUtc = CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        },
        SerializerOptions);

    /// <summary>解析并做基础类型校验（路径合法性由 <see cref="ProjectPackageStructure"/> 与包校验器负责）。</summary>
    public static ParseResult TryParse(string json)
    {
        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return ParseResult.Fail(PackageIssueCode.ManifestMalformed, $"manifest.json 不是合法 JSON：{ex.Message}");
        }

        if (dto is null)
        {
            return ParseResult.Fail(PackageIssueCode.ManifestMalformed, "manifest.json 为空或非对象结构。");
        }

        if (dto.PackageFormatVersion <= 0)
        {
            return ParseResult.Fail(PackageIssueCode.ManifestMalformed, "packageFormatVersion 缺失或非法。");
        }

        if (dto.PackageFormatVersion > ProjectPackageFormat.SupportedPackageFormatVersion)
        {
            return ParseResult.Fail(
                PackageIssueCode.ManifestUnsupportedVersion,
                $"包格式版本 {dto.PackageFormatVersion} 高于本应用支持版本 {ProjectPackageFormat.SupportedPackageFormatVersion}，已拒绝打开。");
        }

        if (dto.Database is null || string.IsNullOrWhiteSpace(dto.Database.RelativePath))
        {
            return ParseResult.Fail(PackageIssueCode.ManifestMalformed, "database.relativePath 缺失。");
        }

        if (!DateTimeOffset.TryParse(dto.CreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var createdAt))
        {
            return ParseResult.Fail(PackageIssueCode.ManifestMalformed, "createdAtUtc 缺失或不是 ISO-8601 时间。");
        }

        return ParseResult.Ok(new PackageManifest
        {
            PackageFormatVersion = dto.PackageFormatVersion,
            Database = new ManifestDatabaseEntry { RelativePath = dto.Database!.RelativePath.Trim() },
            CreatedAtUtc = createdAt,
        });
    }

    private sealed class ManifestDto
    {
        [JsonPropertyName("packageFormatVersion")]
        public int PackageFormatVersion { get; set; }

        [JsonPropertyName("database")]
        public ManifestDatabaseDto? Database { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string? CreatedAtUtc { get; set; }
    }

    private sealed class ManifestDatabaseDto
    {
        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; } = string.Empty;
    }
}
