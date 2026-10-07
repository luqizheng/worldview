// src/ui/StatusBar.tsx
// 状态栏：面包屑 + 当前地图名 + 比例尺 + 当前鼠标坐标（spec §C2）

import { useDomainStore } from "@/state/useDomainStore";
import { useUiStore } from "@/state/useUiStore";

export function StatusBar() {
  const breadcrumb = useDomainStore((s) => s.breadcrumb);
  const switchToBreadcrumbIndex = useDomainStore((s) => s.switchToBreadcrumbIndex);
  const currentMap = useDomainStore((s) => s.currentMap);
  const viewport = useUiStore((s) => s.viewport);

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        padding: "0 12px",
        height: "100%",
        fontSize: 11,
        color: "#aaa",
      }}
    >
      {/* 面包屑 */}
      <div style={{ display: "flex", gap: 4 }}>
        {breadcrumb.map((item, i) => (
          <span key={`${item.mapPath}-${i}`} style={{ display: "flex", alignItems: "center", gap: 4 }}>
            <button
              onClick={() => switchToBreadcrumbIndex(i)}
              style={{
                background: "transparent",
                border: "none",
                color: "#7aaaff",
                cursor: "pointer",
                fontSize: 11,
                padding: 0,
              }}
            >
              {item.label}
            </button>
            {i < breadcrumb.length - 1 && <span style={{ color: "#666" }}>›</span>}
          </span>
        ))}
      </div>

      <div style={{ flex: 1 }} />

      {/* 当前地图 */}
      <div>{currentMap?.name ?? "(无)"}</div>

      {/* 比例尺 */}
      <div style={{ color: "#888" }}>scale: {viewport.scale.toFixed(2)}</div>

      {/* 鼠标坐标占位（spec §C2：阶段 D 接入） */}
      <div style={{ color: "#888" }}>(-, -)</div>
    </div>
  );
}