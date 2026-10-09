using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using WorldView.Storage.Yaml;

namespace WorldView.ProjectManagement;

/// <summary>
/// 单条最近项目记录。
/// </summary>
public sealed record RecentProjectEntry
{
    [YamlMember(Alias = "project_root")]
    public string ProjectRoot { get; init; } = "";

    [YamlMember(Alias = "display_name")]
    public string DisplayName { get; init; } = "";

    [YamlMember(Alias = "last_opened_at")]
    public string LastOpenedAt { get; init; } = "";

    /// <summary>扫描后由 List 标记的项目路径是否还存在；不持久化。</summary>
    [YamlIgnore]
    public bool IsMissing { get; init; }

    public RecentProjectEntry() { }

    public RecentProjectEntry(string projectRoot, string displayName, string lastOpenedAt, bool isMissing = false)
    {
        ProjectRoot = projectRoot;
        DisplayName = displayName;
        LastOpenedAt = lastOpenedAt;
        IsMissing = isMissing;
    }

    [YamlIgnore]
    public DateTime LastOpenedAtUtc => DateTime.Parse(
        LastOpenedAt, null,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
}

/// <summary>持久化形态。</summary>
internal sealed record RecentProjectsFile
{
    [YamlMember(Alias = "schema_version")]
    public int SchemaVersion { get; init; } = 1;

    [YamlMember(Alias = "entries")]
    public IReadOnlyList<RecentProjectEntry> Entries { get; init; } = Array.Empty<RecentProjectEntry>();
}

/// <summary>
/// 最近项目清单：路径规范化、按 project_root 去重、最多 10 项、损坏恢复时不覆写原文件。
/// <para>文件位置：<c>&lt;userConfigDir&gt;/.worldview/recent_projects.yaml</c>。</para>
/// </summary>
public sealed class RecentProjectService
{
    public const int MaxEntries = 10;
    public const string DotWorldviewDir = ".worldview";
    public const string FileName = "recent_projects.yaml";

    private readonly IAppDataLocator _locator;
    private readonly IClock _clock;
    private readonly object _lock = new();

    public string FilePath => Path.Combine(_locator.GetUserConfigDir(), DotWorldviewDir, FileName);

    public RecentProjectService(IAppDataLocator locator, IClock clock)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>读取并校验当前清单；损坏抛 <see cref="ProjectException"/> RecentProjectsCorrupted。</summary>
    public IReadOnlyList<RecentProjectEntry> List()
    {
        lock (_lock)
        {
            var path = FilePath;
            if (!File.Exists(path))
                return Array.Empty<RecentProjectEntry>();

            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { throw ProjectException.RecentProjectsCorrupted("最近项目清单读取失败", null); }

            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<RecentProjectEntry>();

            RecentProjectsFile file;
            try
            {
                file = YamlSchemas.Deserialize<RecentProjectsFile>(text);
            }
            catch (Exception ex) when (ex is YamlException || ex is InvalidOperationException)
            {
                throw ProjectException.RecentProjectsCorrupted("最近项目清单 YAML 损坏", ex);
            }
            if (file is null) return Array.Empty<RecentProjectEntry>();

            var entries = file.Entries ?? Array.Empty<RecentProjectEntry>();
            return entries
                .Select(e => e with { IsMissing = !Directory.Exists(e.ProjectRoot) })
                .ToList();
        }
    }

    public void RecordOpened(string projectRoot, string displayName)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("projectRoot 不可为空", nameof(projectRoot));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("displayName 不可为空", nameof(displayName));

        lock (_lock)
        {
            var path = FilePath;
            var existing = LoadFileOrThrow(path);
            var normalized = NormalizePath(projectRoot);
            var now = _clock.UtcNow;

            var list = (existing.Entries ?? Array.Empty<RecentProjectEntry>())
                .Where(e => !PathsEqual(NormalizePath(e.ProjectRoot), normalized))
                .ToList();
            list.Insert(0, new RecentProjectEntry(normalized, displayName, FormatTimestamp(now)));

            if (list.Count > MaxEntries)
                list = list.Take(MaxEntries).ToList();

            SaveFile(path, new RecentProjectsFile { SchemaVersion = 1, Entries = list });
        }
    }

    private RecentProjectsFile LoadFileOrThrow(string path)
    {
        if (!File.Exists(path))
            return new RecentProjectsFile();

        string text;
        try { text = File.ReadAllText(path); }
        catch (IOException) { throw ProjectException.RecentProjectsCorrupted("最近项目清单读取失败", null); }
        if (string.IsNullOrWhiteSpace(text))
            return new RecentProjectsFile();

        try { return YamlSchemas.Deserialize<RecentProjectsFile>(text) ?? new RecentProjectsFile(); }
        catch (Exception ex) when (ex is YamlException || ex is InvalidOperationException)
        {
            throw ProjectException.RecentProjectsCorrupted("最近项目清单 YAML 损坏", ex);
        }
    }

    private void SaveFile(string path, RecentProjectsFile file)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var yaml = YamlSchemas.Serialize(file);
        var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp, yaml);
        if (File.Exists(path))
        {
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase); // Windows 文件系统不区分大小写

    private static string FormatTimestamp(DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
}
