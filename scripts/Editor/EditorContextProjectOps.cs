using System.Collections.Generic;
using WorldView.Domain;
using WorldView.ProjectManagement;

namespace WorldView.Editor;

/// <summary>
/// W5：把 <see cref="EditorContext"/>（Godot 节点）适配成 <see cref="IProjectOperations"/>。
/// <para>
/// Godot 4 节点类显式实现 interface 在跨程序集 cast 时会抛 <c>InvalidCastException</c>，
/// 因此这里用 <b>委派模式</b>：每个 IProjectOperations 成员都委派到 EditorContext 的 public 方法。
/// </para>
/// <para>
/// 适配器由 <c>EditorController.InitializeProjectManagement</c> 在启动时构造一次，
/// 整个进程生命周期内复用。
/// </para>
/// </summary>
public sealed class EditorContextProjectOps : IProjectOperations
{
    private readonly EditorContext _ctx;

    public EditorContextProjectOps(EditorContext ctx)
    {
        _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));
    }

    public string? ProjectRoot => string.IsNullOrEmpty(_ctx.ProjectRoot) ? null : _ctx.ProjectRoot;

    public World? World => _ctx.World;

    public IReadOnlyList<MapDoc> Maps => _ctx.Maps;

    public void LoadProject(string path) => _ctx.LoadProject(path);

    public bool SaveProject() => _ctx.SaveProject();

    public void ReplaceMap(MapDoc map) => _ctx.ReplaceMap(map);

    public void AddMap(MapDoc map, string refPath) => _ctx.AddMap(map, refPath);

    public void DrillTo(string mapId) => _ctx.DrillTo(mapId);

    public void UnloadProject() => _ctx.UnloadProject();

    public void NotifyHint(string text) => _ctx.NotifyHint(text);
}
