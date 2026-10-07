// src/state/useErrorStore.ts
// 全局错误对话框 store（spec §10.7）：
// - 全局挂载在 App.tsx 顶层
// - domain.saveProject 失败时调用 show({title, errors})
// - 任何 command 失败时也可调用 show({...})

import { create } from "zustand";
import type { ValidationError } from "@/types";

export interface ErrorDialog {
  title: string;
  errors: ValidationError[];
}

export interface ErrorState {
  dialog: ErrorDialog | null;
  show(dialog: ErrorDialog): void;
  close(): void;
}

export const useErrorStore = create<ErrorState>((set) => ({
  dialog: null,
  show(dialog) {
    set({ dialog });
  },
  close() {
    set({ dialog: null });
  },
}));