using System;
using System.IO;

namespace WorldView.ProjectManagement;

/// <summary>
/// 生产环境的 <see cref="IAppDataLocator"/>。
///
/// 解析规则：
/// <list type="number">
///   <item>如果构造时提供了 <paramref name="overrideUserConfigDir"/>，直接返回（绝对路径）。</item>
///   <item>否则尝试通过反射调用 <c>Godot.ProjectSettings.GlobalizePath("user://")</c>，
///         仅当 <c>Godot.ProjectSettings</c> 类型存在时才执行 — 避免对 Godot 的静态依赖。</item>
///   <item>类型不存在或调用抛异常时，回落到
///         <c>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldView")</c>。</item>
/// </list>
///
/// 返回值若是首次创建，会保证目录存在（<see cref="Directory.CreateDirectory"/>）。
/// </summary>
public sealed class SystemAppDataLocator : IAppDataLocator
{
    private readonly string? _override;

    public SystemAppDataLocator(string? overrideUserConfigDir = null)
    {
        _override = overrideUserConfigDir;
    }

    public string GetUserConfigDir()
    {
        if (!string.IsNullOrWhiteSpace(_override))
        {
            Directory.CreateDirectory(_override);
            return _override;
        }

        var godotResolved = TryResolveFromGodot();
        if (!string.IsNullOrWhiteSpace(godotResolved))
        {
            Directory.CreateDirectory(godotResolved);
            return godotResolved;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorldView");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string? TryResolveFromGodot()
    {
        // 仅在 Godot 程序集中存在 Godot.ProjectSettings 类型时才走反射调用。
        // 域层不要 `using Godot;`，改用 Type.GetType 严格按完全限定名查找。
        try
        {
            var t = Type.GetType("Godot.ProjectSettings, GodotSharp");
            if (t is null) return null;

            var method = t.GetMethod("GlobalizePath", new[] { typeof(string) });
            if (method is null) return null;

            var result = method.Invoke(null, new object[] { "user://" });
            if (result is string s && !string.IsNullOrWhiteSpace(s)) return s;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
