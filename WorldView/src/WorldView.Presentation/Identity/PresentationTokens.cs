namespace WorldView.Presentation.Identity;

/// <summary>
/// M1 内置呈现基线 token（M1 Profile §3.4）。这些是显式命名的、带 schema 版本的
/// 呈现算法/配置身份——不是 Domain revision，不暗示存在正式样式/字体子系统。
/// 中性样式或字体行为发生影响输出的变化时，必须递增对应 token 版本。
/// </summary>
public static class PresentationTokens
{
    /// <summary>M1 中性样式基线。</summary>
    public const string BuiltinNeutralStyle = "BuiltinNeutralStyle/v1";

    /// <summary>M1 基线字体配置。</summary>
    public const string BuiltinFontBaseline = "BuiltinFontBaseline/v1";
}
