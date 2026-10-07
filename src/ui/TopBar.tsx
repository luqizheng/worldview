// src/ui/TopBar.tsx
// 顶栏：项目名 + 「打开工程」+ 「保存」（spec §C1 / D1）

import { useState } from "react";
import { useDomainStore } from "@/state/useDomainStore";

export function TopBar() {
  const project = useDomainStore((s) => s.project);
  const dirtyMapPaths = useDomainStore((s) => s.dirtyMapPaths);
  const openProject = useDomainStore((s) => s.openProject);
  const saveProject = useDomainStore((s) => s.saveProject);
  const [path, setPath] = useState("");

  const dirty = dirtyMapPaths.size > 0;

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "0 16px", height: "100%" }}>
      <div style={{ fontWeight: 700, fontSize: 14, color: "#c8a55a" }}>World View</div>
      <div style={{ width: 1, height: 20, background: "#444" }} />
      <div style={{ fontSize: 13, color: "#aaa" }}>{project?.name ?? "(未打开工程)"}</div>
      {dirty && <span style={{ color: "#e8a55a", fontSize: 12 }}>● 未保存</span>}

      <div style={{ flex: 1 }} />

      <input
        type="text"
        placeholder="工程目录路径"
        value={path}
        onChange={(e) => setPath(e.target.value)}
        style={{
          width: 360,
          padding: "4px 8px",
          background: "#2a2a2a",
          border: "1px solid #444",
          color: "#eee",
          borderRadius: 4,
          fontSize: 12,
        }}
      />
      <button
        onClick={() => path && openProject(path)}
        style={{
          padding: "4px 12px",
          background: "#3a5a3a",
          color: "#fff",
          border: "none",
          borderRadius: 4,
          fontSize: 12,
          cursor: "pointer",
        }}
      >
        打开工程
      </button>
      <button
        onClick={() => saveProject()}
        disabled={!dirty}
        style={{
          padding: "4px 12px",
          background: dirty ? "#5a5a8a" : "#444",
          color: dirty ? "#fff" : "#888",
          border: "none",
          borderRadius: 4,
          fontSize: 12,
          cursor: dirty ? "pointer" : "not-allowed",
        }}
        title={dirty ? "保存到磁盘" : "无变更"}
      >
        保存
      </button>
    </div>
  );
}