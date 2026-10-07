//! 校验器：MVP 阶段实现 spec §3「Requirement: 校验器」五条规则。
//!
//! 校验结果用 `Vec<ValidationError>` 表达，每条带「字段 + 原因」便于 UI 定位。
//! 范围纪律：本模块不读文件、不写文件——只接受已加载的 `LoadedProject` 数据。
//!
//! 五条规则：
//! 1. `project.yaml` 存在且 `project_version = "0.1"`；
//! 2. 所有 `map_ref` / `interior_ref` / `cells_ref` 指向的文件存在（在 `open_project` 加载阶段走「已加载的 maps 集合」做可达性检查；MVP `validate_loaded` 假设 `open_project` 已
//!    全部加载，文件不可达 = 不在 `loaded.maps` 中）；
//! 3. `id` 工程内全局唯一（跨类型：Map / CityRef / BuildingRef / Character / Timeline）；
//! 4. **grid 编码**：cells 行数 == `grid.height`、列数 == `grid.width`；
//!    **sparse 编码**：每个 cell 的 `(col, row)` 落在 `[0, width) × [0, height)` 内，且 `(col, row)` 唯一不重复；
//! 5. `city_refs[i].map_ref` 为空时合法（未创建城市地图）；非空时路径可达（合并到规则 2）。

use std::collections::HashSet;

use serde::Deserialize;

use crate::model::{BuildingRef, CityRef, LoadedProject, Project};
use crate::terrain::{TerrainCell, TerrainGrid, TerrainType};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ValidationError {
    pub path: String,
    pub reason: String,
}

impl std::fmt::Display for ValidationError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "[{}] {}", self.path, self.reason)
    }
}

/// MVP 校验入口（仅 `Project` 元数据，无地图细节）。
///
/// 跑规则 1 / 部分规则 2（路径字符串形态）/ 部分规则 3（基于 path stem 的去重）。
pub fn validate(project: &Project) -> Vec<ValidationError> {
    let mut errors = Vec::new();

    // 1. project_version 必须为 "0.1"
    if project.project_version != "0.1" {
        errors.push(ValidationError {
            path: "project.project_version".into(),
            reason: format!("不支持的版本 '{}'（仅支持 '0.1'）", project.project_version),
        });
    }

    // 2. 路径引用规则：相对工程根、不含 `..`、不绝对
    for (idx, map_path) in project.map_index.iter().enumerate() {
        check_path(map_path, &format!("map_index[{idx}]"), &mut errors);
    }
    for (idx, ch_path) in project.character_index.iter().enumerate() {
        check_path(ch_path, &format!("character_index[{idx}]"), &mut errors);
    }
    if let Some(timeline) = &project.timeline {
        check_path(timeline, "timeline", &mut errors);
    }

    // 3. ID 全局唯一（跨类型；MVP 阶段按 path stem 做粗校验）
    let mut seen: HashSet<(String, String)> = HashSet::new();
    for ch_path in &project.character_index {
        if let Some(id) = path_stem(ch_path) {
            check_unique(
                &mut seen,
                ("character".to_string(), id.clone()),
                &mut errors,
                "character_index",
            );
        }
    }
    if let Some(timeline) = &project.timeline {
        if let Some(id) = path_stem(timeline) {
            check_unique(
                &mut seen,
                ("timeline".to_string(), id.clone()),
                &mut errors,
                "timeline",
            );
        }
    }
    for map_path in &project.map_index {
        if let Some(id) = path_stem(map_path) {
            check_unique(
                &mut seen,
                ("map".to_string(), id.clone()),
                &mut errors,
                "map_index",
            );
        }
    }

    errors
}

/// 加载工程后再次校验（带地图 / 网格细节）。
///
/// 跑全部 5 条规则。
pub fn validate_loaded(loaded: &LoadedProject) -> Vec<ValidationError> {
    let mut errors = validate(&loaded.project);

    // 规则 3：跨 Map / CityRef / BuildingRef / Character / Timeline ID 全局唯一
    // 跨类型不分彼此 — 同一 ID 字符串在工程内唯一。
    let mut seen_ids: HashSet<String> = HashSet::new();
    for (map_path, lm) in &loaded.maps {
        check_unique_str(&mut seen_ids, lm.map.id.clone(), &mut errors, &format!("{map_path}.id"));
        for (i, city) in lm.map.city_refs.iter().enumerate() {
            check_unique_str(
                &mut seen_ids,
                city.id.clone(),
                &mut errors,
                &format!("{map_path}:city_refs[{i}].id"),
            );
        }
        for (i, bld) in lm.map.building_refs.iter().enumerate() {
            check_unique_str(
                &mut seen_ids,
                bld.id.clone(),
                &mut errors,
                &format!("{map_path}:building_refs[{i}].id"),
            );
        }
    }
    // Character / Timeline ID 唯一（基于 path）
    for ch_path in &loaded.project.character_index {
        if let Some(id) = path_stem(ch_path) {
            check_unique_str(
                &mut seen_ids,
                id.clone(),
                &mut errors,
                &format!("character_index[id={id}]"),
            );
        }
    }
    if let Some(timeline) = &loaded.project.timeline {
        if let Some(id) = path_stem(timeline) {
            check_unique_str(
                &mut seen_ids,
                id.clone(),
                &mut errors,
                &format!("timeline[id={id}]"),
            );
        }
    }

    // 规则 2：所有引用可达（map_ref / interior_ref / parent_map → loaded.maps 集合）
    // 注意：cells_ref 指向 **terrain 文件**，不在 loaded.maps 内；它由 `save_project` 在
    // 写盘前通过 `disk_check_loaded` 单独校验（避免重复 I/O 与循环依赖）。
    for (map_path, lm) in &loaded.maps {
        // 规则 4：sparse 坐标范围 + 唯一
        if let Some(td) = &lm.map.terrain_data {
            check_sparse_cells(
                td.cells.as_slice(),
                lm.grid.width(),
                lm.grid.height(),
                &format!("{map_path}:terrain_data.cells"),
                &mut errors,
            );
        }
        if let Some(hd) = &lm.map.height_data {
            if let Some(h_cells_ref) = &hd.cells_ref {
                // height 同样由 disk_check_loaded 校验可达性
                let _ = h_cells_ref;
            }
        }
        // city_refs.map_ref：空合法；非空须可达（规则 5）
        for (i, city) in lm.map.city_refs.iter().enumerate() {
            if let Some(target) = &city.map_ref {
                check_ref_exists(
                    target,
                    &format!("{map_path}:city_refs[{i}].map_ref"),
                    loaded,
                    &mut errors,
                );
            }
        }
        // building_refs.interior_ref：空合法；非空须可达
        for (i, bld) in lm.map.building_refs.iter().enumerate() {
            if let Some(target) = &bld.interior_ref {
                check_ref_exists(
                    target,
                    &format!("{map_path}:building_refs[{i}].interior_ref"),
                    loaded,
                    &mut errors,
                );
            }
        }
        // parent_map / child_maps：MVP 同样按可达性校验
        if let Some(parent) = &lm.map.parent_map {
            check_ref_exists(
                parent,
                &format!("{map_path}:parent_map"),
                loaded,
                &mut errors,
            );
        }
        for child in &lm.map.child_maps {
            check_ref_exists(child, &format!("{map_path}:child_maps"), loaded, &mut errors);
        }
    }

    // 规则 4：grid 编码 cells 长度 = width * height（无论加载时是否在 TerrainGrid 内部截断过）
    for (map_path, lm) in &loaded.maps {
        let expected = (lm.grid.width() as usize) * (lm.grid.height() as usize);
        if lm.grid.cells().len() != expected {
            errors.push(ValidationError {
                path: format!("{map_path}:terrain"),
                reason: format!(
                    "terrain cells 长度 = {}，期望 = {}（grid {}×{}）",
                    lm.grid.cells().len(),
                    expected,
                    lm.grid.width(),
                    lm.grid.height()
                ),
            });
        }
    }

    errors
}

fn check_path(path: &str, field: &str, errors: &mut Vec<ValidationError>) {
    if path.is_empty() {
        errors.push(ValidationError {
            path: field.into(),
            reason: "空字符串".into(),
        });
        return;
    }
    let is_absolute = path.starts_with('/')
        || path.starts_with("./")
        || path.contains(":\\")
        || path.contains(":/")
        || (path.len() >= 2 && path.as_bytes()[1] == b':');
    if is_absolute {
        errors.push(ValidationError {
            path: field.into(),
            reason: format!("禁止绝对路径: {path}"),
        });
    }
    if path.split('/').any(|s| s == "..") {
        errors.push(ValidationError {
            path: field.into(),
            reason: format!("禁止 '..' 越界: {path}"),
        });
    }
}

fn check_unique(
    seen: &mut HashSet<(String, String)>,
    key: (String, String),
    errors: &mut Vec<ValidationError>,
    field: &str,
) {
    if !seen.insert(key.clone()) {
        errors.push(ValidationError {
            path: format!("{field}[id={}]", key.1),
            reason: format!("ID 与已有 {key:?} 冲突"),
        });
    }
}

fn check_unique_str(
    seen: &mut HashSet<String>,
    id: String,
    errors: &mut Vec<ValidationError>,
    field: &str,
) {
    if id.is_empty() {
        errors.push(ValidationError {
            path: field.into(),
            reason: "ID 为空字符串".into(),
        });
        return;
    }
    if !seen.insert(id.clone()) {
        errors.push(ValidationError {
            path: field.into(),
            reason: format!("ID '{id}' 已存在，跨类型不能重名"),
        });
    }
}

fn check_ref_exists(
    target: &str,
    field: &str,
    loaded: &LoadedProject,
    errors: &mut Vec<ValidationError>,
) {
    if target.is_empty() {
        errors.push(ValidationError {
            path: field.into(),
            reason: "空字符串".into(),
        });
        return;
    }
    if !loaded.maps.contains_key(target) {
        errors.push(ValidationError {
            path: field.into(),
            reason: format!("目标不可达: {target}"),
        });
    }
}

/// sparse 编码：(col, row) 必须在 `[0, width) × [0, height)`，且 (col, row) 不重复。
fn check_sparse_cells(
    cells: &[serde_yaml::Value],
    width: u32,
    height: u32,
    field: &str,
    errors: &mut Vec<ValidationError>,
) {
    let mut seen: HashSet<(u32, u32)> = HashSet::new();
    for (i, raw) in cells.iter().enumerate() {
        // 仅解析 `{col, row, ...}` 形态；解析失败不阻塞（其它规则兜底）
        let parsed: Result<SparseCellPartial, _> = serde_yaml::from_value(raw.clone());
        match parsed {
            Ok(cell) => {
                if cell.col >= width || cell.row >= height {
                    errors.push(ValidationError {
                        path: format!("{field}[{i}]"),
                        reason: format!(
                            "坐标 ({}, {}) 越界 grid ({}x{})",
                            cell.col, cell.row, width, height
                        ),
                    });
                    continue;
                }
                if !seen.insert((cell.col, cell.row)) {
                    errors.push(ValidationError {
                        path: format!("{field}[{i}]"),
                        reason: format!("重复坐标 ({}, {})", cell.col, cell.row),
                    });
                }
            }
            Err(_) => {
                errors.push(ValidationError {
                    path: format!("{field}[{i}]"),
                    reason: "无法解析为 sparse cell 形态（需要 col/row 字段）".into(),
                });
            }
        }
    }
}

#[derive(Debug, Deserialize)]
#[allow(dead_code)]
struct SparseCellPartial {
    col: u32,
    row: u32,
    #[serde(default)]
    r#type: Option<String>,
    #[serde(default)]
    move_cost: Option<i32>,
    #[serde(default)]
    buildable: Option<bool>,
}

/// 从文件路径提取可能的 ID（去掉扩展名与目录）。
fn path_stem(path: &str) -> Option<String> {
    let stem = path.rsplit_once('/').map(|(_, n)| n).unwrap_or(path);
    let stem = stem.rsplit_once('.').map(|(n, _)| n).unwrap_or(stem);
    if stem.is_empty() {
        None
    } else {
        Some(stem.to_string())
    }
}

// TerrainGrid 投影方法，供校验器读 map/view 类型
impl TerrainGrid {
    pub fn width(&self) -> u32 {
        self.width
    }
    pub fn height(&self) -> u32 {
        self.height
    }
}

// 引用类型标记为「已使用」
#[allow(dead_code)]
fn _types_used(_: CityRef, _: BuildingRef, _: TerrainType, _: TerrainCell) {}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::model::{Era, LoadedMap, MapGrid, MapTime, MapType};
    use crate::terrain::{TerrainCell, TerrainGrid};

    fn sample_project() -> Project {
        Project {
            project_version: "0.1".into(),
            name: "示例".into(),
            author: None,
            description: None,
            era: Era {
                name: "历".into(),
                start_year: 1,
                months_per_year: 12,
                days_per_month: vec![30; 12],
                hours_per_day: 24,
                minutes_per_hour: 60,
                seconds_per_minute: 60,
                leap: None,
            },
            default_time: crate::model::TimePoint {
                era: None,
                year: 1,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
            },
            map_index: vec!["maps/world/main.yaml".into()],
            character_index: vec![],
            timeline: None,
            settings: None,
        }
    }

    fn sample_world_map(id: &str) -> crate::model::Map {
        crate::model::Map {
            id: id.into(),
            name: id.into(),
            r#type: MapType::World,
            parent_map: None,
            child_maps: vec![],
            grid: MapGrid {
                width: 2,
                height: 2,
                cell_distance: 10.0,
                distance_unit: "km".into(),
                base_unit: "m".into(),
                height_unit: None,
                origin: crate::model::GridOrigin { x: 0.0, y: 0.0 },
                show: true,
                snap: true,
                scale_label: None,
            },
            time: MapTime {
                default_granularity: "month".into(),
                min_granularity: "day".into(),
                display_format: None,
                event_sort_unit: None,
                inherit_calendar: true,
                allow_fuzzy: true,
            },
            layers: vec![],
            terrain_data: None,
            height_data: None,
            city_refs: vec![],
            building_refs: vec![],
            presentation: None,
            travel: None,
        }
    }

    #[test]
    fn validate_project_version() {
        let _project = sample_project();
        let mut bad_version = bad_version();
        bad_version.project_version = "99.0".into();
        let errs = validate(&bad_version);
        assert!(errs.iter().any(|e| e.path.starts_with("project.project_version")));
    }

    fn bad_version() -> Project {
        let mut p = sample_project();
        p.project_version = "0.2".into();
        p
    }

    #[test]
    fn validate_rejects_absolute_and_dotdot() {
        let mut project = sample_project();
        project.map_index = vec!["/abs.yaml".into(), "../escape.yaml".into()];
        let errs = validate(&project);
        assert!(errs.iter().any(|e| e.reason.contains("绝对路径")));
        assert!(errs.iter().any(|e| e.reason.contains("越界")));
    }

    fn loaded_with_world(path: &str, world: crate::model::Map, grid: TerrainGrid) -> LoadedProject {
        let mut project = sample_project();
        project.map_index = vec![path.into()];
        let mut maps = std::collections::HashMap::new();
        maps.insert(
            path.into(),
            LoadedMap {
                path: path.into(),
                map: world,
                grid,
            },
        );
        LoadedProject { project, maps }
    }

    #[test]
    fn validate_loaded_grid_dim_mismatch() {
        let world_path = "maps/wrong.yaml";
        let bad =
            TerrainGrid::from_raw(2, 2, TerrainCell::plain_default(), vec![TerrainCell::plain_default(); 3]);
        let lp = loaded_with_world(world_path, sample_world_map("wrong"), bad);
        let errs = validate_loaded(&lp);
        assert!(errs.iter().any(|e| e.reason.contains("terrain cells")));
    }

    #[test]
    fn validate_loaded_city_ref_unreachable() {
        let world_path = "maps/world/main.yaml";
        let mut world = sample_world_map("main");
        world.city_refs.push(crate::model::CityRef {
            id: "city_x".into(),
            name: "X 城".into(),
            x: 1.0,
            y: 1.0,
            entry: None,
            map_ref: Some("maps/city/missing.yaml".into()),
            population: None,
            faction: None,
            notes: None,
        });

        let lp = loaded_with_world(world_path, world, TerrainGrid::new(2, 2, TerrainCell::plain_default()));
        let errs = validate_loaded(&lp);
        assert!(
            errs.iter().any(|e| e.reason.contains("目标不可达")),
            "expected '目标不可达' in {errs:?}"
        );
    }

    #[test]
    fn validate_loaded_city_ref_empty_is_ok() {
        let world_path = "maps/world/main.yaml";
        let mut world = sample_world_map("main");
        world.city_refs.push(crate::model::CityRef {
            id: "city_x".into(),
            name: "X 城".into(),
            x: 1.0,
            y: 1.0,
            entry: None,
            map_ref: None,
            population: None,
            faction: None,
            notes: None,
        });

        let lp = loaded_with_world(world_path, world, TerrainGrid::new(2, 2, TerrainCell::plain_default()));
        let errs = validate_loaded(&lp);
        assert!(
            !errs.iter().any(|e| e.reason.contains("目标不可达")),
            "空 map_ref 应为合法，错误: {errs:?}"
        );
    }

    #[test]
    fn validate_loaded_id_unique_across_types() {
        // Map.id = "main" 与 character_index[0] path stem = "main" 重名 → 应报冲突
        let mut project = sample_project();
        project.map_index = vec!["maps/world/main.yaml".into()];
        project.character_index = vec!["characters/main.yaml".into()];

        let lp = LoadedProject {
            project,
            maps: [(
                "maps/world/main.yaml".into(),
                LoadedMap {
                    path: "maps/world/main.yaml".into(),
                    map: sample_world_map("main"),
                    grid: TerrainGrid::new(2, 2, TerrainCell::plain_default()),
                },
            )]
            .into_iter()
            .collect(),
        };

        let errs = validate_loaded(&lp);
        assert!(
            errs.iter().any(|e| e.reason.contains("跨类型不能重名")),
            "expected ID 冲突错误，errs: {errs:?}"
        );
    }

    #[test]
    fn validate_loaded_building_interior_ref_unreachable() {
        // 在 city 地图上挂一栋 building，interior_ref 指向不存在的 building 地图
        let city_path = "maps/city/port_aurora.yaml";
        let mut city = sample_world_map("port_aurora");
        city.r#type = MapType::City;
        city.building_refs.push(crate::model::BuildingRef {
            id: "bld_a".into(),
            name: "A".into(),
            r#type: "住宅".into(),
            geometry: crate::model::BuildingGeometry { x: 0.0, y: 0.0, w: 1.0, h: 1.0 },
            rotation: 0.0,
            floors: 1,
            entrance: vec![],
            interior_ref: Some("maps/building/missing.yaml".into()),
            faction: None,
            owner: None,
            state: "intact".into(),
            custom_attributes: None,
        });

        let mut project = sample_project();
        project.map_index = vec![city_path.into()];
        let lp = LoadedProject {
            project,
            maps: [(
                city_path.into(),
                LoadedMap {
                    path: city_path.into(),
                    map: city,
                    grid: TerrainGrid::new(2, 2, TerrainCell::plain_default()),
                },
            )]
            .into_iter()
            .collect(),
        };
        let errs = validate_loaded(&lp);
        assert!(
            errs.iter().any(|e| e.reason.contains("目标不可达")),
            "expected interior_ref 不可达错误，errs: {errs:?}"
        );
    }

    #[test]
    fn validate_sparse_out_of_range_and_duplicate() {
        // 故意构造：sparse cells 含 1 个 (5, 5) 越界 + 1 个 (0, 0) 重复
        let cells = vec![
            serde_yaml::to_value(serde_yaml::Mapping::from_iter([
                ("col".into(), serde_yaml::Value::from(0u32)),
                ("row".into(), serde_yaml::Value::from(0u32)),
                ("type".into(), serde_yaml::Value::from("mountain")),
            ]))
            .unwrap(),
            serde_yaml::to_value(serde_yaml::Mapping::from_iter([
                ("col".into(), serde_yaml::Value::from(0u32)),
                ("row".into(), serde_yaml::Value::from(0u32)),
                ("type".into(), serde_yaml::Value::from("plain")),
            ]))
            .unwrap(),
            serde_yaml::to_value(serde_yaml::Mapping::from_iter([
                ("col".into(), serde_yaml::Value::from(5u32)),
                ("row".into(), serde_yaml::Value::from(5u32)),
                ("type".into(), serde_yaml::Value::from("plain")),
            ]))
            .unwrap(),
        ];
        let mut errs = Vec::new();
        check_sparse_cells(&cells, 2, 2, "test", &mut errs);
        assert!(errs.iter().any(|e| e.reason.contains("重复坐标")));
        assert!(errs.iter().any(|e| e.reason.contains("越界")));
    }
}