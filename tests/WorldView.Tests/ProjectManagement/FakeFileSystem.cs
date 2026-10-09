using System;
using System.Collections.Generic;
using WorldView.ProjectManagement;

namespace WorldView.Tests.ProjectManagement;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册"存在"路径, 多次调用累积。返回 this 便于链式。</summary>
    public FakeFileSystem Exists(params string[] paths)
    {
        foreach (var p in paths) _existing.Add(p);
        return this;
    }

    public bool DirectoryExists(string path) => _existing.Contains(path);
}
