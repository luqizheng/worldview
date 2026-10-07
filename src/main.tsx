// src/main.tsx
// Vite 入口；挂载 React 应用。

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "@/app/App";

const root = document.getElementById("root");
if (!root) {
  throw new Error("missing #root element");
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);