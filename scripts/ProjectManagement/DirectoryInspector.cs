using System.IO;
using WorldView.ProjectManagement;

namespace WorldView.ProjectManagement;

/// <summary>
/// 工程目录状态检查器（用于 CreateProjectAsync 之前校验目标）。
/// </summary>
public static class DirectoryInspector
{
    /// <summary>
    /// 路径对应的目录是否为空（含隐藏条目）。
    /// 注意：路径不存在时按 "空" 处理 — 调用方负责先创建或校验。
    /// </summary>
    public static bool IsEmpty(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath)) return false;

        if (!Directory.Exists(absolutePath)) return true;

        // EnumerateFileSystemEntries 同时包含文件与目录,也包括 .git 等隐藏项
        foreach (var _ in Directory.EnumerateFileSystemEntries(absolutePath))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 校验 <paramref name="absolutePath"/> 可作为新建工程的根目录。
    /// <para>规则：</para>
    /// <list type="bullet">
    ///   <item>路径是文件而非目录 → <see cref="ProjectException"/> InvalidProject</item>
    ///   <item>目录已存在且包含任何条目（含隐藏文件） → DirectoryNotEmpty</item>
    ///   <item>目录已存在但已含 <c>project.yaml</c> → InvalidProject（已有工程）</item>
    ///   <item>目录不存在或为空 → 通过</item>
    /// </list>
    /// </summary>
    public static void ValidateForCreate(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw ProjectException.Invalid("工程根路径为空字符串。");
        }

        if (File.Exists(absolutePath))
        {
            throw ProjectException.Invalid(
                $"工程根路径已存在同名文件，无法创建工程：{absolutePath}");
        }

        if (Directory.Exists(absolutePath))
        {
            // 先查 yaml：如果目录已有 project.yaml，无论目录是否含有其它东西，都视作"已是工程"。
            var yamlPath = Path.Combine(absolutePath, "project.yaml");
            if (File.Exists(yamlPath))
            {
                throw ProjectException.Invalid(
                    $"目标目录已含 project.yaml，已是现有工程：{absolutePath}");
            }

            if (!IsEmpty(absolutePath))
            {
                throw ProjectException.DirectoryNotEmpty(
                    $"目标目录非空，无法作为新工程根：{absolutePath}");
            }
        }
    }
}
