using Godot;

namespace WorldView.Ui.Theme;

/// <summary>
/// JRPG 主题单例缓存（§4.5 配套基础设施）
///
/// <para>所有 <c>Jrpg*</c> 组件通过 <see cref="GetTheme"/> 共享同一份 <c>Theme</c> 资源，
/// 避免每控件重复加载 <c>assets/themes/parchment_theme.tres</c>。</para>
///
/// <para>多主题支持(§4.5.5 #15):项目已收敛为单一 <c>parchment_theme.tres</c>(2026-10-09 Dark 下线)。
/// Task 2 进一步把 <c>SetActiveTheme</c> 切走 —— JRPG 走单 skin 规约,运行时不再切换主题。
/// 若未来需多 skin(如 Minimal / HighContrast),在 <see cref="GetTheme"/> 内用
/// <c>ResourceLoader</c> 缓存多份 <c>Theme</c> 实例,再扩一个 <c>GetTheme(string name)</c>。
/// </para>
/// </summary>
public static class JrpgThemeCache
{
    private const string DefaultThemePath = "res://assets/themes/parchment_theme.tres";

    private static Godot.Theme? _active;

    /// <summary>当前激活主题(懒加载,固定 parchment_theme.tres)。</summary>
    public static Godot.Theme GetTheme()
    {
        if (_active != null) return _active;
        _active = ResourceLoader.Load<Godot.Theme>(DefaultThemePath);
        if (_active == null)
        {
            GD.PushWarning($"[JrpgThemeCache] 加载主题失败: {DefaultThemePath},使用 Godot 默认 Theme 兜底");
            _active = new Godot.Theme();
        }
        return _active;
    }

    /// <summary>清空缓存(热重载时使用,Godot 编辑器 [F5] 重载场景时会触发)。</summary>
    public static void Invalidate()
    {
        _active = null;
    }
}
