//! 地形类型与单元。
//!
//! 字段定义以 `docs/数据模型规格_v0.1.md` §3 为唯一真源。
//! MVP 阶段：`height` 字段不参与计算，固定为 0；只承载 `type` / `move_cost` / `buildable` / `resources`。

use serde::{Deserialize, Serialize};

use crate::error::{CoreError, CoreResult};

/// 地形类型枚举。键必须与 [数据模型规格 §3.1] 完全一致。
///
/// [数据模型规格 §3.1]: ../../../docs/数据模型规格_v0.1.md#31-地形类型terrain_type-枚举
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TerrainType {
    Mountain,
    Forest,
    Plain,
    Hill,
    Water,
    Desert,
    Swamp,
    Tundra,
    Basin,
    Coast,
    River,
}

impl TerrainType {
    /// MVP 默认地形；与 `terrain_data.default.type` 一致。
    pub const DEFAULT: Self = Self::Plain;

    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Mountain => "mountain",
            Self::Forest => "forest",
            Self::Plain => "plain",
            Self::Hill => "hill",
            Self::Water => "water",
            Self::Desert => "desert",
            Self::Swamp => "swamp",
            Self::Tundra => "tundra",
            Self::Basin => "basin",
            Self::Coast => "coast",
            Self::River => "river",
        }
    }

    pub fn from_str(s: &str) -> Option<Self> {
        Some(match s {
            "mountain" => Self::Mountain,
            "forest" => Self::Forest,
            "plain" => Self::Plain,
            "hill" => Self::Hill,
            "water" => Self::Water,
            "desert" => Self::Desert,
            "swamp" => Self::Swamp,
            "tundra" => Self::Tundra,
            "basin" => Self::Basin,
            "coast" => Self::Coast,
            "river" => Self::River,
            _ => return None,
        })
    }
}

/// 地形单元。
///
/// MVP 字段：`type` / `move_cost` / `buildable` / `resources` / `height`（固定 0）。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct TerrainCell {
    pub r#type: TerrainType,
    /// `-1` 表示不可通行（深水、断崖）。
    pub move_cost: i32,
    pub buildable: bool,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub resources: Vec<String>,
    /// MVP 阶段固定 `0`；`height_data` 不参与计算。
    #[serde(default, skip_serializing_if = "is_zero")]
    pub height: i32,
}

fn is_zero(n: &i32) -> bool {
    *n == 0
}

impl TerrainCell {
    /// MVP 默认单元：平原、可通行、可建造。
    pub fn plain_default() -> Self {
        Self {
            r#type: TerrainType::Plain,
            move_cost: 1,
            buildable: true,
            resources: Vec::new(),
            height: 0,
        }
    }

    /// 与 `default` 字段比较：返回 true 表示与默认完全相同，可从 sparse 编码中剔除。
    pub fn equals_default(&self, other: &TerrainCell) -> bool {
        self.r#type == other.r#type
            && self.move_cost == other.move_cost
            && self.buildable == other.buildable
            && self.resources == other.resources
            && self.height == other.height
    }
}

/// 地形网格（MVP 阶段内存中以 `Vec<TerrainCell>` 行优先存储）。
///
/// 坐标：`(col, row)`，`index = row * width + col`。`width` / `height` 与 [Map::grid] 一致。
#[derive(Debug, Clone)]
pub struct TerrainGrid {
    pub width: u32,
    pub height: u32,
    cells: Vec<TerrainCell>,
    default: TerrainCell,
}

impl TerrainGrid {
    pub fn new(width: u32, height: u32, default: TerrainCell) -> Self {
        let cells = vec![default.clone(); (width as usize) * (height as usize)];
        Self {
            width,
            height,
            cells,
            default,
        }
    }

    pub fn default_cell(&self) -> &TerrainCell {
        &self.default
    }

    pub fn from_cells(width: u32, height: u32, default: TerrainCell, cells: Vec<TerrainCell>) -> CoreResult<Self> {
        let expected = (width as usize) * (height as usize);
        if cells.len() != expected {
            return Err(CoreError::Validation(format!(
                "terrain cells 长度 = {}，期望 = {}（grid {}×{}）",
                cells.len(),
                expected,
                width,
                height
            )));
        }
        Ok(Self { width, height, cells, default })
    }

    /// **仅供测试 / 从磁盘加载时**使用的 raw 构造器：不检查 cells 长度。
    /// 长度校验请走 [`crate::validate::validate_loaded`]。
    pub fn from_raw(width: u32, height: u32, default: TerrainCell, cells: Vec<TerrainCell>) -> Self {
        Self { width, height, cells, default }
    }

    pub fn cells(&self) -> &[TerrainCell] {
        &self.cells
    }

    pub fn cells_mut(&mut self) -> &mut [TerrainCell] {
        &mut self.cells
    }

    pub fn get_cell(&self, col: u32, row: u32) -> Option<&TerrainCell> {
        if col >= self.width || row >= self.height {
            return None;
        }
        let idx = (row as usize) * (self.width as usize) + (col as usize);
        self.cells.get(idx)
    }

    pub fn set_cell(&mut self, col: u32, row: u32, cell: TerrainCell) -> CoreResult<()> {
        if col >= self.width || row >= self.height {
            return Err(CoreError::Range(format!(
                "cell ({col}, {row}) 越界 grid ({}x{})",
                self.width,
                self.height
            )));
        }
        let idx = (row as usize) * (self.width as usize) + (col as usize);
        self.cells[idx] = cell;
        Ok(())
    }

    /// 整图按 `default` 重置。
    pub fn fill_default(&mut self) {
        for cell in &mut self.cells {
            *cell = self.default.clone();
        }
    }

    /// 转为 sparse cells 列表（仅保留与 default 不同的单元），按 `(row, col)` 升序排列。
    ///
    /// 实现参见 [`docs/存储与目录规范_v0.1.md` §5.1.4](../../../../docs/存储与目录规范_v0.1.md)。
    pub fn to_sparse_cells(&self) -> Vec<SparseCell> {
        let mut out = Vec::new();
        for row in 0..self.height {
            for col in 0..self.width {
                let idx = (row as usize) * (self.width as usize) + (col as usize);
                let cell = &self.cells[idx];
                if !cell.equals_default(&self.default) {
                    out.push(SparseCell {
                        col,
                        row,
                        cell: cell.clone(),
                    });
                }
            }
        }
        out
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SparseCell {
    pub col: u32,
    pub row: u32,
    #[serde(flatten)]
    pub cell: TerrainCell,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn terrain_type_round_trip() {
        for ty in [
            TerrainType::Mountain,
            TerrainType::Forest,
            TerrainType::Plain,
            TerrainType::Hill,
            TerrainType::Water,
            TerrainType::Desert,
            TerrainType::Swamp,
            TerrainType::Tundra,
            TerrainType::Basin,
            TerrainType::Coast,
            TerrainType::River,
        ] {
            let s = ty.as_str();
            assert_eq!(TerrainType::from_str(s), Some(ty));
        }
        assert_eq!(TerrainType::from_str("未知"), None);
    }

    #[test]
    fn terrain_grid_default_fill() {
        let mut grid = TerrainGrid::new(4, 3, TerrainCell::plain_default());
        grid.fill_default();
        for cell in grid.cells() {
            assert!(cell.equals_default(&TerrainCell::plain_default()));
        }
        assert_eq!(grid.to_sparse_cells().len(), 0);
    }

    #[test]
    fn terrain_grid_sparse_round_trip() {
        let mut grid = TerrainGrid::new(4, 4, TerrainCell::plain_default());
        let mut mountain = TerrainCell::plain_default();
        mountain.r#type = TerrainType::Mountain;
        mountain.move_cost = 4;
        mountain.buildable = false;
        grid.set_cell(1, 1, mountain.clone()).unwrap();

        let sparse = grid.to_sparse_cells();
        assert_eq!(sparse.len(), 1);
        assert_eq!(sparse[0].col, 1);
        assert_eq!(sparse[0].row, 1);
        assert_eq!(sparse[0].cell.r#type, TerrainType::Mountain);

        // 重置 → 改回 plain → sparse 列表应为空
        grid.fill_default();
        assert!(grid.to_sparse_cells().is_empty());
    }

    #[test]
    fn terrain_grid_out_of_range() {
        let grid = TerrainGrid::new(2, 2, TerrainCell::plain_default());
        assert!(grid.get_cell(2, 0).is_none());
        assert!(grid.get_cell(0, 2).is_none());
    }
}