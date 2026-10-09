using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Layout;

/// <summary>
/// 回归护栏：F11 全屏是**会话级**状态，绝不能把它的可见性隐藏落盘。
///
/// <para><b>事故</b>：2026-10-10 用户在 F11 全屏下关窗，<c>SetFullscreen</c> 结尾的
/// <c>MarkLayoutChanged()</c> 把「TopStrip / BottomPalette 不可见」写进
/// <c>.worldview/layouts/sample_world.yaml</c>；第二次启动 <c>ApplyLayout</c> 忠实还原，
/// 用户看到「BottomPalette 没了」。</para>
///
/// <para>护栏两条：① <c>SetFullscreen</c> 内不得调用 <c>MarkLayoutChanged</c>；
/// ② 进全屏前必须 <c>FlushLayoutNow</c> 先落盘真实可见状态。</para>
/// </summary>
public class FullscreenPersistenceTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.csproj")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no WorldView.csproj above)");
        }
    }

    private static string DockLayoutSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Dock", "Service", "DockLayout.cs"));

    /// <summary>
    /// 剥掉 <c>//</c> 行注释与 <c>/* */</c> 块注释、字符串字面量——只留真代码。
    /// 否则护栏会匹配到注释里的「不调 MarkLayoutChanged()」这类说明文字。
    /// </summary>
    private static string StripCommentsAndLiterals(string source)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            // 行注释
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            // 块注释
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }
                i = Math.Min(i + 2, source.Length);
                continue;
            }

            // 字符串 / 原始字符串字面量（简单版：遇引号吞到配对引号）
            if (source[i] == '"')
            {
                sb.Append(' ');
                i++;
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\\')
                    {
                        i++;
                    }
                    i++;
                }
                i = Math.Min(i + 1, source.Length);
                continue;
            }

            sb.Append(source[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>截出 <c>SetFullscreen(bool on)</c> 的方法体（从签名到下一个同级 <c>}</c>）。</summary>
    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"method signature not found: {signature}");

        var openBrace = source.IndexOf('{', start);
        Assert.True(openBrace >= 0, $"no opening brace after: {signature}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }
        }

        throw new InvalidOperationException($"unbalanced braces in method: {signature}");
    }

    [Fact]
    public void SetFullscreen_不得调用_MarkLayoutChanged()
    {
        var body = StripCommentsAndLiterals(ExtractMethod(DockLayoutSource, "public void SetFullscreen(bool on)"));

        Assert.False(
            body.Contains("MarkLayoutChanged"),
            "SetFullscreen must not call MarkLayoutChanged(): F11 visibility hiding is session-scoped "
            + "and persisted visibility must not be dirtied — otherwise 'visible: false' for "
            + "TopStrip/BottomPalette is flushed to .worldview/layouts/<project>.yaml and the next "
            + "launch restores an empty UI.");
    }

    [Fact]
    public void SetFullscreen_进全屏前必须_flush_真实可见状态()
    {
        var body = StripCommentsAndLiterals(ExtractMethod(DockLayoutSource, "public void SetFullscreen(bool on)"));

        // 进全屏前 flush：把「用户当下真实看到的可见状态」落盘，
        // 使之后 F11 期间的防抖 flush 即便触发也不会覆盖成 false（此时已无脏数据）。
        Assert.True(
            body.Contains("FlushLayoutNow()"),
            "SetFullscreen must call FlushLayoutNow() before hiding chrome/panels, so the real "
            + "visible state is already on disk when F11 begins.");
    }

    [Fact]
    public void _ExitTree_仍会_flush_落盘_真实布局()
    {
        // 关窗路径必须保留：否则 F11 之外的正常改动在退出时会丢。
        var body = StripCommentsAndLiterals(ExtractMethod(DockLayoutSource, "public override void _ExitTree()"));

        Assert.True(
            body.Contains("FlushLayoutNow()"),
            "_ExitTree must still flush layout so ordinary (non-F11) layout changes survive quit.");
    }
}
