// src/ui/LayerPanel.tsx
// 右图层面板（spec §D1）：列出当前地图 layers，可切换 visible / opacity / z_order / locked。

import { useDomainStore } from "@/state/useDomainStore";
import type { Layer } from "@/types";

// MVP 阶段：图层数据从 domain store 的 selectedObjectId 暂取不到（无 Plot Map）。
// 这里直接读 domain.project 的 MapSummary；MVP 简化：先用 mock 数据。
// 真实实现会在 DomainStore 持有 LoadedMap（含 layers 数组）；MVP 暂走最简 path。

export function LayerPanel() {
  const project = useDomainStore((s) => s.project);
  const currentMap = useDomainStore((s) => s.currentMap);

  if (!project || !currentMap) {
    return (
      <div style={{ padding: 12, color: "#666", fontSize: 12 }}>
        <div style={{ fontWeight: 600, color: "#aaa", marginBottom: 8 }}>图层</div>
        <div>未加载地图</div>
      </div>
    );
  }

  // MVP 简化：用 bridge 拉一次完整 Map（含 layers）—— 这里用 mock
  const layers: Layer[] = [
    { id: "layer_terrain", name: "基础地形", type: "terrain", visible: true, locked: false, opacity: 1.0, z_order: 0 },
    { id: "layer_road", name: "道路", type: "road", visible: true, locked: false, opacity: 1.0, z_order: 10 },
  ];

  return (
    <div style={{ padding: 12, borderBottom: "1px solid #333" }}>
      <div style={{ fontWeight: 600, color: "#aaa", fontSize: 13, marginBottom: 8 }}>图层</div>
      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {layers.map((layer) => (
          <div
            key={layer.id}
            style={{
              padding: "6px 8px",
              background: "#2a2a2a",
              border: "1px solid #444",
              borderRadius: 3,
              fontSize: 12,
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <input type="checkbox" defaultChecked={layer.visible} title="可见" />
              <input type="checkbox" defaultChecked={layer.locked} title="锁定" />
              <div style={{ flex: 1 }}>{layer.name}</div>
              <div style={{ color: "#888", fontSize: 11 }}>{layer.type}</div>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 4 }}>
              <span style={{ color: "#888", fontSize: 11 }}>opacity</span>
              <input type="range" min={0} max={1} step={0.05} defaultValue={layer.opacity} style={{ flex: 1 }} />
              <span style={{ color: "#888", fontSize: 11, width: 28 }}>{layer.opacity.toFixed(2)}</span>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 2 }}>
              <span style={{ color: "#888", fontSize: 11 }}>z_order</span>
              <input type="number" defaultValue={layer.z_order} style={{ width: 60 }} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}