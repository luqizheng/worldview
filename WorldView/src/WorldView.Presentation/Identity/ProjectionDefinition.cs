using System;

namespace WorldView.Presentation.Identity;

/// <summary>
/// typed 投影配置状态（M1 Profile §3.3）。M1 没有配置的 ProjectionDefinition 权威，
/// <c>MapRecord.SpatialReferenceId</c> 可为 null，因此一律使用显式
/// <see cref="NotConfigured"/>：不猜测 EPSG/CRS/地理坐标系。M1 的 Pan/Zoom 属局部
/// 二维视口相机变换，不是地理投影；未来真实配置只能来自权威投影契约读路径。
/// </summary>
public abstract record ProjectionDefinition
{
    private ProjectionDefinition()
    {
    }

    /// <summary>未配置权威投影定义（M1 唯一合法状态）。</summary>
    public sealed record NotConfigured : ProjectionDefinition
    {
        public static readonly NotConfigured Instance = new();

        private NotConfigured()
        {
        }

        public override string ToString() => "NotConfigured";
    }

    /// <summary>
    /// 预留给未来权威投影定义（定义 ID + 版本）。任何配置值与 NotConfigured
    /// 在 canonical 序列化与指纹上可区分。
    /// </summary>
    public sealed record Configured : ProjectionDefinition
    {
        /// <summary>权威投影定义 ID（非空白）。</summary>
        public string DefinitionId { get; }

        /// <summary>权威投影定义版本（非空白；变换语义变化时随之变化）。</summary>
        public string DefinitionVersion { get; }

        public Configured(string definitionId, string definitionVersion)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("投影定义 ID 不能为空白。", nameof(definitionId));
            }

            if (string.IsNullOrWhiteSpace(definitionVersion))
            {
                throw new ArgumentException("投影定义版本不能为空白。", nameof(definitionVersion));
            }

            DefinitionId = definitionId;
            DefinitionVersion = definitionVersion;
        }

        public override string ToString() => $"Configured({DefinitionId}/{DefinitionVersion})";
    }
}
