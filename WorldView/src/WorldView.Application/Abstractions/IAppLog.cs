namespace WorldView.Application.Abstractions;

/// <summary>
/// 极简应用日志边界：进程级异常处理与应用生命周期诊断写入。
/// 详细异常与 SQL 内容只进日志，不进入用户界面（AF-01 §9）。
/// </summary>
public interface IAppLog
{
    void Info(string category, string message);

    void Warn(string category, string message, System.Exception? exception = null);

    void Error(string category, string message, System.Exception? exception = null);
}
