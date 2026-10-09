using System;
using System.IO;
using WorldView.Storage.Yaml;

namespace WorldView.ProjectManagement;

/// <summary>
/// 写协调器：把单文件写入与多文件工程保存统一为"先写暂存，验证后再原子替换"的语义。
/// <list type="number">
///   <item>单文件：同目录临时文件 → flush(true) → 重读校验字节一致 → 原子替换或移动 → 失败清理 tmp。</item>
///   <item>多文件：建 <c>&lt;root&gt;.tmp-&lt;guid&gt;/</c> 暂存工程 → 调 writer → ProjectLoader 校验 → Directory.Move 同卷替换 → 失败回滚（清理 staging）。</item>
///   <item>所有失败统一抛 <see cref="ProjectException"/>，错误码 <see cref="ProjectErrorCode.SaveFailed"/>。</item>
/// </list>
/// </summary>
public sealed class ProjectWriteCoordinator
{
    /// <summary>
    /// 同目录临时文件名（前缀点号，避免目录列表里被误扫）。
    /// </summary>
    private const string TmpPrefix = ".tmp-";

    /// <summary>
    /// 工程暂存后缀。
    /// </summary>
    private const string StagingSuffix = ".tmp-";

    public ProjectWriteCoordinator()
    {
    }

    /// <summary>
    /// 把 <paramref name="content"/> 原子写入 <paramref name="absolutePath"/>。
    /// <list type="bullet">
    ///   <item>目标不存在时走 <see cref="File.Move(string,string)"/>，存在时走 <see cref="File.Replace(string,string,string)"/>。</item>
    ///   <item>写完后重读校验字节一致，避免磁盘缓存或编码问题。</item>
    ///   <item>任何异常都转 <see cref="ProjectException(SaveFailed)"/> 并清理临时文件。</item>
    /// </list>
    /// </summary>
    public void WriteFileAtomic(string absolutePath, string content)
    {
        if (string.IsNullOrEmpty(absolutePath))
            throw new ArgumentException("absolutePath 不可为空", nameof(absolutePath));
        content ??= string.Empty;

        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = Path.Combine(
            string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : dir!,
            $"{TmpPrefix}{Guid.NewGuid():N}.tmp");

        try
        {
            // 写 + flush（FileStream.Flush(true) 强制刷新到底层存储）
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, new System.Text.UTF8Encoding(false)))
            {
                sw.Write(content);
                sw.Flush();
                fs.Flush(flushToDisk: true);
            }

            // 重读校验字节一致
            var writtenBytes = File.ReadAllBytes(tmp);
            var expectedBytes = new System.Text.UTF8Encoding(false).GetBytes(content);
            if (writtenBytes.Length != expectedBytes.Length)
            {
                throw new IOException(
                    $"写入字节长度校验失败：预期 {expectedBytes.Length}，实际 {writtenBytes.Length}");
            }
            for (int i = 0; i < writtenBytes.Length; i++)
            {
                if (writtenBytes[i] != expectedBytes[i])
                {
                    throw new IOException(
                        $"写入字节内容校验失败：偏移 {i} 预期 0x{expectedBytes[i]:X2}，实际 0x{writtenBytes[i]:X2}");
                }
            }

            // 原子替换或移动
            if (File.Exists(absolutePath))
            {
                File.Replace(tmp, absolutePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, absolutePath);
            }
        }
        catch (Exception ex) when (ex is not ProjectException)
        {
            TryDelete(tmp);
            throw ProjectException.SaveFailed(
                $"原子写入失败：{absolutePath}", ex);
        }
    }

    /// <summary>
    /// 把整个工程目录"先暂存再原子替换"：在 <paramref name="rootPath"/> 同目录建
    /// <c>&lt;rootPath&gt;.tmp-&lt;guid&gt;/</c> 暂存工程 → 调 <paramref name="writeAll"/> 让其写入暂存工程 →
    /// 用 <see cref="ProjectLoader"/> 校验 → 同卷 <see cref="Directory.Move(string,string)"/>。
    /// </summary>
    /// <remarks>
    /// 若 <paramref name="rootPath"/> 已经存在，则 <see cref="Directory.Move"/> 会失败；
    /// 此类场景下，应当先由调用方删除旧目录（或先调用 <see cref="BackupService"/> 创建保护快照后删除旧目录，
    /// 然后由本方法重建）。MVP 不在此处自动快照（保护快照属 Task 8 RecoveryService 范畴）。
    /// </remarks>
    public void WriteProjectAtomic(string rootPath, Action<string> writeAll)
    {
        if (string.IsNullOrEmpty(rootPath))
            throw new ArgumentException("rootPath 不可为空", nameof(rootPath));
        if (writeAll is null) throw new ArgumentNullException(nameof(writeAll));

        var parentDir = Path.GetDirectoryName(rootPath);
        if (string.IsNullOrEmpty(parentDir)) parentDir = Directory.GetCurrentDirectory();

        var stagingRoot = Path.Combine(
            parentDir!,
            $"{Path.GetFileName(rootPath)}{StagingSuffix}{Guid.NewGuid():N}");

        try
        {
            // 1. 建暂存工程
            Directory.CreateDirectory(stagingRoot);

            // 2. 让调用方把内容写进暂存工程
            try
            {
                writeAll(stagingRoot);
            }
            catch (Exception writerEx)
            {
                TryDelete(stagingRoot);
                throw ProjectException.SaveFailed(
                    $"工程写入回调失败：{rootPath}", writerEx);
            }

            // 3. 校验暂存工程
            try
            {
                var loadResult = ProjectLoader.Load(stagingRoot);
                if (!loadResult.Validation.IsValid)
                {
                    var firstError = loadResult.Validation.Errors[0];
                    var msg = $"暂存工程校验失败：{firstError.Code} - {firstError.Message}（{firstError.Path}）";
                    TryDelete(stagingRoot);
                    throw ProjectException.SaveFailed(msg, inner: null);
                }
            }
            catch (ProjectException)
            {
                throw;
            }
            catch (Exception loadEx)
            {
                TryDelete(stagingRoot);
                throw ProjectException.SaveFailed(
                    $"暂存工程加载失败：{stagingRoot}", loadEx);
            }

            // 4. 替换：先清掉目标，再 Move
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
            Directory.Move(stagingRoot, rootPath);
        }
        catch (ProjectException)
        {
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(stagingRoot);
            throw ProjectException.SaveFailed(
                $"工程原子写入失败：{rootPath}", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 静默：清理失败不影响主流程的错误传播。
        }
    }
}
