// src/app/App.tsx
// MVP 应用根：五段布局（顶栏 / 左工具栏 / 中央画布 / 右面板 / 状态栏）+ ErrorDialog。

import { TopBar } from "@/ui/TopBar";
import { ToolPanel } from "@/ui/ToolPanel";
import { LayerPanel } from "@/ui/LayerPanel";
import { InspectorPanel } from "@/ui/InspectorPanel";
import { StatusBar } from "@/ui/StatusBar";
import { MapCanvas } from "@/render/MapCanvas";
import { ErrorDialog } from "@/ui/ErrorDialog";
import { useDomainStore } from "@/state/useDomainStore";

export function App() {
  const projectDir = useDomainStore((s) => s.projectDir);
  const currentMapPath = useDomainStore((s) => s.currentMapPath);

  return (
    <div
      style={{
        display: "grid",
        gridTemplateRows: "44px 1fr 24px",
        gridTemplateColumns: "56px 1fr 280px",
        height: "100vh",
        width: "100vw",
        background: "#1a1a1a",
        color: "#e0e0e0",
        fontFamily: "system-ui, -apple-system, sans-serif",
      }}
    >
      {/* 顶栏 */}
      <div style={{ gridColumn: "1 / -1", borderBottom: "1px solid #333" }}>
        <TopBar />
      </div>

      {/* 左工具栏 */}
      <div style={{ borderRight: "1px solid #333", background: "#222" }}>
        <ToolPanel />
      </div>

      {/* 中央画布 */}
      <div
        style={{
          position: "relative",
          background: "#0d0d0d",
          overflow: "hidden",
        }}
      >
        {projectDir && currentMapPath ? <MapCanvas /> : <EmptyState />}
      </div>

      {/* 右面板 */}
      <div style={{ borderLeft: "1px solid #333", background: "#222", overflow: "auto" }}>
        <LayerPanel />
        <InspectorPanel />
      </div>

      {/* 状态栏 */}
      <div style={{ gridColumn: "1 / -1", borderTop: "1px solid #333", background: "#1f1f1f" }}>
        <StatusBar />
      </div>

      {/* 全局错误对话框 */}
      <ErrorDialog />
    </div>
  );
}

function EmptyState() {
  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        flexDirection: "column",
        gap: 16,
        color: "#666",
      }}
    >
      <div style={{ fontSize: 18, fontWeight: 600 }}>World View</div>
      <div style={{ fontSize: 13 }}>尚未打开工程。点击顶栏「打开工程」加载一个目录。</div>
    </div>
  );
}