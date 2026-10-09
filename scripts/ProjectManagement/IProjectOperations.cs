using System.Collections.Generic;
using WorldView.Domain;

namespace WorldView.ProjectManagement;

/// <summary>
/// ProjectManagementService 与 Godot 编辑器之间的窄接口（W5：服务层可测性）。
/// <para>
/// 由 <c>scripts/Editor/EditorContext.cs</c> 适配实现；服务层只依赖本接口，
/// 单元测试可以提供内存实现而不启动 Godot 主循环。
/// </para>
/// <para>
/// 约束：C-M跨 / C-D1 / C-C1 / C-D1-弹窗 / C-E1 / C-E3 / C-N2 / C-M内。
/// </para>
/// </summary>
public interface IProjectOperations
{
    /// <summary>当前工程根；未加载时为 null。</summary>
    string? ProjectRoot { get; }

    /// <summary>当前 World 文档；未加载时为 null。</summary>
    World? World { get; }

    /// <summary>当前已加载的地图集合（只读）。</summary>
    IReadOnlyList<MapDoc> Maps { get; }

    /// <summary>从 <paramref name="path"/> 加载工程并广播 <c>MapChanged</c>。</summary>
    void LoadProject(string path);

    /// <summary>保存当前工程。失败时由实现自行处理（Godot 走 PushError / 服务层走异常）。</summary>
    bool SaveProject();

    /// <summary>按 id 替换或追加一张地图文档。</summary>
    void ReplaceMap(MapDoc map);

    /// <summary>追加新地图并同步进 <c>World.MapIndex</c>（C-C2）。</summary>
    void AddMap(MapDoc map, string refPath);

    /// <summary>切入指定地图。</summary>
    void DrillTo(string mapId);

    /// <summary>卸载当前工程（W5 新增：用于 C-M跨 / 关闭项目）。</summary>
    void UnloadProject();

    /// <summary>向状态栏广播一条提示（不抛、不广播 MapChanged）。</summary>
    void NotifyHint(string text);
}
