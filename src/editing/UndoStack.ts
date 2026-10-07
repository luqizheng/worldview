// src/editing/UndoStack.ts
// FIFO 上限 200 的命令栈（spec §6.3）

import type { Command } from "./commands";

export class UndoStack {
  private undoStack: Command[] = [];
  private redoStack: Command[] = [];
  private readonly cap: number;

  constructor(cap = 200) {
    this.cap = cap;
  }

  push(cmd: Command): void {
    cmd.apply();
    this.undoStack.push(cmd);
    if (this.undoStack.length > this.cap) {
      this.undoStack.shift();
    }
    // 新操作清空 redo 栈
    this.redoStack.length = 0;
  }

  undo(): boolean {
    const cmd = this.undoStack.pop();
    if (!cmd) return false;
    cmd.revert();
    this.redoStack.push(cmd);
    return true;
  }

  redo(): boolean {
    const cmd = this.redoStack.pop();
    if (!cmd) return false;
    cmd.apply();
    this.undoStack.push(cmd);
    return true;
  }

  clear(): void {
    this.undoStack.length = 0;
    this.redoStack.length = 0;
  }

  get size(): number {
    return this.undoStack.length;
  }

  get redoSize(): number {
    return this.redoStack.length;
  }
}