namespace WorldView.ProjectManagement;

/// <summary>
/// 文件系统抽象。领域层依赖此接口而非 <c>System.IO</c>, 便于单元测试注入 fake。
/// </summary>
public interface IFileSystem
{
    /// <summary>检查目录是否存在。等价 <c>System.IO.Directory.Exists(string)</c>。</summary>
    bool DirectoryExists(string path);
}
