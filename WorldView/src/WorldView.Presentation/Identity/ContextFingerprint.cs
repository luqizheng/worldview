using System;
using System.Globalization;
using System.Security.Cryptography;

namespace WorldView.Presentation.Identity;

/// <summary>
/// 上下文指纹：对版本化 canonical 序列化字节计算 SHA-256 的确定性摘要
/// （PRA-01 契约 §2.4 / M1 Profile §3.6）。值语义：私有持有 32 字节副本，
/// 按字节比较；不含进程 ID、线程 ID、时间戳、随机数或内存地址。
/// </summary>
public readonly struct ContextFingerprint : IEquatable<ContextFingerprint>
{
    /// <summary>SHA-256 摘要长度（字节）。</summary>
    public const int ByteLength = 32;

    private readonly byte[]? _bytes;

    private ContextFingerprint(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>对 canonical 序列化数据计算指纹。</summary>
    public static ContextFingerprint Compute(ReadOnlySpan<byte> canonicalData)
        => new(SHA256.HashData(canonicalData));

    /// <summary>从 64 字符小写十六进制文本重建（诊断/测试用；非权威生成路径）。</summary>
    public static bool TryFromHex(string hex, out ContextFingerprint fingerprint)
    {
        fingerprint = default;
        if (string.IsNullOrEmpty(hex) || hex.Length != ByteLength * 2)
        {
            return false;
        }

        var buffer = new byte[ByteLength];
        for (var i = 0; i < ByteLength; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            buffer[i] = value;
        }

        fingerprint = new ContextFingerprint(buffer);
        return true;
    }

    /// <summary>64 字符小写十六进制（不变文化，跨平台稳定）。</summary>
    public string ToHex()
        => Convert.ToHexString(GetBytesOrThrow()).ToLowerInvariant();

    /// <summary>摘要字节的私有副本。</summary>
    public byte[] ToByteArray()
        => (byte[])GetBytesOrThrow().Clone();

    private byte[] GetBytesOrThrow()
        => _bytes ?? throw new InvalidOperationException("指纹为 default(ContextFingerprint)，未经过 Compute 构造。");

    public bool Equals(ContextFingerprint other)
    {
        if (_bytes is null || other._bytes is null)
        {
            return _bytes is null && other._bytes is null;
        }

        return _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    public override bool Equals(object? obj) => obj is ContextFingerprint other && Equals(other);

    public override int GetHashCode()
    {
        var bytes = _bytes;
        if (bytes is null || bytes.Length < 16)
        {
            return 0;
        }

        return HashCode.Combine(
            BitConverter.ToInt64(bytes, 0),
            BitConverter.ToInt64(bytes, 8),
            BitConverter.ToInt64(bytes, 16),
            BitConverter.ToInt64(bytes, 24));
    }

    public override string ToString() => _bytes is null ? "default" : ToHex();

    public static bool operator ==(ContextFingerprint left, ContextFingerprint right) => left.Equals(right);

    public static bool operator !=(ContextFingerprint left, ContextFingerprint right) => !left.Equals(right);
}
