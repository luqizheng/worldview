#if DEBUG
using System;
using System.Threading;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// WP-30B Phase 3A 人工验收专用：一次性关闭故障注入开关（<b>仅 Debug 编译存在</b>）。
///
/// 目的：在真实桌面窗口上验证"关闭失败 → 窗口保留 → 再次点击 X 重试成功"的
/// 完整 UI 路径。故障在 Binding 关闭子序的 Publisher 阶段调用缝
/// （<see cref="MapViewportBinding.DisposePublisherStep"/>）直观抛出，经过真实
/// C2 编排、ProjectSession / ProjectWindow 生命周期，不是测试替身路径。
///
/// 约束：
/// - 一次性：成功消费后即复位；未武装时零影响（无故障、无日志、无状态变化）；
/// - 确定性：无 Sleep、无随机、无时间竞争——仅由用户主动武装 + 首次关闭触发；
/// - Release 隔离：整个类型（含状态、入口、异常类型）被 #if DEBUG 包围，
///   Release 程序集中不存在任何注入入口、状态或分支。
/// </summary>
internal static class DebugCloseFault
{
    /// <summary>注入阶段标识（与 CloseStageLog 的 publisher 阶段对应）。</summary>
    internal const string FaultStage = "publisher_dispose";

    /// <summary>仅 Debug 标题栏可见的武装提示文本。</summary>
    internal const string ArmedTitleSuffix = "  [DEBUG 故障已武装：首次关闭将失败]";

    private static int _armed; // 0 = 未武装；1 = 已武装（Interlocked 原子访问）

    internal static bool IsArmed => Volatile.Read(ref _armed) == 1;

    /// <summary>用户主动武装一次性故障（重复调用仍是"一次"，不累积）。</summary>
    internal static void ArmOnce() => Volatile.Write(ref _armed, 1);

    /// <summary>清除武装（测试 teardown / 用户取消用）。</summary>
    internal static void Disarm() => Volatile.Write(ref _armed, 0);

    /// <summary>
    /// 在指定阶段尝试消费一次性故障：仅当 <paramref name="stage"/> 是注入阶段
    /// 且当前已武装时返回可识别异常，同时复位武装；其余情况一律返回 null。
    /// </summary>
    internal static Exception? Consume(string stage)
    {
        if (stage != FaultStage || !IsArmed)
        {
            return null;
        }

        // 竞争下也只放出一个故障：CAS 成功者获得异常，其余调用者直通。
        if (Interlocked.CompareExchange(ref _armed, 0, 1) != 1)
        {
            return null;
        }

        return new DebugCloseInjectionException(stage);
    }
}

/// <summary>可识别的注入异常（消息含 [DEBUG-FAULT]，便于日志与状态栏定位）。</summary>
internal sealed class DebugCloseInjectionException : InvalidOperationException
{
    internal DebugCloseInjectionException(string stage)
        : base($"[DEBUG-FAULT] 一次性关闭故障注入（阶段 {stage}）；这是人工验收注入，非真实资源故障。")
    {
    }
}
#endif
