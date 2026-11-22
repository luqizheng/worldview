using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WorldView.Application.Abstractions;

namespace WorldView.Infrastructure.Logging;

/// <summary>
/// 文件 + Debug 输出的极简日志实现。日志按日滚动写入应用元数据目录
/// （默认 %LOCALAPPDATA%/WorldView/logs/app-yyyyMMdd.log）。写入失败静默忽略。
/// 详细异常/SQL 数据只进日志，不进 UI（AF-01 §9）。
/// </summary>
public sealed class FileAppLog : IAppLog
{
    private readonly string _logDirectory;
    private readonly object _gate = new();

    public FileAppLog(string? baseDirectory = null)
    {
        var root = baseDirectory
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldView")
                : OperatingSystem.IsMacOS()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "WorldView")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".worldview"));
        _logDirectory = Path.Combine(root, "logs");
    }

    public void Info(string category, string message) => Write("INFO", category, message, null);

    public void Warn(string category, string message, Exception? exception = null) => Write("WARN", category, message, exception);

    public void Error(string category, string message, Exception? exception = null) => Write("ERROR", category, message, exception);

    private void Write(string level, string category, string message, Exception? exception)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} [{level}] [{category}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception.ToString();
            }

            Debug.WriteLine(line);

            lock (_gate)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(
                    Path.Combine(_logDirectory, $"app-{DateTimeOffset.UtcNow:yyyyMMdd}.log"),
                    line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // 日志写入失败绝不影响主流程。
        }
    }
}
