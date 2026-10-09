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
    public void Leaf_不能_using_Service()
    {
        // Task 2 才会拆 IDockHost 注入, 这里只断言"非 Service 业务类不能 using"
        // 当前 DockPanel/DockZone/FloatWindow 因为 DockLayout.Instance 暂时 using, 留白名单
        var leafFiles = FilesUnder("Dock/Leaf");
        Assert.True(leafFiles.Length >= 7, $"expected >= 7 files under Dock/Leaf/, found {leafFiles.Length}");

        // 例外: 这 3 个文件暂时需要 DockLayout.Instance, Task 2 引入 IDockHost 后会清掉
        var whitelist = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DockPanel.cs", "DockZone.cs", "FloatWindow.cs",
        };

        foreach (var file in leafFiles)
        {
            var name = Path.GetFileName(file);
            if (whitelist.Contains(name)) continue;
            var content = File.ReadAllText(file);
            Assert.False(
                content.Contains("using WorldView.Ui.Dock.Service"),
                $"{file} must not use WorldView.Ui.Dock.Service (leaf layer is dependency-bottom)");
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
