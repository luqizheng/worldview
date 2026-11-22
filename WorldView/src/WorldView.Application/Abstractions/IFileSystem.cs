using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// 文件系统抽象：用例层（打包、校验、会话生命周期）通过此接口访问文件系统，
/// 使用例测试不依赖真实文件系统（AGENTS F）。Infrastructure 提供 System.IO 实现。
/// 所有路径均为绝对路径；路径拼接使用 <see cref="System.IO.Path"/>（纯函数，无需抽象）。
/// </summary>
public interface IFileSystem
{
    /// <summary>目录是否存在。</summary>
    bool DirectoryExists(string path);

    /// <summary>文件是否存在。</summary>
    bool FileExists(string path);

    /// <summary>创建目录（含中间层级）。已存在时为空操作。</summary>
    void CreateDirectory(string path);

    /// <summary>读取文本文件（UTF-8）。</summary>
    string ReadAllText(string path);

    /// <summary>写入文本文件（UTF-8）。覆盖已存在文件。</summary>
    void WriteAllText(string path, string content);

    /// <summary>
    /// 同卷目录重命名/移动。目标必须不存在；失败抛出异常，由调用方翻译为结果对象。
    /// </summary>
    void MoveDirectory(string sourcePath, string destinationPath);

    /// <summary>删除目录（递归）。目录不存在时为空操作。</summary>
    void DeleteDirectory(string path, CancellationToken cancellationToken = default);
}
