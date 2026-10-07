// src/editing/commands.ts
// 命令模式接口 + 三个 MVP 命令（spec §6.3）。

export interface Command {
  /** 命令类型名（用于序列化 / 日志） */
  kind: "apply_brush" | "create_city_map" | "place_building" | "noop";
  /** 应用命令 */
  apply(): void;
  /** 撤销命令 */
  revert(): void;
  /** 笔刷 / 放置涉及的网格脏区（MVP：占位，未来 cells 改动生效时用于 TerrainLayer.redrawRect） */
  dirtyRect?: { colMin: number; colMax: number; rowMin: number; rowMax: number } | null;
  /** 任意附加元数据（display / 调试用） */
  meta?: Record<string, unknown>;
}

export class ApplyBrushCommand implements Command {
  readonly kind = "apply_brush" as const;
  constructor(
    public readonly stroke: Array<{ col: number; row: number }>,
    public readonly before: Map<string, import("@/types").TerrainCell>,
    public readonly after: Map<string, import("@/types").TerrainCell>,
    public readonly dirtyRect: { colMin: number; colMax: number; rowMin: number; rowMax: number } | null,
  ) {}
  apply(): void {
    // 真实实现：把 `after` 写回 store / LoadedMap.grid
  }
  revert(): void {
    // 真实实现：把 `before` 写回
  }
}

export class CreateCityMapCommand implements Command {
  readonly kind = "create_city_map" as const;
  constructor(
    public readonly newPath: string,
    public readonly initialMap: import("@/types").Map,
    public readonly parentWorldId: string,
    public readonly cityId: string,
  ) {}
  apply(): void {
    // 真实实现：把 newPath 写入 LoadedProject.maps
  }
  revert(): void {
    // 真实实现：从 LoadedProject.maps 移除 newPath
  }
}

export class PlaceBuildingCommand implements Command {
  readonly kind = "place_building" as const;
  constructor(
    public readonly mapId: string,
    public readonly building: import("@/types").BuildingRef,
    public readonly beforeIndex: number,
  ) {}
  apply(): void {
    // 真实实现：在 building_refs[beforeIndex] 插入 building
  }
  revert(): void {
    // 真实实现：从 building_refs 中移除
  }
}