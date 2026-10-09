using System.IO;

namespace WorldView.ProjectManagement;

/// <summary>走真实文件系统的 <see cref="IFileSystem"/> 实现。生产环境默认。</summary>
public sealed class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
