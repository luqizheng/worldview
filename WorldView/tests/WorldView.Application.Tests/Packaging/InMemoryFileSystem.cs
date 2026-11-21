using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldView.Application.Abstractions;

namespace WorldView.Application.Tests;

/// <summary>
/// 内存文件系统测试替身（AGENTS F：用例测试不依赖真实文件系统）。
/// 路径规范化为全路径 + 统一分隔符，大小写按当前平台语义处理。
/// </summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private sealed record Node(bool IsDirectory, string? Content = null);

    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public InMemoryFileSystem(params string[] initialDirectories)
    {
        foreach (var dir in initialDirectories)
        {
            CreateDirectory(dir);
        }
    }

    private static string Normalize(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        return full.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar);
    }

    public bool DirectoryExists(string path)
    {
        lock (_gate)
        {
            return _nodes.TryGetValue(Normalize(path), out var node) && node.IsDirectory;
        }
    }

    public bool FileExists(string path)
    {
        lock (_gate)
        {
            return _nodes.TryGetValue(Normalize(path), out var node) && !node.IsDirectory;
        }
    }

    public void CreateDirectory(string path)
    {
        var normalized = Normalize(path);
        lock (_gate)
        {
            var segments = normalized.Split(System.IO.Path.DirectorySeparatorChar);
            var current = string.Empty;
            foreach (var segment in segments)
            {
                current = current.Length == 0 ? segment : current + System.IO.Path.DirectorySeparatorChar + segment;
                if (!_nodes.TryGetValue(current, out var node))
                {
                    _nodes[current] = new Node(IsDirectory: true);
                }
                else if (!node.IsDirectory)
                {
                    throw new IOException($"路径已是文件：{current}");
                }
            }
        }
    }

    public string ReadAllText(string path)
    {
        lock (_gate)
        {
            var key = Normalize(path);
            if (!_nodes.TryGetValue(key, out var node) || node.IsDirectory)
            {
                throw new FileNotFoundException("文件不存在。", key);
            }

            return node.Content!;
        }
    }

    public void WriteAllText(string path, string content)
    {
        var key = Normalize(path);
        CreateDirectory(System.IO.Path.GetDirectoryName(key)!);
        lock (_gate)
        {
            _nodes[key] = new Node(IsDirectory: false, Content: content);
        }
    }

    public void MoveDirectory(string sourcePath, string destinationPath)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        lock (_gate)
        {
            if (!_nodes.TryGetValue(source, out var node) || !node.IsDirectory)
            {
                throw new DirectoryNotFoundException($"目录不存在：{source}");
            }

            if (_nodes.ContainsKey(destination))
            {
                throw new IOException($"目标已存在：{destination}");
            }

            var moved = new List<KeyValuePair<string, Node>>();
            foreach (var (key, value) in _nodes)
            {
                if (key == source || key.StartsWith(source + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    moved.Add(new(key, value));
                }
            }

            foreach (var (key, value) in moved)
            {
                _nodes.Remove(key);
                _nodes[destination + key[source.Length..]] = value;
            }
        }
    }

    public void DeleteDirectory(string path, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(path);
        lock (_gate)
        {
            var keys = _nodes.Keys
                .Where(k => k == normalized || k.StartsWith(normalized + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .ToList();
            foreach (var key in keys)
            {
                _nodes.Remove(key);
            }
        }
    }

    /// <summary>测试辅助：列出全部节点路径。</summary>
    public IReadOnlyList<string> SnapshotPaths()
    {
        lock (_gate)
        {
            return _nodes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }
    }
}
