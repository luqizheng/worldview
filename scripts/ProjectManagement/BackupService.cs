using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.ProjectManagement;

/// <summary>
/// 工程备份服务：负责"命名快照"和"打开前轮换"。
/// <list type="number">
///   <item>备份内容目录：<c>&lt;projectRoot&gt;/.worldview/backups/&lt;backup_id&gt;/</c></item>
///   <item>备份清单：<c>&lt;projectRoot&gt;/.worldview/backup_manifest.yaml</c></item>
///   <item>命名快照：要求 <c>displayName</c> 唯一，否则抛 <see cref="ProjectException"/> NameConflict。</item>
///   <item>轮换：每次 <see cref="CreatePreOpenRotationAsync"/> 后保留最近 10 份（按 <c>created_at</c> 升序），超出删除。</item>
/// </list>
/// </summary>
public sealed class BackupService
{
    public const int MaxRotationBackups = 10;
    public const string DotWorldviewDir = ".worldview";
    public const string BackupsSubdir = "backups";

    private readonly string _projectRoot;
    private readonly IClock _clock;
    private readonly ProjectWriteCoordinator? _writer;
    private readonly object _lock = new();

    public string BackupRootDir => Path.Combine(_projectRoot, DotWorldviewDir, BackupsSubdir);
    public string ManifestPath => Path.Combine(_projectRoot, DotWorldviewDir, BackupManifest.FileName);

    public BackupService(string projectRoot, IClock clock, ProjectWriteCoordinator? writer = null)
    {
        if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("projectRoot 不可为空", nameof(projectRoot));
        _projectRoot = projectRoot;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _writer = writer;
    }

    /// <summary>创建一份命名快照；名称必须唯一，否则抛 <see cref="ProjectException"/> NameConflict。</summary>
    public Task<BackupRecord> CreateSnapshotAsync(string displayName, string sourceProjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("displayName 不可为空", nameof(displayName));
        if (string.IsNullOrWhiteSpace(sourceProjectId)) throw new ArgumentException("sourceProjectId 不可为空", nameof(sourceProjectId));

        lock (_lock)
        {
            var manifest = BackupManifest.LoadOrEmpty(ManifestPath);
            if (manifest.Backups.Any(b => string.Equals(b.DisplayName, displayName, StringComparison.Ordinal)))
            {
                throw ProjectException.NameConflict($"已存在同名快照：{displayName}");
            }

            var backupId = Guid.NewGuid().ToString("N");
            var destDir = Path.Combine(BackupRootDir, backupId);
            CopyProjectExcludingDotWorldview(_projectRoot, destDir);

            var record = new BackupRecord(
                backupId,
                BackupKind.NamedSnapshot,
                displayName,
                FormatTimestamp(_clock.UtcNow),
                sourceProjectId,
                destDir);

            var updated = AppendAndSave(manifest, record);
            return Task.FromResult(updated.Backups.First(b => b.BackupId == backupId));
        }
    }

    /// <summary>创建打开前自动轮换快照（无需检查名称冲突）。保留最近 10 份。</summary>
    public Task<BackupRecord> CreatePreOpenRotationAsync(string sourceProjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectId)) throw new ArgumentException("sourceProjectId 不可为空", nameof(sourceProjectId));

        lock (_lock)
        {
            var manifest = BackupManifest.LoadOrEmpty(ManifestPath);
            var now = _clock.UtcNow;
            var backupId = Guid.NewGuid().ToString("N");
            var destDir = Path.Combine(BackupRootDir, backupId);
            CopyProjectExcludingDotWorldview(_projectRoot, destDir);

            var record = new BackupRecord(
                backupId,
                BackupKind.PreOpenRotation,
                "auto-" + now.Ticks,
                FormatTimestamp(now),
                sourceProjectId,
                destDir);

            var updated = AppendAndSave(manifest, record);

            // 轮换：只对 PreOpenRotation 类型计数（命名快照不参与轮换）
            var rotation = updated.Backups
                .Where(b => b.Kind == BackupKind.PreOpenRotation)
                .OrderBy(b => b.CreatedAtUtc)
                .ToList();
            if (rotation.Count > MaxRotationBackups)
            {
                var toRemove = rotation.Take(rotation.Count - MaxRotationBackups).ToList();
                var keep = updated.Backups.Where(b => !toRemove.Any(r => r.BackupId == b.BackupId)).ToList();
                foreach (var rm in toRemove)
                {
                    TryDeleteDir(rm.ContentRoot);
                }
                var rotated = updated with { Backups = keep };
                rotated.Save(ManifestPath);
                return Task.FromResult(rotated.Backups.First(b => b.BackupId == backupId));
            }

            return Task.FromResult(updated.Backups.First(b => b.BackupId == backupId));
        }
    }

    public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var manifest = BackupManifest.LoadOrEmpty(ManifestPath);
            return Task.FromResult<IReadOnlyList<BackupRecord>>(
                manifest.Backups.OrderBy(b => b.CreatedAtUtc).ToList());
        }
    }

    public Task DeleteAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(backupId)) throw new ArgumentException("backupId 不可为空", nameof(backupId));
        lock (_lock)
        {
            var manifest = BackupManifest.LoadOrEmpty(ManifestPath);
            var target = manifest.Backups.FirstOrDefault(b => b.BackupId == backupId);
            if (target is null) return Task.CompletedTask;

            var keep = manifest.Backups.Where(b => b.BackupId != backupId).ToList();
            var updated = manifest with { Backups = keep };
            updated.Save(ManifestPath);
            TryDeleteDir(target.ContentRoot);
            return Task.CompletedTask;
        }
    }

    private BackupManifest AppendAndSave(BackupManifest manifest, BackupRecord record)
    {
        var list = new List<BackupRecord>(manifest.Backups) { record };
        var updated = manifest with { Backups = list };
        updated.Save(ManifestPath);
        return updated;
    }

    private void CopyProjectExcludingDotWorldview(string srcRoot, string destRoot)
    {
        if (!Directory.Exists(srcRoot))
            throw new DirectoryNotFoundException($"工程根不存在：{srcRoot}");
        Directory.CreateDirectory(destRoot);
        CopyDirectoryRecursive(srcRoot, destRoot, excludeDirName: DotWorldviewDir);
    }

    private static void CopyDirectoryRecursive(string srcDir, string destDir, string excludeDirName)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: false);
        }
        foreach (var sub in Directory.EnumerateDirectories(srcDir))
        {
            var name = Path.GetFileName(sub);
            if (string.Equals(name, excludeDirName, StringComparison.Ordinal))
            {
                continue;
            }
            CopyDirectoryRecursive(sub, Path.Combine(destDir, name), excludeDirName);
        }
    }

    private static string FormatTimestamp(DateTime utc)
    {
        return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 静默：清理失败不影响主流程的错误传播。
        }
    }
}
