using System;
using System.IO;

namespace WorldView.Tests.ProjectManagement;

/// <summary>
/// 沙箱友好的临时路径：xUnit 子进程在 %TEMP% 创建目录会被拒绝，
/// 但仓库 workspace 内子目录可以（被 .gitignore 忽略）。
/// </summary>
internal static class ProjectManagementTemp
{
    public static string Root => Path.Combine(
        Directory.GetCurrentDirectory(),
        ".tmp-pmtests");

    public static string NewDir(string label)
    {
        Directory.CreateDirectory(Root);
        var dir = Path.Combine(Root, $"{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Cleanup()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch { /* swallow */ }
    }
}
