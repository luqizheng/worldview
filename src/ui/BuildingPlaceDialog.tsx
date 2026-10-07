// src/ui/BuildingPlaceDialog.tsx
// 建筑放置对话框（spec §T15 / §H2）。
// 用户在拖拽完建筑占地后弹出，输入 name（必填）+ interior_ref（可选），确认后入栈 PlaceBuildingCommand。

import { useState } from "react";
import { useEditingStore } from "@/state/useEditingStore";
import { useDomainStore } from "@/state/useDomainStore";
import { PlaceBuildingCommand } from "@/editing/commands";
import type { BuildingGeometry, BuildingRef } from "@/types";

export interface BuildingPlaceDialogProps {
  geometry: BuildingGeometry;
  buildingType: string;
  mapId: string;
  /** 提交后回调（用于关闭对话框 + 重绘） */
  onCommit: (b: BuildingRef) => void;
  onCancel: () => void;
}

export function BuildingPlaceDialog({
  geometry,
  buildingType,
  mapId,
  onCommit,
  onCancel,
}: BuildingPlaceDialogProps) {
  const [name, setName] = useState("");
  const [interiorRef, setInteriorRef] = useState("");

  const valid = name.trim().length > 0;

  function commit() {
    if (!valid) return;
    const building: BuildingRef = {
      id: `bld_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`,
      name: name.trim(),
      type: buildingType,
      geometry,
      rotation: 0,
      floors: 1,
      state: "intact",
      // interior_ref 仅在非空时填入（spec §H3：不留空时不写）
      ...(interiorRef.trim() ? { interior_ref: interiorRef.trim() } : {}),
    };
    const cmd = new PlaceBuildingCommand(mapId, building, -1);
    useEditingStore.getState().runCommand(cmd);
    if (useDomainStore.getState().currentMapPath) {
      useDomainStore.getState().markDirty(useDomainStore.getState().currentMapPath!);
    }
    onCommit(building);
  }

  return (
    <div
      onClick={onCancel}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 900,
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
        <div style={{ fontSize: 14, fontWeight: 600 }}>放置建筑：{buildingType}</div>
        <div style={{ fontSize: 11, color: "#888" }}>
          占地：x={geometry.x}, y={geometry.y}, w={geometry.w}, h={geometry.h}
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <label style={{ fontSize: 12 }}>名称 *</label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="如 林语居"
            autoFocus
            style={{
              padding: "4px 8px",
              background: "#2a2a2a",
              border: "1px solid #555",
              color: "#eee",
              borderRadius: 3,
            }}
          />
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <label style={{ fontSize: 12 }}>interior_ref（可选，留空表示不创建室内图）</label>
          <input
            value={interiorRef}
            onChange={(e) => setInteriorRef(e.target.value)}
            placeholder="maps/building/xxx.yaml"
            style={{
              padding: "4px 8px",
              background: "#2a2a2a",
              border: "1px solid #555",
              color: "#eee",
              borderRadius: 3,
            }}
          />
        </div>
        <div style={{ display: "flex", gap: 8, marginTop: 8, justifyContent: "flex-end" }}>
          <button
            onClick={onCancel}
            style={{
              padding: "4px 12px",
              background: "#444",
              color: "#fff",
              border: "none",
              borderRadius: 3,
              cursor: "pointer",
            }}
          >
            取消
          </button>
          <button
            onClick={commit}
            disabled={!valid}
            style={{
              padding: "4px 12px",
              background: valid ? "#5a7a5a" : "#444",
              color: valid ? "#fff" : "#888",
              border: "none",
              borderRadius: 3,
              cursor: valid ? "pointer" : "not-allowed",
            }}
          >
            确认
          </button>
        </div>
      </div>
    </div>
  );
}

/** 占位：MVP 简化 — 提供一个下拉菜单用的常量列表。 */
export const BUILDING_TYPES: readonly string[] = [
  "住宅",
  "商店",
  "酒馆",
  "神庙",
  "城堡",
  "兵营",
  "市场",
  "码头",
  "监狱",
  "魔法塔",
] as const;
void BUILDING_TYPES;