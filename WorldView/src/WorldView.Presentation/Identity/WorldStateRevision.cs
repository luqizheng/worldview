using System;

namespace WorldView.Presentation.Identity;

/// <summary>
/// typed World State 评估状态（M1 Profile §3.2）。当前产品没有权威的 World State
/// 重建 read model，因此 M1 一律使用显式 <see cref="NotEvaluated"/> 语义态：
/// 不是 null、空串、整数零或伪 revision；canonical 序列化与指纹可将其与任何
/// <see cref="Evaluated"/> 值区分。未来真实 revision 只能来自权威 Core Domain/Application
/// 读路径，Presentation 不得成为其权威。
/// </summary>
public abstract record WorldStateRevision
{
    private WorldStateRevision()
    {
    }

    /// <summary>本次呈现请求未评估权威 World State revision（M1 唯一合法状态）。</summary>
    public sealed record NotEvaluated : WorldStateRevision
    {
        public static readonly NotEvaluated Instance = new();

        private NotEvaluated()
        {
        }

        public override string ToString() => "NotEvaluated";
    }

    /// <summary>
    /// 预留给未来权威 read model 提供的真实稳定 revision 身份。
    /// M1 不得构造本状态（任何非空身份即与 NotEvaluated 在序列化上可区分）。
    /// </summary>
    public sealed record Evaluated : WorldStateRevision
    {
        /// <summary>权威 revision 身份（非空白；语义由提供方契约拥有）。</summary>
        public string RevisionIdentity { get; }

        public Evaluated(string revisionIdentity)
        {
            if (string.IsNullOrWhiteSpace(revisionIdentity))
            {
                throw new ArgumentException("Evaluated revision 身份不能为空白。", nameof(revisionIdentity));
            }

            RevisionIdentity = revisionIdentity;
        }

        public override string ToString() => $"Evaluated({RevisionIdentity})";
    }
}
