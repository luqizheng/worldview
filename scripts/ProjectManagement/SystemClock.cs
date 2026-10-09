using System;

namespace WorldView.ProjectManagement;

/// <summary>
/// 生产环境时钟 — 直接返回 <see cref="DateTime.UtcNow"/>。
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
