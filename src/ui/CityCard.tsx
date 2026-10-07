// src/ui/CityCard.tsx
// 城市基础信息卡（spec §T16.1）。
// - 显示 city_refs[i] 的 name / population / faction / notes
// - 根据 map_ref 是否为空，按钮文案切换「进入编辑」/「创建城市地图」
// - 创建调用 useDomainStore.createCityMap（spec §T16.4）

import { useState } from "react";
import { useDomainStore } from "@/state/useDomainStore";
import type { CityRef } from "@/types";

export interface CityCardProps {
  city: CityRef;
  currentWorldPath: string;
  onClose: () => void;
}

export function CityCard({ city, currentWorldPath, onClose }: CityCardProps) {
  const switchMap = useDomainStore((s) => s.switchMap);
  const createCityMap = useDomainStore((s) => s.createCityMap);
  const [creating, setCreating] = useState(false);

  async function handleEnter() {
    if (city.map_ref) {
      switchMap(city.map_ref);
      onClose();
    } else {
      setCreating(true);
      try {
        const ok = await createCityMap(currentWorldPath, city.id, city.name, city.id);
        if (ok) onClose();
      } finally {
        setCreating(false);
      }
    }
  }

  return (
    <div
      onClick={onClose}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.4)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 800,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: 360,
          background: "#222",
          color: "#e0e0e0",
          borderRadius: 8,
          border: "1px solid #555",
          padding: 16,
          display: "flex",
          flexDirection: "column",
          gap: 10,
        }}
      >
        <div style={{ display: "flex", alignItems: "center" }}>
          <div style={{ flex: 1, fontSize: 16, fontWeight: 700, color: "#c8a55a" }}>{city.name}</div>
          <button
            onClick={onClose}
            style={{
              background: "transparent",
              color: "#aaa",
              border: "1px solid #555",
              borderRadius: 4,
              padding: "2px 8px",
              cursor: "pointer",
            }}
          >
            关闭
          </button>
        </div>

        <div style={{ fontSize: 11, color: "#888" }}>
          <div><span style={{ color: "#888" }}>id: </span><code>{city.id}</code></div>
          <div><span style={{ color: "#888" }}>坐标: </span>({city.x}, {city.y})</div>
          {city.population !== undefined && (
            <div><span style={{ color: "#888" }}>人口: </span>{city.population.toLocaleString()}</div>
          )}
          {city.faction && (
            <div><span style={{ color: "#888" }}>势力: </span><code>{city.faction}</code></div>
          )}
          {city.notes && (
            <div style={{ marginTop: 6, whiteSpace: "pre-wrap" }}>{city.notes.trim()}</div>
          )}
        </div>

        <div style={{ display: "flex", gap: 8, marginTop: 4, justifyContent: "flex-end" }}>
          <button
            onClick={handleEnter}
            disabled={creating}
            style={{
              padding: "6px 16px",
              background: creating ? "#444" : "#5a7a5a",
              color: creating ? "#888" : "#fff",
              border: "none",
              borderRadius: 4,
              cursor: creating ? "not-allowed" : "pointer",
              fontSize: 13,
            }}
          >
            {creating
              ? "创建中…"
              : city.map_ref
                ? "进入编辑"
                : "创建城市地图"}
          </button>
        </div>
      </div>
    </div>
  );
}