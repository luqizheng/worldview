// <auto-not-in-spec />
// Task 1 仓单 §8.1.2 — NoHardcodedHexTests(2 个)
// 纪律: C# 端零硬编码 hex。"颜色字面量" = Color.FromHtml(...) 和 new Color(r, g, b, a) 形式。

using System.Text.RegularExpressions;
using Xunit;

namespace WorldView.Tests.Ui.Theme;

public class NoHardcodedHexTests
{
    /// <summary>
    /// 仓库根定位:从 AppContext.BaseDirectory(bin/Debug/net8.0/)向上找 WorldView.csproj
    /// </summary>
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WorldView.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Cannot find WorldView.csproj above {startDir}");
    }

    private static readonly string UiRoot = Path.Combine(RepoRoot, "scripts", "Ui");

    private static IEnumerable<string> AllCsFiles()
    {
        if (!Directory.Exists(UiRoot)) yield break;
        foreach (var path in Directory.EnumerateFiles(UiRoot, "*.cs", SearchOption.AllDirectories))
            yield return path;
    }

    [Fact]
    public void UiCs_NoColorFromHtml_Anywhere()
    {
        var offenders = new List<string>();
        var pattern = new Regex(@"Color\.FromHtml\s*\(", RegexOptions.Compiled);
        foreach (var path in AllCsFiles())
        {
            // Generator 项目内的 Color.FromHtml 是合法真源(由 yaml 触发生成),不视为硬编码。
            if (path.Replace('\\', '/').Contains("/Generator/")) continue;
            var text = File.ReadAllText(path);
            if (pattern.IsMatch(text))
                offenders.Add(path);
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void ExistingComponents_NoInlineRgbLiteral()
    {
        // 8 个既有 Jrpg* 组件必须不写 new Color(0.xxx, ...) 字面量
        // (此前 JrpgInfoCard 等 6 处内联 RGB 需要在 Task 2/3 重构)。
        var targets = new[]
        {
            "JrpgButton", "JrpgPanel", "JrpgList", "JrpgTextBox",
            "JrpgRichBox", "JrpgInfoCard", "JrpgSelection", "JrpgBreadcrumb",
        };
        var pattern = new Regex(@"new\s+Color\s*\(\s*[0-9](\.[0-9]+)?f?\s*,\s*[0-9](\.[0-9]+)?f?\s*,", RegexOptions.Compiled);
        var offenders = new List<string>();
        foreach (var path in AllCsFiles())
        {
            var fname = Path.GetFileNameWithoutExtension(path);
            if (!targets.Contains(fname)) continue;
            var text = File.ReadAllText(path);
            if (pattern.IsMatch(text))
                offenders.Add($"{fname} ({path})");
        }
        Assert.Empty(offenders);
    }
}
