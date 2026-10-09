using System;

namespace WorldView.ProjectManagement;

/// <summary>
/// 抽象时钟，便于在测试中替换时间流逝。
/// 所有「时间戳」「轮换判定」均依赖此接口，禁止直接调用 <c>DateTime.UtcNow</c>。
/// </summary>
public interface IClock
{
    /// <summary>当前 UTC 时间。</summary>
    DateTime UtcNow { get; }
}
