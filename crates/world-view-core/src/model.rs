//! Project / Map / Layer 数据模型。
//!
//! 字段以 [数据模型规格_v0.1.md] 为唯一真源；本文件只实现 MVP 阶段用到的字段子集。

use serde::{Deserialize, Serialize};

use crate::terrain::TerrainGrid;

/// 工程（Project）。
///
/// 对应 `docs/数据模型规格_v0.1.md §1`。MVP 阶段实现字段子集：`project_version` / `name` /
/// `author` / `description` / `era` / `default_time` / `map_index` / `character_index` /
/// `settings` / `timeline`（可选）。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Project {
    pub project_version: String,
    pub name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub author: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    pub era: Era,
    pub default_time: TimePoint,
    pub map_index: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub character_index: Vec<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub timeline: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub settings: Option<ProjectSettings>,
}

/// 工程级纪元。
///
/// 对应 `docs/数据模型规格_v0.1.md §1.1`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Era {
    pub name: String,
    pub start_year: i32,
    pub months_per_year: u32,
    pub days_per_month: Vec<u32>,
    pub hours_per_day: u32,
    pub minutes_per_hour: u32,
    pub seconds_per_minute: u32,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub leap: Option<LeapRule>,
}

/// 闰规则（可选）。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LeapRule {
    pub every: u32,
    pub month: u32,
    pub add_days: u32,
}

/// 绝对时间点（低位字段缺省为 0）。
///
/// 对应 `docs/数据模型规格_v0.1.md §7.2`。
#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq, Eq, Hash)]
pub struct TimePoint {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub era: Option<String>,
    pub year: i32,
    #[serde(default = "one")]
    pub month: u32,
    #[serde(default = "one")]
    pub day: u32,
    #[serde(default)]
    pub hour: u32,
    #[serde(default)]
    pub minute: u32,
    #[serde(default)]
    pub second: u32,
}

fn one() -> u32 {
    1
}

/// 工程级工具设置。
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ProjectSettings {
    #[serde(default)]
    pub autosave: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub backup_keep: Option<u32>,
}

/// 地图（Map）。
///
/// MVP 字段子集：`id` / `name` / `type` / `grid` / `time` / `terrain_data` / `city_refs` /
/// `building_refs` / `layers`。其余（`parent_map` / `child_maps` / `anchor` / `extent` /
/// `markers` / `height_data` / `travel` / `presentation`）MVP 阶段保留字段定义但允许为空。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Map {
    pub id: String,
    pub name: String,
    pub r#type: MapType,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub parent_map: Option<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub child_maps: Vec<String>,
    pub grid: MapGrid,
    pub time: MapTime,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub layers: Vec<Layer>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub terrain_data: Option<TerrainDataRef>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub height_data: Option<TerrainDataRef>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub city_refs: Vec<CityRef>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub building_refs: Vec<BuildingRef>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub presentation: Option<MapPresentation>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub travel: Option<serde_yaml::Value>,
}

/// 地图类型枚举。对应 `docs/数据模型规格_v0.1.md §2.1`。
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MapType {
    World,
    Region,
    City,
    Street,
    Building,
    Underground,
}

impl MapType {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::World => "world",
            Self::Region => "region",
            Self::City => "city",
            Self::Street => "street",
            Self::Building => "building",
            Self::Underground => "underground",
        }
    }
}

/// 网格与距离设置。对应 `docs/数据模型规格_v0.1.md §2.2`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MapGrid {
    pub width: u32,
    pub height: u32,
    pub cell_distance: f64,
    pub distance_unit: String,
    pub base_unit: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub height_unit: Option<String>,
    #[serde(default = "default_origin")]
    pub origin: GridOrigin,
    #[serde(default = "default_true")]
    pub show: bool,
    #[serde(default = "default_true")]
    pub snap: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub scale_label: Option<String>,
}

fn default_origin() -> GridOrigin {
    GridOrigin { x: 0.0, y: 0.0 }
}
fn default_true() -> bool {
    true
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GridOrigin {
    pub x: f64,
    pub y: f64,
}

/// 地图级时间设置。对应 `docs/数据模型规格_v0.1.md §2.3`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MapTime {
    pub default_granularity: String,
    pub min_granularity: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub display_format: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub event_sort_unit: Option<String>,
    #[serde(default = "default_true")]
    pub inherit_calendar: bool,
    #[serde(default = "default_true")]
    pub allow_fuzzy: bool,
}

/// 图层。对应 `docs/数据模型规格_v0.1.md §4`。
///
/// MVP 阶段：`data` 与 `params` 字段以 `serde_yaml::Value` 透传，避免实现层污染字段定义。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Layer {
    pub id: String,
    pub name: String,
    pub r#type: String,
    pub visible: bool,
    pub locked: bool,
    pub opacity: f64,
    pub z_order: i32,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub valid_from: Option<TimePoint>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub valid_to: Option<TimePoint>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub data: Option<serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub params: Option<serde_yaml::Value>,
}

/// 地形 / 高度数据索引。对应 `docs/数据模型规格_v0.1.md §3.5`。
///
/// MVP 阶段只支持 `grid` / `sparse`；`chunked` 字段保留但 MVP 不生成 `.bin`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TerrainDataRef {
    pub encoding: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub cells_ref: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub default: Option<serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub cells: Vec<serde_yaml::Value>,
}

/// 城市条目。对应 `docs/数据模型规格_v0.1.md §2.5`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CityRef {
    pub id: String,
    pub name: String,
    pub x: f64,
    pub y: f64,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub entry: Option<GridCoord>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub map_ref: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub population: Option<u64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub faction: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GridCoord {
    pub x: f64,
    pub y: f64,
}

/// 建筑条目。对应 `docs/数据模型规格_v0.1.md §2.6`。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BuildingRef {
    pub id: String,
    pub name: String,
    pub r#type: String,
    pub geometry: BuildingGeometry,
    #[serde(default)]
    pub rotation: f64,
    #[serde(default = "one_u32")]
    pub floors: u32,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub entrance: Vec<GridCoord>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub interior_ref: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub faction: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub owner: Option<String>,
    #[serde(default = "default_building_state")]
    pub state: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub custom_attributes: Option<serde_yaml::Value>,
}

fn one_u32() -> u32 {
    1
}
fn default_building_state() -> String {
    "intact".to_string()
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BuildingGeometry {
    pub x: f64,
    pub y: f64,
    pub w: f64,
    pub h: f64,
}

/// 呈现与视图设置。对应 `docs/数据模型规格_v0.1.md §2.8`。
///
/// MVP 阶段只关心 `default_mode` / `view_mode` / `show_scale_bar` / `show_coordinates`；
/// 其它字段透传。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MapPresentation {
    #[serde(default = "default_edit_mode")]
    pub default_mode: String,
    #[serde(default = "default_2d_view")]
    pub view_mode: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub theme: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub observer: Option<String>,
    #[serde(default = "default_true")]
    pub show_scale_bar: bool,
    #[serde(default = "default_true")]
    pub show_coordinates: bool,
    #[serde(default)]
    pub show_minimap: bool,
}

fn default_edit_mode() -> String {
    "edit".to_string()
}
fn default_2d_view() -> String {
    "2d".to_string()
}

/// MVP 阶段：内存中持有已加载的网格。
///
/// `Project` / `Map` 仅持有 YAML 元数据；`LoadedMap` 同时持有元数据 + 网格 + 编码。
/// 由 `world-view-io` 在 `open_project` 时填充。
#[derive(Debug, Clone)]
pub struct LoadedProject {
    pub project: Project,
    pub maps: std::collections::HashMap<String, LoadedMap>,
}

/// 已加载的地图：Map YAML + TerrainGrid + 路径索引。
#[derive(Debug, Clone)]
pub struct LoadedMap {
    pub path: String,
    pub map: Map,
    pub grid: TerrainGrid,
}

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE_PROJECT: &str = r#"
project_version: "0.1"
name: 示例世界
era:
  name: 创世历
  start_year: 1
  months_per_year: 12
  days_per_month: [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
  hours_per_day: 24
  minutes_per_hour: 60
  seconds_per_minute: 60
default_time:
  year: 1042
  month: 3
  day: 12
map_index:
  - maps/world/main.yaml
settings:
  autosave: false
"#;

    #[test]
    fn parse_project_yaml() {
        let project: Project = serde_yaml::from_str(SAMPLE_PROJECT).unwrap();
        assert_eq!(project.project_version, "0.1");
        assert_eq!(project.name, "示例世界");
        assert_eq!(project.era.months_per_year, 12);
        assert_eq!(project.default_time.year, 1042);
        assert_eq!(project.map_index.len(), 1);
        assert!(!project.settings.as_ref().unwrap().autosave);
    }
}