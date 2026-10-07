// src/state/useEditingStore.ts
// 编辑层 store：UndoStack + runCommand / undo / redo。

import { create } from "zustand";
import { UndoStack } from "@/editing/UndoStack";
import type { Command } from "@/editing/commands";

export interface EditingState {
  undo: UndoStack;
  runCommand(cmd: Command): void;
  undoOne(): void;
  redoOne(): void;
  clearAll(): void;
}

export const useEditingStore = create<EditingState>((set, get) => ({
  undo: new UndoStack(200),

  runCommand(cmd) {
    // MVP 阶段：直接 push + apply；dirty 标记由 caller 决定
    get().undo.push(cmd);
    set({ undo: get().undo });
  },

  undoOne() {
    const ok = get().undo.undo();
    set({ undo: get().undo });
    void ok;
  },

  redoOne() {
    const ok = get().undo.redo();
    set({ undo: get().undo });
    void ok;
  },

  clearAll() {
    get().undo.clear();
    set({ undo: get().undo });
  },
}));