// src/ui/ToolPanel.tsx
// 左工具栏（spec §C1）：pan / brush / placeBuilding

import { useUiStore } from "@/state/useUiStore";
import type { ToolId } from "@/state/useDomainStore";

const TOOLS: Array<{ id: ToolId; label: string; hint: string }> = [
  { id: "pan", label: "✋", hint: "平移 (Pan)" },
  { id: "brush", label: "●", hint: "笔刷 (Brush)" },
  { id: "placeBuilding", label: "▭", hint: "放置建筑 (Place)" },
];

export function ToolPanel() {
  const activeTool = useUiStore((s) => s.activeTool);
  const setTool = useUiStore((s) => s.setTool);

  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: 8, gap: 4 }}>
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
    </div>
  );
}