using System;
using System.Collections.Generic;
using System.IO;
using WorldView.Storage.Yaml;
using YamlDotNet.Serialization;

namespace WorldView.ProjectManagement;

/// <summary>备份种类。</summary>
public enum BackupKind
{
    NamedSnapshot,
    PreOpenRotation,
}

/// <summary>备份清单中的一项记录。init-only 属性便于 YamlDotNet 反射反序列化。</summary>
public sealed record BackupRecord
{
    [YamlMember(Alias = "backup_id")]
    public string BackupId { get; init; } = "";

    [YamlMember(Alias = "kind")]
    public BackupKind Kind { get; init; }

    [YamlMember(Alias = "display_name")]
    public string DisplayName { get; init; } = "";

    [YamlMember(Alias = "created_at")]
    public string CreatedAt { get; init; } = "";

    [YamlMember(Alias = "source_project_id")]
    public string SourceProjectId { get; init; } = "";

    [YamlMember(Alias = "content_root")]
    public string ContentRoot { get; init; } = "";

    public BackupRecord() { }

    public BackupRecord(
        string backupId,
        BackupKind kind,
        string displayName,
        string createdAt,
        string sourceProjectId,
        string contentRoot)
    {
        BackupId = backupId;
        Kind = kind;
        DisplayName = displayName;
        CreatedAt = createdAt;
        SourceProjectId = sourceProjectId;
        ContentRoot = contentRoot;
    }

    /// <summary>解析 <see cref="CreatedAt"/> 为 UTC（不可逆；如需可逆请在调用方用 <see cref="IClock"/>）。</summary>
    [YamlIgnore]
    public DateTime CreatedAtUtc => DateTime.Parse(CreatedAt, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
}

/// <summary>
/// 备份清单。持久化在 <c>&lt;projectRoot&gt;/.worldview/backup_manifest.yaml</c>。
/// <para>备份内容目录：<c>&lt;projectRoot&gt;/.worldview/backups/&lt;backup_id&gt;/</c>。</para>
/// </summary>
public sealed record BackupManifest
{
    public const string FileName = "backup_manifest.yaml";
    public const int CurrentSchemaVersion = 1;

    [YamlMember(Alias = "schema_version")]
    public int SchemaVersion { get; init; } = 1;

    [YamlMember(Alias = "backups")]
    public IReadOnlyList<BackupRecord> Backups { get; init; } = Array.Empty<BackupRecord>();

    public BackupManifest() { }

    public BackupManifest(int schemaVersion, IReadOnlyList<BackupRecord> backups)
    {
        SchemaVersion = schemaVersion;
        Backups = backups;
    }

    /// <summary>从文件加载；不存在或反序列化失败时返回空清单。</summary>
    public static BackupManifest LoadOrEmpty(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return new BackupManifest();
        try
        {
            var text = File.ReadAllText(filePath);
            var loaded = YamlSchemas.Deserialize<BackupManifest>(text);
            return loaded ?? new BackupManifest();
        }
        catch
        {
            // 损坏的清单视作空：调用方负责写回修复后的版本
            return new BackupManifest();
        }
    }

    /// <summary>写入文件。原子写：先写临时，再替换。</summary>
    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var yaml = YamlSchemas.Serialize(this);
        var tmp = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp, yaml);
        if (File.Exists(filePath))
        {
            File.Replace(tmp, filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, filePath);
        }
    }
}
