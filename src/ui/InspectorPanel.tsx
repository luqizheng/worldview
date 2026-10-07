// src/ui/InspectorPanel.tsx
// 右检查器面板：选中对象详情 / 当前地图基础信息（MVP 阶段：基础信息）

import { useDomainStore } from "@/state/useDomainStore";
import { useUiStore } from "@/state/useUiStore";

export function InspectorPanel() {
  const currentMap = useDomainStore((s) => s.currentMap);
  const selectedId = useUiStore((s) => s.selectedObjectId);
  const dirty = useUiStore((s) => s.isDirty);

  return (
    <div style={{ padding: 12, color: "#aaa", fontSize: 12 }}>
      <div style={{ fontWeight: 600, color: "#aaa", marginBottom: 8 }}>检查器</div>
      {!currentMap && <div style={{ color: "#666" }}>未加载地图</div>}
      {currentMap && (
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <div>
            <span style={{ color: "#888" }}>id: </span>
            {currentMap.id}
          </div>
          <div>
            <span style={{ color: "#888" }}>name: </span>
            {currentMap.name}
          </div>
          <div>
            <span style={{ color: "#888" }}>type: </span>
            {currentMap.type}
          </div>
          <div>
            <span style={{ color: "#888" }}>path: </span>
            <code style={{ fontSize: 11 }}>{currentMap.path}</code>
          </div>
          <div style={{ marginTop: 6 }}>
            <span style={{ color: "#888" }}>选中: </span>
            {selectedId ?? "(无)"}
          </div>
          <div>
            <span style={{ color: "#888" }}>dirty: </span>
            {dirty ? "是" : "否"}
          </div>
        </div>
      )}
    </div>
  );
}