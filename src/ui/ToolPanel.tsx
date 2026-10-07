// src/ui/ToolPanel.tsx
// 左工具栏（spec §C1）：pan / brush / placeBuilding

import { useUiStore } from "@/state/useUiStore";
import { useDomainStore } from "@/state/useDomainStore";
import type { ToolId } from "@/state/useDomainStore";
import { BUILDING_TYPES } from "@/ui/BuildingPlaceDialog";

const TOOLS: Array<{ id: ToolId; label: string; hint: string }> = [
  { id: "pan", label: "✋", hint: "平移 (Pan)" },
  { id: "brush", label: "●", hint: "笔刷 (Brush)" },
  { id: "placeBuilding", label: "▭", hint: "放置建筑 (Place)" },
];

export function ToolPanel() {
  const activeTool = useUiStore((s) => s.activeTool);
  const setTool = useUiStore((s) => s.setTool);
  const brushParams = useUiStore((s) => s.brushParams);
  const setBrushParams = useUiStore((s) => s.setBrushParams);
  const markDirty = useDomainStore((s) => s.markDirty);

  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "stretch", padding: 8, gap: 6 }}>
      {TOOLS.map((t) => {
        const active = activeTool === t.id;
        return (
          <button
            key={t.id}
            onClick={() => setTool(t.id)}
            title={t.hint}
            style={{
              width: 40,
              height: 40,
              fontSize: 18,
              background: active ? "#5a6a8a" : "#2a2a2a",
              color: active ? "#fff" : "#aaa",
              border: "1px solid " + (active ? "#7a8aaa" : "#444"),
              borderRadius: 4,
              cursor: "pointer",
            }}
          >
            {t.label}
          </button>
        );
      })}

      <div style={{ height: 1, background: "#333", margin: "6px 0" }} />

      {/* 笔刷参数（activeTool === "brush" 时显示） */}
      {activeTool === "brush" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 11, color: "#aaa" }}>
          <div style={{ fontWeight: 600, color: "#bbb", marginBottom: 2 }}>笔刷</div>
          <label>size <input
            type="number" min={1} max={16}
            value={brushParams.size}
            onChange={(e) => setBrushParams({ size: Math.max(1, Math.min(16, Number(e.target.value) || 1)) })}
            style={{ width: 50, marginLeft: 4 }}
          /></label>
          <label>terrain
            <select
              value={brushParams.terrainType}
              onChange={(e) => setBrushParams({ terrainType: e.target.value })}
              style={{ width: "100%", marginTop: 2 }}
            >
              {["plain","mountain","forest","hill","water","desert","swamp","tundra","basin","coast","river"]
                .map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </label>
        </div>
      )}

      {/* 建筑类型（activeTool === "placeBuilding" 时显示） */}
      {activeTool === "placeBuilding" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 11, color: "#aaa" }}>
          <div style={{ fontWeight: 600, color: "#bbb", marginBottom: 2 }}>建筑</div>
          {BUILDING_TYPES.map((bt) => (
            <button
              key={bt}
              onClick={() => {
                // 把建筑类型写到一个本地存储或 UI store；MVP 简化：写到 window 全局
                (window as unknown as { __wv_buildingType?: string }).__wv_buildingType = bt;
                markDirty("(tool-panel-selection)"); // 占位：触发 dirty 但不污染实际 map
              }}
              style={{
                padding: "4px 6px",
                background: (window as unknown as { __wv_buildingType?: string }).__wv_buildingType === bt ? "#5a6a8a" : "#2a2a2a",
                color: "#fff",
                border: "1px solid #444",
                borderRadius: 3,
                cursor: "pointer",
                fontSize: 11,
              }}
            >
              {bt}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}