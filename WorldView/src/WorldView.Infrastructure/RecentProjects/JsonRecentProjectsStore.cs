using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;

namespace WorldView.Infrastructure.RecentProjects;

/// <summary>
/// JSON 文件实现的最近项目存储（应用级 UI 元数据；与世界数据严格分离）。
/// 默认位置：%LOCALAPPDATA%/WorldView/preferences/recent-projects.json（macOS: ~/Library/Application Support）。
/// 存储不可用时全部操作静默降级，不影响打开/创建主流程。
/// </summary>
public sealed class JsonRecentProjectsStore : IRecentProjectsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public JsonRecentProjectsStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? GetDefaultBaseDirectory();
        _filePath = Path.Combine(root, "preferences", "recent-projects.json");
    }

    public Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_gate)
            {
                if (!File.Exists(_filePath))
                {
                    return Task.FromResult<IReadOnlyList<RecentProjectEntry>>(Array.Empty<RecentProjectEntry>());
                }

                var json = File.ReadAllText(_filePath);
                var entries = JsonSerializer.Deserialize<List<RecentEntryDto>>(json, SerializerOptions) ?? new List<RecentEntryDto>();
                List<RecentProjectEntry> result = new(entries.Count);
                foreach (var entry in entries)
                {
                    if (entry.PackagePath is not null && entry.LastOpenedAtUtc is not null)
                    {
                        result.Add(new RecentProjectEntry
                        {
                            PackagePath = entry.PackagePath,
                            LastOpenedAtUtc = DateTimeOffset.Parse(entry.LastOpenedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
                        });
                    }
                }

                return Task.FromResult<IReadOnlyList<RecentProjectEntry>>(result);
            }
        }
        catch (Exception)
        {
            return Task.FromResult<IReadOnlyList<RecentProjectEntry>>(Array.Empty<RecentProjectEntry>());
        }
    }

    public Task RecordOpenedAsync(string packagePath, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_gate)
            {
                var directory = Path.GetDirectoryName(_filePath)!;
                Directory.CreateDirectory(directory);

                List<RecentEntryDto> entries = new();
                if (File.Exists(_filePath))
                {
                    entries = JsonSerializer.Deserialize<List<RecentEntryDto>>(File.ReadAllText(_filePath), SerializerOptions) ?? new List<RecentEntryDto>();
                }

                var normalized = System.IO.Path.GetFullPath(packagePath).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                entries.RemoveAll(e => string.Equals(e.PackagePath, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.PackagePath, normalized, StringComparison.Ordinal));
                entries.Insert(0, new RecentEntryDto
                {
                    PackagePath = normalized,
                    LastOpenedAtUtc = openedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                });

                if (entries.Count > 10)
                {
                    entries.RemoveRange(10, entries.Count - 10);
                }

                File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, SerializerOptions));
            }
        }
        catch (Exception)
        {
            // 静默降级：最近项目列表失败不影响主流程。
        }

        return Task.CompletedTask;
    }

    private static string GetDefaultBaseDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldView");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "WorldView");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".worldview");
    }

    private sealed class RecentEntryDto
    {
        [JsonPropertyName("packagePath")]
        public string? PackagePath { get; set; }

        [JsonPropertyName("lastOpenedAtUtc")]
        public string? LastOpenedAtUtc { get; set; }
    }
}
