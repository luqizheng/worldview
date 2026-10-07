// src/ui/ErrorDialog.tsx
// 全局错误对话框（spec §10.7 / §T16.4）

import { useErrorStore } from "@/state/useErrorStore";
import type { ValidationError } from "@/types";

export function ErrorDialog() {
  const dialog = useErrorStore((s) => s.dialog);
  const close = useErrorStore((s) => s.close);

  if (!dialog) return null;

  return (
    <div
      onClick={close}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.6)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 1000,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: 600,
          maxHeight: "70vh",
          background: "#222",
          color: "#e0e0e0",
          borderRadius: 8,
          border: "1px solid #555",
          display: "flex",
          flexDirection: "column",
        }}
      >
        <div
          style={{
            padding: "12px 16px",
            borderBottom: "1px solid #333",
            display: "flex",
            alignItems: "center",
          }}
        >
          <div style={{ flex: 1, fontSize: 14, fontWeight: 600, color: "#e8a55a" }}>{dialog.title}</div>
          <button
            onClick={close}
            style={{
              background: "transparent",
              color: "#aaa",
              border: "1px solid #555",
              borderRadius: 4,
              padding: "2px 10px",
              cursor: "pointer",
            }}
          >
            关闭
          </button>
        </div>
        <div style={{ padding: 16, overflow: "auto", flex: 1 }}>
          {dialog.errors.length === 0 ? (
            <div style={{ color: "#888" }}>(无错误)</div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {dialog.errors.map((err: ValidationError, i: number) => (
                <ErrorItem key={i} err={err} />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function ErrorItem({ err }: { err: ValidationError }) {
  const text = `[${err.path}] ${err.reason}`;
  return (
    <div
      style={{
        background: "#2a2a2a",
        border: "1px solid #444",
        borderRadius: 4,
        padding: "6px 10px",
        display: "flex",
        alignItems: "flex-start",
        gap: 8,
        fontSize: 12,
        fontFamily: "ui-monospace, monospace",
      }}
    >
      <div style={{ flex: 1, wordBreak: "break-all" }}>{text}</div>
      <button
        onClick={() => navigator.clipboard?.writeText(text)}
        title="复制纯文本"
        style={{
          background: "#3a3a3a",
          color: "#fff",
          border: "1px solid #555",
          borderRadius: 3,
          padding: "2px 8px",
          cursor: "pointer",
          fontSize: 11,
        }}
      >
        复制
      </button>
    </div>
  );
}