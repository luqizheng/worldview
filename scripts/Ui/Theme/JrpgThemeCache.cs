using Godot;

namespace WorldView.Ui.Theme;

/// <summary>
/// JRPG 主题单例缓存（§4.5 配套基础设施）
///
/// <para>所有 <c>Jrpg*</c> 组件通过 <see cref="GetTheme"/> 共享同一份 <c>Theme</c> 资源，
/// 避免每控件重复加载 <c>assets/themes/parchment_theme.tres</c>。</para>
///
/// <para>多主题支持（§4.5.5 #15）：项目已收敛为单一 `parchment_theme.tres`（2026-10-09 Dark 下线）。
/// 未来若新增主题（如 Minimal / HighContrast），通过 <see cref="SetActiveTheme"/> 切换，
/// 不需修改任何 Jrpg* 组件代码。</para>
/// </summary>
public static class JrpgThemeCache
{
    private const string DefaultThemePath = "res://assets/themes/parchment_theme.tres";

    private static Godot.Theme? _active;
    private static string _activePath = DefaultThemePath;

    /// <summary>当前激活主题（懒加载）。</summary>
    public static Godot.Theme GetTheme()
    {
        if (_active != null) return _active;
        _active = ResourceLoader.Load<Godot.Theme>(_activePath);
        if (_active == null)
        {
            GD.PushWarning($"[JrpgThemeCache] 加载主题失败: {_activePath},使用 Godot 默认 Theme 兜底");
            _active = new Godot.Theme();
        }
        return _active;
    }

    /// <summary>切换主题（未来扩展接口,目前只有 parchment_theme.tres 一套）。</summary>
    /// <param name="themePath">res:// 路径,需先在 assets/themes/ 下创建</param>
    public static void SetActiveTheme(string themePath)
    {
        if (themePath == _activePath && _active != null) return;
        _activePath = themePath;
        _active = ResourceLoader.Load<Godot.Theme>(_activePath);
        if (_active == null)
        {
            GD.PushError($"[JrpgThemeCache] 切换主题失败: {themePath},回退到 {DefaultThemePath}");
            _activePath = DefaultThemePath;
            _active = ResourceLoader.Load<Godot.Theme>(_activePath);
        }
    }

    /// <summary>清空缓存（热重载时使用,Godot 编辑器 [F5] 重载场景时会触发）。</summary>
    public static void Invalidate()
    {
        _active = null;
    }
}
