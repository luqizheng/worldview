using System;
using System.IO;

namespace WorldView.Tests.Support;

/// <summary>
/// 测试路径定位工具。
///
/// 关键点：不依赖 <c>Directory.GetCurrentDirectory()</c>——MTP runner 会把
/// 自己的工作目录切到 exe 所在目录（bin/Debug/net8.0/），导致向上递归找
/// <c>project.yaml</c> 永远找不到（14 条测试因此失败）。
///
/// 改用 <c>AppContext.BaseDirectory</c>（即程序集所在目录）反推仓库根，
/// 与 runner 的 cwd 无关。
/// </summary>
public static class TestPaths
{
    /// <summary>从程序集位置向上递归找 WorldView.sln，返回其所在目录（仓库根）。</summary>
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            if (dir is null)
            {
                throw new InvalidOperationException(
                    $"从 {AppContext.BaseDirectory} 向上未找到 WorldView.sln");
            }

            return dir.FullName;
        }
    }

    /// <summary>示例工程目录（含 project.yaml 的仓库根/examples/sample_world）。</summary>
    public static string SampleWorldDir => Path.Combine(RepoRoot, "examples", "sample_world");

    /// <summary>
    /// 在可写的仓库内（bin/Debug/net8.0/ 下）创建临时目录。
    /// 沙箱环境子进程继承 Low mandatory label 无法写 Temp（Medium），故不用 GetTempPath。
    /// </summary>
    public static string CreateScratchDir(string prefix)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
