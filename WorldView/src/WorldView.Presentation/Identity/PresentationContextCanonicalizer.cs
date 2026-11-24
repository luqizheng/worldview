using System;
using System.Collections.Generic;
using System.Text;
using WorldView.Domain.Time;

namespace WorldView.Presentation.Identity;

/// <summary>
/// PresentationContext 的版本化 canonical 序列化与指纹计算（PRA-01 契约 §2.4）。
///
/// 编码规则（schema <see cref="SchemaVersion"/>，字段顺序固定，逐字节确定）：
///   [int32 LE]  序列化 schema 版本
///   [16 字节]   MapId（Guid.ToByteArray() 的固定 16 字节顺序）
///   [1 字节]    BranchId 存在标志（0x00 = 根历史/缺省；0x01 = 显式 Branch）
///   [16 字节]   BranchId（仅当标志为 0x01）
///   [16 字节]   RootHistoryId
///   [int64 LE]  HistoricalTime.DayNumber
///   [int32 LE]  HistoricalTime.OffsetSeconds
///   [1 字节]    WorldStateRevision 类别（0x01 = NotEvaluated；0x02 = Evaluated）
///   [string]    Evaluated.RevisionIdentity（仅当类别为 0x02）
///   [1 字节]    Projection 类别（0x01 = NotConfigured；0x02 = Configured）
///   [string]    Configured.DefinitionId（仅当类别为 0x02）
///   [string]    Configured.DefinitionVersion（仅当类别为 0x02）
///   [string]    StyleToken
///   [string]    FontToken
///   [int64 LE]  ViewportGeneration
///   [int32 LE]  SceneAssemblerVersion
///
/// string 编码 = [int32 LE 字节长度] + UTF-8 原始字节（不区分大小写折叠、
/// 不做文化相关规范化；身份比较按精确字节序列）。所有整数固定小端、
/// 定长编码；不依赖 Dictionary 遍历顺序；不含时间戳/随机数/进程信息。
/// NotEvaluated / NotConfigured 与任何 Evaluated / Configured 值凭借
/// 类别字节与负载差异可区分。
/// </summary>
internal static class PresentationContextCanonicalizer
{
    /// <summary>canonical 序列化 schema 版本；编码规则变化时递增。</summary>
    public const int SchemaVersion = 1;

    private const byte FlagAbsent = 0x00;
    private const byte FlagPresent = 0x01;
    private const byte WorldStateNotEvaluated = 0x01;
    private const byte WorldStateEvaluated = 0x02;
    private const byte ProjectionNotConfigured = 0x01;
    private const byte ProjectionConfigured = 0x02;

    public static ContextFingerprint FingerprintOf(PresentationContext context)
        => ContextFingerprint.Compute(Serialize(context));

    public static byte[] Serialize(PresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var buffer = new List<byte>(capacity: 160);
        WriteInt32LE(buffer, SchemaVersion);
        WriteGuid(buffer, context.MapId);

        if (context.BranchId is { } branchId)
        {
            buffer.Add(FlagPresent);
            WriteGuid(buffer, branchId);
        }
        else
        {
            buffer.Add(FlagAbsent);
        }

        WriteGuid(buffer, context.RootHistoryId);
        WriteInt64LE(buffer, context.HistoricalTime.DayNumber);
        WriteInt32LE(buffer, context.HistoricalTime.OffsetSeconds);

        switch (context.WorldStateRevision)
        {
            case WorldStateRevision.NotEvaluated:
                buffer.Add(WorldStateNotEvaluated);
                break;
            case WorldStateRevision.Evaluated evaluated:
                buffer.Add(WorldStateEvaluated);
                WriteString(buffer, evaluated.RevisionIdentity);
                break;
            default:
                throw new InvalidOperationException($"未知的 WorldStateRevision 子类型：{context.WorldStateRevision.GetType().FullName}。");
        }

        switch (context.Projection)
        {
            case ProjectionDefinition.NotConfigured:
                buffer.Add(ProjectionNotConfigured);
                break;
            case ProjectionDefinition.Configured configured:
                buffer.Add(ProjectionConfigured);
                WriteString(buffer, configured.DefinitionId);
                WriteString(buffer, configured.DefinitionVersion);
                break;
            default:
                throw new InvalidOperationException($"未知的 ProjectionDefinition 子类型：{context.Projection.GetType().FullName}。");
        }

        WriteString(buffer, context.StyleToken);
        WriteString(buffer, context.FontToken);
        WriteInt64LE(buffer, context.ViewportGeneration);
        WriteInt32LE(buffer, context.SceneAssemblerVersion);
        return buffer.ToArray();
    }

    /// <summary>Guid 以 <see cref="Guid.ToByteArray"/> 的固定 16 字节顺序编码（值跨平台稳定）。</summary>
    private static void WriteGuid(List<byte> buffer, Guid value)
        => buffer.AddRange(value.ToByteArray());

    private static void WriteInt32LE(List<byte> buffer, int value)
    {
        var unsigned = (uint)value;
        buffer.Add((byte)(unsigned & 0xFF));
        buffer.Add((byte)((unsigned >> 8) & 0xFF));
        buffer.Add((byte)((unsigned >> 16) & 0xFF));
        buffer.Add((byte)((unsigned >> 24) & 0xFF));
    }

    private static void WriteInt64LE(List<byte> buffer, long value)
    {
        var unsigned = (ulong)value;
        for (var shift = 0; shift < 64; shift += 8)
        {
            buffer.Add((byte)((unsigned >> shift) & 0xFF));
        }
    }

    /// <summary>UTF-8 原始字节 + int32 LE 长度前缀（null 与空串、默认值可区分：本上下文的必填串均非空白）。</summary>
    private static void WriteString(List<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32LE(buffer, bytes.Length);
        buffer.AddRange(bytes);
    }
}
