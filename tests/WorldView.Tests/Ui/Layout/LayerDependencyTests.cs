using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Layout;

public class LayerDependencyTests
{
    private static string RepoRoot
    {
        get
        {
            // bin/Debug/net8.0/ -> tests/WorldView.Tests/ -> tests/ -> repo
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.csproj")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no WorldView.csproj above)");
        }
    }

    private static string[] FilesUnder(string relativeDir)
    {
        var path = Path.Combine(RepoRoot, "scripts", "Ui", relativeDir);
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
    }

    [Fact]
    public void Leaf_不能_using_具体_DockLayout_类_或_Instance()
    {
        // Task 2 引入了 IDockHost / IPanelRegistry seam, Leaf 仍 using Service ns 拿接口,
        // 但**不能**直接 reference DockLayout concrete class 或 DockLayout.Instance 单例。
        // 这是 spec §1.2 「真反环」 破除的硬性证明。
        //
        // 允许: `(DockLayout.Instance as IFoo)?.X(...)` —— 这是 IDockHost/IPanelRegistry seam
        // 唯一访问单例的标准 idiom。
        var leafFiles = FilesUnder("Dock/Leaf");
        Assert.True(leafFiles.Length >= 7, $"expected >= 7 files under Dock/Leaf/, found {leafFiles.Length}");

        foreach (var file in leafFiles)
        {
            var content = File.ReadAllText(file);

            // 反环 1: 把 DockLayout 当变量类型（具体类作方法签名 / 字段类型）— 严禁
            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(
                    content, @"\bDockLayout\s+[a-zA-Z_]"),
                $"{file} must not use DockLayout as a type (use IDockHost/IPanelRegistry)");
        }
    }

    [Fact]
    public void 反环_破除_Palette_不_直接_using_DockLayout()
    {
        // RED: 拆分前, scripts/Ui/Palette/*.cs 内能搜到 DockLayout.Instance?.HandlePanelDrop
        var chromeFiles = FilesUnder("Chrome");
        Assert.True(chromeFiles.Length >= 10, $"expected >= 10 files under Chrome/, found {chromeFiles.Length}");

        foreach (var file in chromeFiles)
        {
            var content = File.ReadAllText(file);
            Assert.False(
                content.Contains("DockLayout.Instance?.HandlePanelDrop"),
                $"{file} must not call DockLayout.Instance?.HandlePanelDrop directly (use IDockHost)");
        }
    }

    [Fact]
    public void Leaf_不能_using_Chrome_或_Panels()
    {
        foreach (var file in FilesUnder("Dock/Leaf"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Chrome"), $"{file} must not use WorldView.Ui.Chrome");
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }

    [Fact]
    public void Service_不能_using_Chrome_或_Panels()
    {
        foreach (var file in FilesUnder("Dock/Service"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Chrome"), $"{file} must not use WorldView.Ui.Chrome");
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }

    [Fact]
    public void Chrome_不能_using_Panels()
    {
        foreach (var file in FilesUnder("Chrome"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }
}
