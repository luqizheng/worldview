using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Layout;

/// <summary>
/// 回归护栏：顶栏必须有「视图」菜单入口，能打开 Inspector / LayerPanel 两个默认隐藏的浮动面板。
///
/// <para><b>背景</b>：<c>PanelState.Default()</c> 里 <c>inspector</c> / <c>layer_panel</c> 都是
/// <c>Visible = false</c>（<c>PanelState.cs:351-370</c>），而 <c>ToggleFloat</c> 是
/// <c>DockLayout</c> 的 public 方法却没有任何调用方 —— 用户在 F5 目检时看不到这两个面板，
/// P0-C1 第 2、3 项（拖 Inspector 入左 Dock / 拖到中央出 FloatWindow）因此无法验证。</para>
///
/// <para>本护栏锁定三件事：① <c>Main.tscn</c> 的 <c>TopBar</c> 下有 <c>ViewBtn</c>；
/// ② <c>EditorController</c> 缓存并连接了它；③ 菜单项绑定了两个 <c>ToggleFloat</c>。</para>
/// </summary>
public class ViewMenuTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no WorldView.sln above)");
        }
    }

    private static string MainScene => File.ReadAllText(Path.Combine(RepoRoot, "scenes", "Main.tscn"));

    private static string EditorControllerSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Editor", "EditorController.cs"));

    [Fact]
    public void Main场景_TopBar_下必须有_ViewBtn()
    {
        Assert.Contains(
            "[node name=\"ViewBtn\" type=\"Button\" parent=\"UiLayer/DockLayout/Root/TopStrip/TopBar\"]",
            MainScene,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EditorController_必须缓存_ViewBtn()
    {
        Assert.Contains(
            "GetNodeOrNull<Button>($\"{TopBarPath}/ViewBtn\")",
            EditorControllerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EditorController_必须连接_ViewBtn_Pressed()
    {
        Assert.Contains(
            "_viewBtn.Pressed += OnViewPressed",
            EditorControllerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void 视图菜单_必须绑定_inspector_与_layer_panel_两个_ToggleFloat()
    {
        Assert.Contains("ToggleFloat(\"inspector\")", EditorControllerSource, StringComparison.Ordinal);
        Assert.Contains("ToggleFloat(\"layer_panel\")", EditorControllerSource, StringComparison.Ordinal);
    }
}
