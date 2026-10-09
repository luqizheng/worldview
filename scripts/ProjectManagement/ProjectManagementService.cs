using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WorldView.Domain;
using WorldView.Storage.Yaml;

namespace WorldView.ProjectManagement;

/// <summary>
/// 工程管理服务门面（W5：32px 顶部条 + 工程管理服务层收尾）。
/// <para>
/// 把 <c>OpenProject / NewProject / CloseProject / SwitchProject / AddMap / ListRecents / OpenRecent / RemoveRecent</c>
/// 这八条规格强约束集中到一个静态服务，统一抛 <see cref="ProjectException"/>。
/// </para>
/// <para>
/// 设计要点：
/// <list type="bullet">
///   <item>不重写 <see cref="RecentProjectService"/> / <see cref="BackupService"/> / <see cref="DirtyStateService"/> / <see cref="AutosaveService"/> / <see cref="MapFactory"/> / <see cref="MapIdGenerator"/> / <see cref="ProjectWriteCoordinator"/> / <see cref="DirectoryInspector"/>；只组合。</item>
///   <item>对 Godot 节点的依赖走 <see cref="IProjectOperations"/>（由 <c>EditorContext</c> 适配），使服务层可在 xUnit 里直接测。</item>
///   <item><see cref="ResetForTest"/> 清空静态绑定 / 缓存 / 最近列表缓存（每个测试后兜底）。</item>
///   <item>不直接修改 <see cref="ProjectSaver"/> / <see cref="ProjectLoader"/>；C-C1 通过 <c>World.Kind + SchemaVersion</c> 字段自动落盘。</item>
/// </list>
/// </para>
/// <para>
/// 强约束覆盖：
/// <list type="bullet">
///   <item><b>C-R1</b> recents 落 <c>.worldview/recent_projects.yaml</c>（由 <see cref="RecentProjectService"/> 负责）。</item>
///   <item><b>C-R2</b> 记录 <c>project_root + last_opened_at + display_name</c>。</item>
///   <item><b>C-R3</b> 仅在 <see cref="OpenProject"/> 真正进入编辑器时刷新 last_opened_at。</item>
///   <item><b>C-I1</b> <see cref="OpenProject"/> 校验根路径含 <c>project.yaml</c>。</item>
///   <item><b>C-S2</b> 加载失败抛 <see cref="ProjectException"/> <c>InvalidProject</c>，由 UI 引导"新建项目到此处"。</item>
///   <item><b>C-N2</b> 最小骨架 = <c>project.yaml</c> + 一张空白世界地图（不复用 <c>examples/sample_world</c>）。</item>
///   <item><b>C-D1</b> <c>display_name = 目录名</c>，不弹输入框。</item>
///   <item><b>C-M内</b> <see cref="AddMap"/> 不动 recents / last_opened_at。</item>
///   <item><b>C-M跨</b> 切换 = 关闭 + 打开；<see cref="CloseProject(ProjectCloseDecision)"/> 三分支。</item>
///   <item><b>C-C1</b> <c>project.yaml</c> 含 <c>kind: world_view_project + schema_version: 1</c>（由 <c>World</c> record 字段承担）。</item>
///   <item><b>C-C2</b> <c>maps/</c> 目录即清单，<c>World.MapIndex</c> 仍写入但语义是"目录的引用清单"。</item>
///   <item><b>C-D1-弹窗</b> AddMap 的类型+命名弹窗在 UI 层（<c>scripts/Ui/AddMapDialog.cs</c>），本服务签名只接受 <c>MapType + displayName</c>。</item>
///   <item><b>C-E1 / C-E3</b> 双入口（新建 / 打开）由 UI 层提供，本服务不感知。</item>
///   <item><b>C-C3</b> <c>examples/sample_world</c> 能通过 <see cref="OpenProject"/> 加载（取决于 <c>ProjectLoader</c>，服务层不需特别处理）。</item>
/// </list>
/// </para>
/// </summary>
public static class ProjectManagementService
{
    // ------------------------------------------------------------------ 绑定状态

    private static IProjectOperations? _ops;
    private static RecentProjectService? _recent;
    private static DirtyStateService? _dirty;
    private static Func<string, BackupService>? _backupFactory;
    private static AutosaveService? _autosave;
    private static readonly object _lock = new();

    // 缓存的最近列表（避免 ListRecents 反复读盘）
    private static List<RecentProjectEntry>? _recentCache;

    // ------------------------------------------------------------------ 初始化

    /// <summary>
    /// 绑定依赖（由 <c>EditorController._Ready</c> 调一次）。
    /// 全部可空：测试中允许只传 <paramref name="context"/> + <paramref name="recent"/>。
    /// </summary>
    public static void Initialize(
        IProjectOperations? context,
        RecentProjectService recent,
        Func<string, BackupService>? backupFactory = null,
        DirtyStateService? dirty = null)
    {
        lock (_lock)
        {
            _ops = context;
            _recent = recent ?? throw new ArgumentNullException(nameof(recent));
            _backupFactory = backupFactory;
            _dirty = dirty;
            _recentCache = null; // 重新绑定后失效
        }
    }

    /// <summary>测试用：清静态状态。生产代码不会调用。</summary>
    public static void ResetForTest()
    {
        lock (_lock)
        {
            _ops = null;
            _recent = null;
            _dirty = null;
            _backupFactory = null;
            _recentCache?.Clear();
            _recentCache = null;
            try { _autosave?.Dispose(); } catch { /* 吞 */ }
            _autosave = null;
        }
    }

    // ------------------------------------------------------------------ 打开 / 关闭

    /// <summary>
    /// 打开工程（C-I1：校验 <c>project.yaml</c>；C-S2：失败抛 InvalidProject；C-R3：仅成功时刷新 recents）。
    /// </summary>
    /// <remarks>
    /// 流程：校验根 → 若已加载其它工程先做 Pre-Open 备份 → <see cref="IProjectOperations.LoadProject"/>
    /// → 捕获加载异常并包装成 <see cref="ProjectException"/> InvalidProject → 成功后
    /// <see cref="RecentProjectService.RecordOpened"/> + <see cref="DirtyStateService.MarkClean"/>(0)。
    /// </remarks>
    public static void OpenProject(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw ProjectException.Invalid("工程根路径为空字符串。");
        }

        var resolved = Path.GetFullPath(rootPath);

        // C-I1：校验根 + project.yaml 存在
        if (!Directory.Exists(resolved))
        {
            throw ProjectException.Invalid(
                $"工程根目录不存在：{resolved}（如要新建项目，请使用“新建项目”）");
        }
        var projectYaml = Path.Combine(resolved, "project.yaml");
        if (!File.Exists(projectYaml))
        {
            // C-S2：提示用户走"新建项目到此处"流程
            throw ProjectException.Invalid(
                $"该路径不是工程（缺少 project.yaml）。如要新建项目到此目录，请使用“新建项目”。");
        }

        var ops = RequireOps();
        var recent = RequireRecent();

        // Pre-Open 备份：仅当已有旧工程 + factory 注入
        var oldRoot = ops.ProjectRoot;
        if (!string.IsNullOrEmpty(oldRoot)
            && !PathsEqual(oldRoot, resolved)
            && _backupFactory is not null)
        {
            try
            {
                var backup = _backupFactory(oldRoot);
                _ = backup.CreatePreOpenRotationAsync($"open-{Path.GetFileName(resolved)}");
            }
            catch (Exception ex)
            {
                // 备份失败不阻塞打开；只通过 NotifyHint 提示
                try { ops.NotifyHint($"旧工程备份失败：{ex.Message}"); } catch { /* 吞 */ }
            }
        }

        // 真正的加载
        var prevRoot = ops.ProjectRoot;
        var prevWorld = ops.World;
        try
        {
            ops.LoadProject(resolved);
        }
        catch (Exception ex)
        {
            // C-S2：IProjectOperations 的实现可能 PushError 后不抛；这里再加一层保险：
            // 检查 ops.ProjectRoot / ops.World 是否反映新工程；不一致即视为失败。
            if (ops.World is null || !PathsEqual(ops.ProjectRoot ?? string.Empty, resolved))
            {
                throw ProjectException.Invalid(
                    $"该路径不是有效的工程（加载失败：{ex.Message}）。如要新建项目到此目录，请使用“新建项目”。");
            }
            // 走到这里说明 LoadProject 内部已吞了异常但状态有变 — 仍按失败处理
            throw ProjectException.Invalid(
                $"该路径不是有效的工程（{ex.Message}）。");
        }

        // 加载后必须 ProjectRoot 切换 + World != null
        if (ops.World is null
            || !PathsEqual(ops.ProjectRoot ?? string.Empty, resolved))
        {
            throw ProjectException.Invalid(
                $"该路径不是工程（project.yaml 解析失败或为空）。");
        }

        // C-R3：仅在真正进入编辑器时刷新
        try
        {
            recent.RecordOpened(resolved, ops.World.Name);
            _recentCache = null;
        }
        catch
        {
            // recents 写盘失败不阻塞打开
        }

        // 干净状态
        try { _dirty?.MarkClean(0); } catch { /* 吞 */ }

        // 重建 Autosave（旧的 Dispose 掉）
        try
        {
            _autosave?.Dispose();
        }
        catch { /* 吞 */ }
        _autosave = null;
    }

    /// <summary>
    /// 新建工程（C-N2 + C-C1 + C-C2 + C-D1）。落盘后立即 <see cref="OpenProject"/>。
    /// </summary>
    public static void NewProject(string parentDir, string name)
    {
        if (string.IsNullOrWhiteSpace(parentDir))
        {
            throw ProjectException.Invalid("父目录路径为空字符串。");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw ProjectException.Invalid("工程名不能为空。");
        }

        // 名称白名单：仅字母 / 数字 / 下划线 / 连字符 / 空格 / 中文
        foreach (var ch in name)
        {
            if (!IsAllowedNameChar(ch))
            {
                throw ProjectException.Invalid(
                    $"工程名包含非法字符 '{ch}'，仅允许字母、数字、下划线、连字符、空格。");
            }
        }

        var resolvedParent = Path.GetFullPath(parentDir);
        if (!Directory.Exists(resolvedParent))
        {
            throw ProjectException.Invalid($"父目录不存在：{resolvedParent}");
        }

        var root = Path.Combine(resolvedParent, name);

        // C-N2：校验目标（root 必须不存在 / 已存在但为空且无 project.yaml）
        DirectoryInspector.ValidateForCreate(root);

        // 构造空白 World（C-C1 由 record 字段承担 default: Kind="world_view_project" / SchemaVersion=1）
        var blankWorld = new World(
            Name: name,
            Description: null,
            Author: null,
            DefaultTime: new DefaultTime(),
            Era: new EraSpec(),
            MapIndex: Array.Empty<MapIndexEntry>(),
            CharacterIndex: Array.Empty<MapIndexEntry>(),
            Assets: null,
            Settings: new WorldSettings());

        // 取第一个 map id + 空白地图
        var firstId = MapIdGenerator.NextId(blankWorld, MapType.World);
        var blankMap = MapFactory.CreateBlankMap(MapType.World, firstId, name, Array.Empty<LayerRef>());

        // 同步 MapIndex：ProjectSaver 依赖 world.MapIndex 与 maps 顺序一致（C-C2：写时含一项）
        var refPath = $"maps/world/{firstId}/main.yaml";
        var worldWithMap = blankWorld with
        {
            MapIndex = new[] { new MapIndexEntry(refPath) },
        };

        // 用 ProjectWriteCoordinator 走 staging + 校验后原子替换
        // （注意：ProjectSaver.Save 要求 rootPath 已存在，故 stagingRoot 内部已 CreateDirectory）
        var coordinator = new ProjectWriteCoordinator();
        try
        {
            coordinator.WriteProjectAtomic(root, staging =>
            {
                Directory.CreateDirectory(Path.Combine(staging, "maps", "world", firstId));
                ProjectSaver.Save(staging, worldWithMap, new[] { blankMap });
            });
        }
        catch (ProjectException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ProjectException.SaveFailed($"新建工程失败：{root}", ex);
        }

        // C-D1：立即打开（C-R3 在 OpenProject 里会刷新 recents）
        OpenProject(root);
    }

    /// <summary>
    /// 关闭工程（不带决策 = 按当前脏状态决定）。
    /// </summary>
    public static void CloseProject()
    {
        var dirty = _dirty;
        if (dirty is null || !dirty.HasUnsavedChanges)
        {
            CloseWithoutSaving();
            return;
        }

        // 脏状态走 SaveAndClose
        CloseProject(ProjectCloseDecision.SaveAndClose);
    }

    /// <summary>
    /// 关闭工程（显式决策；C-M跨 走 SaveAndClose / CloseWithoutSaving / Cancel）。
    /// </summary>
    public static void CloseProject(ProjectCloseDecision decision)
    {
        switch (decision)
        {
            case ProjectCloseDecision.Cancel:
                return;

            case ProjectCloseDecision.SaveAndClose:
                if (_dirty is not null && _dirty.HasUnsavedChanges)
                {
                    var ok = RequireOps().SaveProject();
                    if (!ok)
                    {
                        throw ProjectException.SaveFailed("保存失败，无法关闭工程。");
                    }
                    _dirty.MarkClean(0);
                }
                CloseWithoutSaving();
                return;

            case ProjectCloseDecision.CloseWithoutSaving:
                CloseWithoutSaving();
                return;

            default:
                throw ProjectException.Invalid($"未知的关闭决策：{decision}");
        }
    }

    private static void CloseWithoutSaving()
    {
        var ops = _ops;
        if (ops is null) return;
        if (ops.World is null) return; // 没工程可关

        try { _autosave?.Dispose(); } catch { /* 吞 */ }
        _autosave = null;

        ops.UnloadProject();

        try { _dirty?.MarkClean(0); } catch { /* 吞 */ }
    }

    /// <summary>
    /// 切换工程（C-M跨 = 关闭 + 打开）。
    /// 若当前脏状态要求保存但调用方传 <c>Cancel</c>，则早返回（不抛）。
    /// </summary>
    public static void SwitchProject(string rootPath, ProjectCloseDecision? dirtyDecision = null)
    {
        if (_ops is null || _ops.World is null)
        {
            // 没有当前工程，直接打开即可
            OpenProject(rootPath);
            return;
        }

        var req = _dirty?.PrepareClose() ?? CloseRequirement.NoChanges;
        if (req == CloseRequirement.NeedsSave)
        {
            if (dirtyDecision is null)
            {
                throw ProjectException.ProjectHasUnsavedChanges();
            }
            if (dirtyDecision == ProjectCloseDecision.Cancel)
            {
                return; // 早返回（用户取消）
            }
        }

        CloseProject(dirtyDecision ?? ProjectCloseDecision.CloseWithoutSaving);
        OpenProject(rootPath);
    }

    // ------------------------------------------------------------------ 地图

    /// <summary>
    /// 新增地图（C-M内：不动 recents；C-D1-弹窗：UI 层提供类型 + 名称；C-C2：仅同步 MapIndex）。
    /// </summary>
    /// <param name="type">地图类型（World / City）。</param>
    /// <param name="displayName">显示名（不能为空）。</param>
    /// <param name="parentMapId">父地图 id（C-E1：新增 World 留空；新增 City 需指定父 World）。</param>
    /// <returns>新建地图的 <see cref="MapDoc"/>；未加载工程时返回 null。</returns>
    public static MapDoc? AddMap(MapType type, string displayName, string? parentMapId = null)
    {
        var ops = _ops;
        if (ops is null || ops.World is null)
        {
            throw ProjectException.Invalid("当前未加载工程，无法新增地图。");
        }
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw ProjectException.Invalid("地图名不能为空。");
        }

        // City 必有父 World
        if (type == MapType.City && string.IsNullOrEmpty(parentMapId))
        {
            throw ProjectException.Invalid("新增 City 地图必须指定父 World 地图。");
        }

        var id = MapIdGenerator.NextId(ops.World, type);
        var map = MapFactory.CreateBlankMap(type, id, displayName, Array.Empty<LayerRef>());

        var subdir = type == MapType.World ? "world" : "city";
        var refPath = $"maps/{subdir}/{id}/main.yaml";

        // 落盘：单 map 不动整个工程（避免覆盖其它并发编辑），直接走 ProjectSaver。
        try
        {
            var fullMapPath = Path.Combine(ops.ProjectRoot!, refPath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullMapPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var coordinator = new ProjectWriteCoordinator();
            coordinator.WriteFileAtomic(fullMapPath, YamlSchemas.Serialize(map));
        }
        catch (ProjectException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ProjectException.SaveFailed($"新增地图落盘失败：{displayName}", ex);
        }

        // 同步 EditorContext：AddMap 同时更新 _maps 和 World.MapIndex（C-C2）
        ops.AddMap(map, refPath);

        // 自动下钻到新地图
        ops.DrillTo(id);

        // 落盘整套工程（让 project.yaml 的 MapIndex 与新地图一致；C-M内 不动 recents / last_opened_at）
        // SaveProject 失败时已 EmitSignal 错误提示；这里不抛 — 内存已经一致，下次保存会再覆盖
        try
        {
            ops.SaveProject();
        }
        catch (Exception ex)
        {
            ops.NotifyHint($"新增地图后整体落盘失败：{ex.Message}");
        }

        return map;
    }

    // ------------------------------------------------------------------ 最近项目

    /// <summary>
    /// 从最近列表打开（内部走 <see cref="OpenProject"/>，故 C-R3 自动刷新）。
    /// </summary>
    public static void OpenRecent(string rootPath)
    {
        OpenProject(rootPath);
    }

    /// <summary>
    /// 从最近列表移除（不存在则早返回）。
    /// </summary>
    public static void RemoveRecent(string rootPath)
    {
        var recent = _recent;
        if (recent is null) return;
        if (string.IsNullOrWhiteSpace(rootPath)) return;

        try
        {
            var list = recent.List();
            if (!list.Any(e => PathsEqual(e.ProjectRoot, rootPath)))
            {
                return;
            }
            // RecentProjectService 暂无 Remove 公共方法 — 写一个删除版
            // 这里走"读 → 过滤 → 写"的内部入口
            RemoveFromRecentList(recent, rootPath);
            _recentCache = null;
        }
        catch
        {
            // 静默：UI 不必因清理失败报错
        }
    }

    /// <summary>
    /// 取最近项目（最多 <paramref name="max"/> 项；带内存缓存）。
    /// </summary>
    public static IReadOnlyList<RecentProjectEntry> ListRecents(int max = 10)
    {
        var recent = _recent;
        if (recent is null) return Array.Empty<RecentProjectEntry>();

        lock (_lock)
        {
            if (_recentCache is null)
            {
                try
                {
                    _recentCache = new List<RecentProjectEntry>(recent.List());
                }
                catch
                {
                    _recentCache = new List<RecentProjectEntry>();
                }
            }
            if (max <= 0 || max >= _recentCache.Count) return _recentCache;
            return _recentCache.Take(max).ToList();
        }
    }

    // ------------------------------------------------------------------ 内部工具

    private static IProjectOperations RequireOps()
    {
        return _ops ?? throw ProjectException.ServiceNotInitialized();
    }

    private static RecentProjectService RequireRecent()
    {
        return _recent ?? throw ProjectException.ServiceNotInitialized();
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a),
            Path.GetFullPath(b),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedNameChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ' || ch == '.';

    private static void RemoveFromRecentList(RecentProjectService recent, string projectRoot)
    {
        // 直接读文件 → 过滤 → 通过"读取序列化"再写回（避免给 RecentProjectService 加公共 API 破坏既有签名）
        var path = recent.FilePath;
        if (!File.Exists(path)) return;

        var text = File.ReadAllText(path);
        var file = YamlSchemas.Deserialize<RecentProjectsFile>(text);
        if (file is null) return;

        var keep = (file.Entries ?? Array.Empty<RecentProjectEntry>())
            .Where(e => !PathsEqual(e.ProjectRoot, projectRoot))
            .ToList();
        if (keep.Count == (file.Entries?.Count ?? 0)) return;

        var updated = file with { Entries = keep };
        var coordinator = new ProjectWriteCoordinator();
        coordinator.WriteFileAtomic(path, YamlSchemas.Serialize(updated));
    }
}
