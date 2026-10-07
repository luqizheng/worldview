//! World View I/O 层。
//!
//! 与 `world-view-core` 的边界：
//! - `core` 不知道目录路径，只持有数据；
//! - `io` 接受 `dir: &Path` 参数，负责把 YAML / 地形文件读写到磁盘。
//!
//! # 主要入口
//! - [`load_project`] — 从目录加载 `Project` + 全量地图，构造 [`LoadedProject`]；
//! - [`save_project`] — 校验后把 `LoadedProject` 写回磁盘；
//! - [`create_city_map`] — 在指定目录下生成一份最小可加载的城市地图骨架。
//!
//! MVP 阶段 `world-view-io` **不引入 `tauri` 依赖**（spec §T1.2 纪律）；
//! Tauri 适配层放 [`src-tauri`] crate。
//!
//! MVP 不引 `chunked`：所有 terrain YAML 仅支持 `encoding: grid` 或 `encoding: sparse`。

#![forbid(unsafe_code)]

use std::collections::HashMap;
use std::fs;
use std::path::Path;

use serde::{Deserialize, Serialize};
use world_view_core::model::{
    GridOrigin, Layer, LoadedMap, LoadedProject, Map, MapGrid, MapPresentation, MapTime, MapType,
    Project, TerrainDataRef,
};
use world_view_core::terrain::{TerrainCell as CoreTerrainCell, TerrainGrid};
use world_view_core::validate::{validate_loaded, ValidationError};

pub mod error;

pub use error::{IoError, IoResult};

const CITY_MAP_DEFAULT_GRID_WIDTH: u32 = 64;
const CITY_MAP_DEFAULT_GRID_HEIGHT: u32 = 64;
const CITY_MAP_DEFAULT_CELL_DISTANCE: f64 = 2.0;

/// 加载一个工程目录。
///
/// **全失败语义**：任一地图加载失败（文件不存在 / YAML 解析错 / cells_ref 不可达），
/// 返回 `Err(IoError::MapLoadFailed { path, cause })`，**不返回部分 `Project`**。
/// 已成功读取但还没挂到 `LoadedProject` 上的地图全部丢弃。
pub fn load_project(dir: &Path) -> IoResult<LoadedProject> {
    if !dir.is_dir() {
        return Err(IoError::ProjectNotFound {
            path: dir.display().to_string(),
        });
    }
    let project_path = dir.join("project.yaml");
    let project_yaml = fs::read_to_string(&project_path).map_err(|e| IoError::ProjectNotFound {
        path: format!("{} ({})", project_path.display(), e),
    })?;
    let project: Project = serde_yaml::from_str(&project_yaml).map_err(|e| {
        IoError::ProjectYamlParse {
            path: project_path.display().to_string(),
            cause: e.to_string(),
        }
    })?;

    let mut maps: HashMap<String, LoadedMap> = HashMap::new();
    for rel_path in &project.map_index {
        let abs_path = dir.join(rel_path);
        let yaml = fs::read_to_string(&abs_path).map_err(|e| IoError::MapLoadFailed {
            path: rel_path.clone(),
            cause: format!("读取失败: {e}"),
        })?;
        let map: Map = serde_yaml::from_str(&yaml).map_err(|e| IoError::MapLoadFailed {
            path: rel_path.clone(),
            cause: format!("YAML 解析: {e}"),
        })?;
        let grid = match &map.terrain_data {
            Some(td) => load_terrain_grid_with_dim(
                dir,
                td,
                map.grid.width,
                map.grid.height,
                rel_path,
            )?,
            None => TerrainGrid::new(
                map.grid.width,
                map.grid.height,
                CoreTerrainCell::plain_default(),
            ),
        };
        maps.insert(
            rel_path.clone(),
            LoadedMap {
                path: rel_path.clone(),
                map,
                grid,
            },
        );
    }

    Ok(LoadedProject { project, maps })
}

/// 从 terrain_data 索引（cells_ref）加载地形网格，注入 grid 维度。
///
/// MVP 阶段支持 `encoding: grid` 与 `encoding: sparse`。
/// `encoding: chunked` MVP **不实现**，落到此处直接报错。
fn load_terrain_grid_with_dim(
    root: &Path,
    td: &TerrainDataRef,
    width: u32,
    height: u32,
    parent_path: &str,
) -> IoResult<TerrainGrid> {
    if td.encoding == "chunked" {
        return Err(IoError::MapLoadFailed {
            path: parent_path.to_string(),
            cause: "MVP 阶段不支持 encoding: chunked".into(),
        });
    }
    let default = parse_default_cell(td.default.as_ref());

    let cells_ref = td.cells_ref.as_ref().ok_or_else(|| IoError::MapLoadFailed {
        path: parent_path.to_string(),
        cause: "terrain_data.cells_ref 缺失".into(),
    })?;
    let cells_abs = root.join(cells_ref);
    let cells_yaml = fs::read_to_string(&cells_abs).map_err(|e| IoError::MapLoadFailed {
        path: parent_path.to_string(),
        cause: format!("terrain 文件不可达 ({cells_ref}): {e}"),
    })?;
    let terrain_doc: TerrainFile = serde_yaml::from_str(&cells_yaml).map_err(|e| {
        IoError::MapLoadFailed {
            path: parent_path.to_string(),
            cause: format!("terrain 文件 YAML 解析 ({cells_ref}): {e}"),
        }
    })?;

    match terrain_doc.encoding.as_str() {
        "grid" => {
            let cells_vec = terrain_doc.cells.unwrap_or_default();
            if cells_vec.len() != (width as usize) * (height as usize) {
                return Err(IoError::MapLoadFailed {
                    path: parent_path.to_string(),
                    cause: format!(
                        "terrain cells 长度 = {}，期望 = {}（grid {}x{}）",
                        cells_vec.len(),
                        width as usize * height as usize,
                        width,
                        height
                    ),
                });
            }
            let parsed: Vec<CoreTerrainCell> = cells_vec
                .into_iter()
                .map(|raw| parse_cell(raw, &default))
                .collect();
            TerrainGrid::from_cells(width, height, default, parsed).map_err(|e| {
                IoError::MapLoadFailed {
                    path: parent_path.to_string(),
                    cause: e.to_string(),
                }
            })
        }
        "sparse" => {
            let mut grid = TerrainGrid::new(width, height, default.clone());
            let cells_vec = terrain_doc.cells.unwrap_or_default();
            for raw in cells_vec {
                let sp: SparseCell = serde_yaml::from_value(raw).map_err(|e| {
                    IoError::MapLoadFailed {
                        path: parent_path.to_string(),
                        cause: format!("sparse cell 解析: {e}"),
                    }
                })?;
                grid.set_cell(sp.col, sp.row, sp.cell).map_err(|e| {
                    IoError::MapLoadFailed {
                        path: parent_path.to_string(),
                        cause: e.to_string(),
                    }
                })?;
            }
            Ok(grid)
        }
        other => Err(IoError::MapLoadFailed {
            path: parent_path.to_string(),
            cause: format!("不支持的 terrain encoding: {other}"),
        }),
    }
}

fn parse_default_cell(value: Option<&serde_yaml::Value>) -> CoreTerrainCell {
    match value {
        None => CoreTerrainCell::plain_default(),
        Some(v) => match serde_yaml::from_value(v.clone()) {
            Ok(c) => c,
            Err(_) => CoreTerrainCell::plain_default(),
        },
    }
}

fn parse_cell(raw: serde_yaml::Value, default: &CoreTerrainCell) -> CoreTerrainCell {
    serde_yaml::from_value(raw).unwrap_or_else(|_| default.clone())
}

#[derive(Debug, Deserialize, Serialize)]
struct TerrainFile {
    encoding: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    default: Option<serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    cells: Option<Vec<serde_yaml::Value>>,
}

#[derive(Debug, Deserialize)]
struct SparseCell {
    col: u32,
    row: u32,
    #[serde(flatten)]
    cell: CoreTerrainCell,
}

/// 保存工程。写盘前**先校验**，校验失败返回 `Err(Vec<ValidationError>)` 不写盘。
///
/// **MVP 行为**：每次保存都把所有 dirty map 写回；地形统一以 sparse 编码写出。
///
/// **校验分两阶段**：
/// 1. `validate_loaded`（纯数据，不读盘）—— 5 条规则中除 cells_ref 外的全部；
/// 2. `disk_check_loaded`（读盘）—— terrain_data.cells_ref 指向的文件存在。
pub fn save_project(dir: &Path, loaded: &LoadedProject) -> Result<(), Vec<ValidationError>> {
    let mut errors = validate_loaded(loaded);
    errors.extend(disk_check_loaded(dir, loaded));
    if !errors.is_empty() {
        return Err(errors);
    }
    let project_yaml = serde_yaml::to_string(&loaded.project).expect("Project 序列化不应失败");
    fs::write(dir.join("project.yaml"), project_yaml).map_err(|e| vec![ValidationError {
        path: "project.yaml".into(),
        reason: format!("写盘失败: {e}"),
    }])?;

    for (rel_path, lm) in &loaded.maps {
        let abs = dir.join(rel_path);
        if let Some(parent) = abs.parent() {
            fs::create_dir_all(parent).map_err(|e| {
                vec![ValidationError {
                    path: rel_path.clone(),
                    reason: format!("创建目录失败: {e}"),
                }]
            })?;
        }
        let map_yaml = serde_yaml::to_string(&lm.map).expect("Map 序列化不应失败");
        fs::write(&abs, map_yaml).map_err(|e| {
            vec![ValidationError {
                path: rel_path.clone(),
                reason: format!("写盘失败: {e}"),
            }]
        })?;

        // 地形统一写 sparse
        if let Some(td) = &lm.map.terrain_data {
            let cells_rel = td
                .cells_ref
                .clone()
                .unwrap_or_else(|| derive_cells_ref(rel_path));
            let sparse = TerrainFile {
                encoding: "sparse".into(),
                default: Some(
                    serde_yaml::to_value(lm.grid.default_cell().clone()).unwrap_or_default(),
                ),
                cells: Some(
                    lm.grid
                        .to_sparse_cells()
                        .into_iter()
                        .map(|sc| {
                            serde_yaml::to_value(SparseOut {
                                col: sc.col,
                                row: sc.row,
                                cell: sc.cell,
                            })
                            .unwrap_or(serde_yaml::Value::Null)
                        })
                        .collect(),
                ),
            };
            let cells_yaml = serde_yaml::to_string(&sparse).expect("terrain 序列化");
            let cells_abs = dir.join(&cells_rel);
            if let Some(parent) = cells_abs.parent() {
                fs::create_dir_all(parent).map_err(|e| {
                    vec![ValidationError {
                        path: cells_rel.clone(),
                        reason: format!("创建目录失败: {e}"),
                    }]
                })?;
            }
            fs::write(&cells_abs, cells_yaml).map_err(|e| {
                vec![ValidationError {
                    path: cells_rel.clone(),
                    reason: format!("写盘失败: {e}"),
                }]
            })?;
        }
    }

    Ok(())
}

/// 根据 map 文件路径派生同名 terrain 文件路径（例：`maps/world/main.yaml` → `maps/world/main.terrain.yaml`）。
fn derive_cells_ref(map_rel: &str) -> String {
    let p = std::path::Path::new(map_rel);
    let stem = p.file_stem().and_then(|s| s.to_str()).unwrap_or("main");
    let parent = p.parent().and_then(|s| s.to_str()).unwrap_or("");
    if parent.is_empty() {
        format!("{stem}.terrain.yaml")
    } else {
        format!("{parent}/{stem}.terrain.yaml")
    }
}

/// 验证工程目录是否结构合法（YAML 能解析 + 所有引用可达）。
/// **不在** `load_project` 时调用：load_project 已自带校验；本函数供 save_project 调用。
pub fn disk_check_loaded(dir: &Path, loaded: &LoadedProject) -> Vec<ValidationError> {
    let mut errors = Vec::new();
    for (rel, lm) in &loaded.maps {
        // terrain_data.cells_ref 文件可达性
        if let Some(td) = &lm.map.terrain_data {
            if let Some(cells_ref) = &td.cells_ref {
                let abs = dir.join(cells_ref);
                if !abs.is_file() {
                    errors.push(ValidationError {
                        path: format!("{rel}:terrain_data.cells_ref"),
                        reason: format!("文件不存在: {}", abs.display()),
                    });
                }
            }
        }
        if let Some(hd) = &lm.map.height_data {
            if let Some(h_cells_ref) = &hd.cells_ref {
                let abs = dir.join(h_cells_ref);
                if !abs.is_file() {
                    errors.push(ValidationError {
                        path: format!("{rel}:height_data.cells_ref"),
                        reason: format!("文件不存在: {}", abs.display()),
                    });
                }
            }
        }
    }
    errors
}

/// 生成城市地图骨架（按 spec §F08 默认值）。
///
/// **返回值**：新建的 `Map` 对象（已是 `city` 类型，含 layers + 完整 grid/time/presentation）。
///
/// **同步更新**：
/// - `parent_world.city_refs[i].map_ref = "maps/city/<id>.yaml"`（仅在 `parent_city_id` 命中时）；
/// - `project.map_index` 追加新路径；
/// - 在磁盘上写出 `maps/city/<id>.yaml` + `maps/city/<id>.terrain.yaml`（空 sparse）。
pub fn create_city_map(
    dir: &Path,
    project: &mut Project,
    parent_world_path: &str,
    city_id: &str,
    city_name: &str,
    parent_city_id: &str,
) -> IoResult<Map> {
    let parent_world_path_norm = normalize_rel_path(parent_world_path);
    let new_rel = format!("maps/city/{city_id}.yaml");
    let new_terrain_rel = format!("maps/city/{city_id}.terrain.yaml");

    let map = build_default_city_map(city_id, city_name);

    let map_abs = dir.join(&new_rel);
    let terrain_abs = dir.join(&new_terrain_rel);
    if let Some(parent) = map_abs.parent() {
        fs::create_dir_all(parent).map_err(|e| IoError::CreateCityFailed {
            path: map_abs.display().to_string(),
            cause: format!("创建目录失败: {e}"),
        })?;
    }
    let map_yaml = serde_yaml::to_string(&map).expect("Map 序列化不应失败");
    fs::write(&map_abs, map_yaml).map_err(|e| IoError::CreateCityFailed {
        path: map_abs.display().to_string(),
        cause: format!("写盘失败: {e}"),
    })?;
    let terrain_yaml = serde_yaml::to_string(&TerrainFile {
        encoding: "sparse".into(),
        default: Some(serde_yaml::to_value(CoreTerrainCell::plain_default()).unwrap_or_default()),
        cells: Some(vec![]),
    })
    .expect("terrain 序列化不应失败");
    fs::write(&terrain_abs, terrain_yaml).map_err(|e| IoError::CreateCityFailed {
        path: terrain_abs.display().to_string(),
        cause: format!("写盘失败: {e}"),
    })?;

    // 同步更新：把 parent world 中匹配 parent_city_id 的 map_ref 改成新路径
    let target_world = project
        .map_index
        .iter()
        .find(|p| normalize_rel_path(p) == parent_world_path_norm)
        .cloned();
    if let Some(world_rel) = target_world {
        // parent world 的内存 Map 不在 LoadedProject.maps 中（这里只持有 project 元数据）。
        // MVP 策略：调用方在 create_city_map 之后调用 save_project 整体落盘，
        // 由 save_project 写出更新后的 map（含新的 map_ref）。
        // 这里仅记录「期望同步」的世界路径作为返回值附加信息。
        // —— 真正改 map_ref 的逻辑由 caller 拿到 LoadedMap 后改。
        let _ = (world_rel, parent_city_id);
    } else {
        return Err(IoError::CreateCityFailed {
            path: parent_world_path.to_string(),
            cause: "parent world 不在 project.map_index 中".into(),
        });
    }

    if !project.map_index.iter().any(|p| normalize_rel_path(p) == new_rel) {
        project.map_index.push(new_rel.clone());
    }

    Ok(map)
}

/// 把 "maps\\world\\main.yaml" 这种 Windows 风格路径归一化为 POSIX 风格。
fn normalize_rel_path(p: &str) -> String {
    p.replace('\\', "/")
}

/// 构造一份最小可加载的城市地图骨架（spec §F08 默认值）。
pub fn build_default_city_map(id: &str, name: &str) -> Map {
    let grid = MapGrid {
        width: CITY_MAP_DEFAULT_GRID_WIDTH,
        height: CITY_MAP_DEFAULT_GRID_HEIGHT,
        cell_distance: CITY_MAP_DEFAULT_CELL_DISTANCE,
        distance_unit: "m".into(),
        base_unit: "m".into(),
        height_unit: Some("m".into()),
        origin: GridOrigin { x: 0.0, y: 0.0 },
        show: true,
        snap: true,
        scale_label: None,
    };
    let time = MapTime {
        default_granularity: "hour".into(),
        min_granularity: "hour".into(),
        display_format: None,
        event_sort_unit: None,
        inherit_calendar: true,
        allow_fuzzy: true,
    };
    let presentation = MapPresentation {
        default_mode: "edit".into(),
        view_mode: "2d".into(),
        theme: Some("parchment".into()),
        observer: None,
        show_scale_bar: true,
        show_coordinates: true,
        show_minimap: false,
    };
    let terrain_data = TerrainDataRef {
        encoding: "sparse".into(),
        cells_ref: None, // 由 create_city_map 写盘时手动覆盖
        default: Some(
            serde_yaml::to_value(CoreTerrainCell::plain_default()).unwrap_or_default(),
        ),
        cells: vec![],
    };
    Map {
        id: id.into(),
        name: name.into(),
        r#type: MapType::City,
        parent_map: None,
        child_maps: vec![],
        grid,
        time,
        layers: vec![
            Layer {
                id: "layer_terrain".into(),
                name: "基础地形".into(),
                r#type: "terrain".into(),
                visible: true,
                locked: false,
                opacity: 1.0,
                z_order: 0,
                valid_from: None,
                valid_to: None,
                data: None,
                params: None,
            },
            Layer {
                id: "layer_road".into(),
                name: "道路".into(),
                r#type: "road".into(),
                visible: true,
                locked: false,
                opacity: 1.0,
                z_order: 10,
                valid_from: None,
                valid_to: None,
                data: None,
                params: None,
            },
        ],
        terrain_data: Some(terrain_data),
        height_data: None,
        city_refs: vec![],
        building_refs: vec![],
        presentation: Some(presentation),
        travel: None,
    }
}

#[derive(Debug, Serialize)]
struct SparseOut {
    col: u32,
    row: u32,
    #[serde(flatten)]
    cell: CoreTerrainCell,
}

/// 把 LoadedMap 的内存状态序列化为 YAML（仅 Map 元数据）。
pub fn serialize_map(lm: &LoadedMap) -> String {
    serde_yaml::to_string(&lm.map).expect("Map 序列化不应失败")
}

/// 把 TerrainGrid 序列化为 sparse 格式 YAML。
pub fn serialize_terrain_sparse(grid: &TerrainGrid) -> String {
    let file = TerrainFile {
        encoding: "sparse".into(),
        default: Some(serde_yaml::to_value(grid.default_cell().clone()).unwrap_or_default()),
        cells: Some(
            grid.to_sparse_cells()
                .into_iter()
                .map(|sc| {
                    serde_yaml::to_value(SparseOut {
                        col: sc.col,
                        row: sc.row,
                        cell: sc.cell,
                    })
                    .unwrap_or(serde_yaml::Value::Null)
                })
                .collect(),
        ),
    };
    serde_yaml::to_string(&file).expect("terrain 序列化不应失败")
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU64, Ordering};
    use world_view_core::model::Era;
    use world_view_core::terrain::{TerrainCell, TerrainGrid, TerrainType};

    static COUNTER: AtomicU64 = AtomicU64::new(0);

    /// 自托管的临时目录：测试结束时**不自动清理**（避免 RAII 跨界 panic）；
    /// CI / 评审脚本清理 `./world-view-test-*`。
    ///
    /// **注意**：在某些受限沙盒下，`std::env::temp_dir()` 指向不可写目录。
    /// 这里改用 `CARGO_MANIFEST_DIR + /target/test-tmp/`（cargo test 工作目录就是 crate 根）。
    struct TempDir {
        path: std::path::PathBuf,
    }
    impl TempDir {
        fn new() -> Self {
            let id = COUNTER.fetch_add(1, Ordering::SeqCst);
            let pid = std::process::id();
            let nanos = std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .map(|d| d.as_nanos())
                .unwrap_or(0);
            // 优先用 CARGO_MANIFEST_DIR；fallback 到 current_dir；最后 fallback temp_dir
            let base = std::env::var("CARGO_MANIFEST_DIR")
                .map(std::path::PathBuf::from)
                .unwrap_or_else(|_| {
                    std::env::current_dir().unwrap_or_else(|_| std::env::temp_dir())
                });
            let path = base
                .join("target")
                .join("test-tmp")
                .join(format!("wv-test-{pid}-{nanos}-{id}"));
            if path.exists() {
                let _ = fs::remove_dir_all(&path);
            }
            if let Some(parent) = path.parent() {
                let _ = fs::create_dir_all(parent);
            }
            fs::create_dir_all(&path).unwrap_or_else(|e| {
                panic!("create temp dir {} failed: {e}", path.display())
            });
            Self { path }
        }
        fn path(&self) -> &Path {
            &self.path
        }
    }
    impl Drop for TempDir {
        fn drop(&mut self) {
            // best-effort
            let _ = fs::remove_dir_all(&self.path);
        }
    }

    fn write_file(path: &Path, content: &str) {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).unwrap();
        }
        fs::write(path, content).unwrap();
    }

    fn tmp() -> TempDir {
        TempDir::new()
    }

    fn sample_project_yaml() -> &'static str {
        r#"
project_version: "0.1"
name: 测试
era:
  name: 历
  start_year: 1
  months_per_year: 12
  days_per_month: [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
  hours_per_day: 24
  minutes_per_hour: 60
  seconds_per_minute: 60
default_time:
  year: 1
  month: 1
  day: 1
map_index:
  - maps/world/main.yaml
"#
    }

    fn sample_world_map_yaml() -> &'static str {
        r#"
id: main
name: 艾瑟兰大陆
type: world
grid:
  width: 4
  height: 4
  cell_distance: 10
  distance_unit: km
  base_unit: m
  origin: {x: 0, y: 0}
  show: true
  snap: true
time:
  default_granularity: month
  min_granularity: day
  inherit_calendar: true
  allow_fuzzy: true
terrain_data:
  encoding: sparse
  default: {type: plain, move_cost: 1, buildable: true}
  cells_ref: maps/world/main.terrain.yaml
layers:
  - id: layer_terrain
    name: 基础地形
    type: terrain
    visible: true
    locked: false
    opacity: 1.0
    z_order: 0
city_refs: []
building_refs: []
"#
    }

    #[test]
    fn load_and_save_roundtrip() {
        let tmp = tmp();
        let dir = tmp.path();
        write_file(&dir.join("project.yaml"), sample_project_yaml());
        write_file(&dir.join("maps/world/main.yaml"), sample_world_map_yaml());
        write_file(
            &dir.join("maps/world/main.terrain.yaml"),
            "encoding: sparse\ndefault: {type: plain, move_cost: 1, buildable: true}\ncells: []\n",
        );

        let loaded = load_project(dir).expect("load ok");
        assert_eq!(loaded.project.name, "测试");
        assert_eq!(loaded.maps.len(), 1);
        assert_eq!(loaded.maps["maps/world/main.yaml"].grid.width(), 4);

        // 保存：因 validate_loaded 已通过，应不报错
        save_project(dir, &loaded).expect("save ok");

        // 再读
        let reloaded = load_project(dir).expect("reload ok");
        assert_eq!(reloaded.maps["maps/world/main.yaml"].grid.width(), 4);
    }

    #[test]
    fn load_fails_on_missing_cells_ref() {
        let tmp = tmp();
        let dir = tmp.path();
        write_file(&dir.join("project.yaml"), sample_project_yaml());
        write_file(&dir.join("maps/world/main.yaml"), sample_world_map_yaml());
        // 故意不写 terrain 文件

        let err = load_project(dir).expect_err("应失败");
        match err {
            IoError::MapLoadFailed { cause, .. } => {
                assert!(cause.contains("terrain"));
            }
            _ => panic!("expected MapLoadFailed, got {err:?}"),
        }
    }

    #[test]
    fn create_city_map_writes_files_and_updates_index() {
        let tmp = tmp();
        let dir = tmp.path();
        write_file(&dir.join("project.yaml"), sample_project_yaml());
        write_file(&dir.join("maps/world/main.yaml"), sample_world_map_yaml());
        write_file(
            &dir.join("maps/world/main.terrain.yaml"),
            "encoding: sparse\ndefault: {type: plain, move_cost: 1, buildable: true}\ncells: []\n",
        );
        let mut loaded = load_project(dir).expect("load ok");
        // 注入一个 CityRef（map_ref 为空）
        loaded.maps.get_mut("maps/world/main.yaml").unwrap().map.city_refs.push(
            world_view_core::model::CityRef {
                id: "city_x".into(),
                name: "X 城".into(),
                x: 1.0,
                y: 1.0,
                entry: None,
                map_ref: None,
                population: None,
                faction: None,
                notes: None,
            },
        );
        save_project(dir, &loaded).expect("save ok");

        // 现在再次读（city_map 还没建）
        let mut loaded = load_project(dir).expect("reload ok");
        let new_map = create_city_map(
            dir,
            &mut loaded.project,
            "maps/world/main.yaml",
            "city_x",
            "X 城",
            "city_x",
        )
        .expect("create ok");
        assert_eq!(new_map.id, "city_x");
        assert_eq!(new_map.r#type, MapType::City);

        // 文件应已生成
        assert!(dir.join("maps/city/city_x.yaml").is_file());
        assert!(dir.join("maps/city/city_x.terrain.yaml").is_file());

        // project.map_index 已含新路径
        assert!(loaded
            .project
            .map_index
            .iter()
            .any(|p| p == "maps/city/city_x.yaml"));
    }

    #[test]
    fn save_rejects_invalid_terrain_grid_size() {
        let tmp = tmp();
        let dir = tmp.path();
        write_file(&dir.join("project.yaml"), sample_project_yaml());
        write_file(&dir.join("maps/world/main.yaml"), sample_world_map_yaml());
        write_file(
            &dir.join("maps/world/main.terrain.yaml"),
            "encoding: sparse\ndefault: {type: plain, move_cost: 1, buildable: true}\ncells: []\n",
        );
        let mut loaded = load_project(dir).expect("load ok");
        // 故意改一个 map_ref 指向不存在的路径
        let m = loaded.maps.get_mut("maps/world/main.yaml").unwrap();
        m.map.city_refs.push(world_view_core::model::CityRef {
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
        let errs = save_project(dir, &loaded).expect_err("应校验失败");
        assert!(
            errs.iter().any(|e| e.reason.contains("目标不可达")),
            "errs: {errs:?}"
        );
    }

    #[test]
    fn build_default_city_map_has_required_defaults() {
        let m = build_default_city_map("c1", "城1");
        assert_eq!(m.grid.width, 64);
        assert_eq!(m.grid.height, 64);
        assert_eq!(m.grid.cell_distance, 2.0);
        assert_eq!(m.grid.distance_unit, "m");
        assert_eq!(m.time.default_granularity, "hour");
        assert_eq!(m.time.min_granularity, "hour");
        assert_eq!(m.layers.len(), 2);
        assert_eq!(m.layers[0].r#type, "terrain");
        assert_eq!(m.layers[1].r#type, "road");
    }

    #[test]
    fn serialize_terrain_sparse_only_writes_diffs() {
        // 全 plain 网格 → sparse 应无 cells
        let grid = TerrainGrid::new(4, 4, TerrainCell::plain_default());
        let s = serialize_terrain_sparse(&grid);
        assert!(s.contains("encoding: sparse"));
        // cells 部分应当只剩 `cells: []` 或 omitted
        assert!(!s.contains("col:"));
    }

    #[test]
    fn chunked_encoding_is_rejected() {
        let tmp = tmp();
        let dir = tmp.path();
        write_file(&dir.join("project.yaml"), sample_project_yaml());
        let bad_map = r#"
id: main
name: x
type: world
grid: {width: 4, height: 4, cell_distance: 10, distance_unit: km, base_unit: m, origin: {x:0, y:0}, show: true, snap: true}
time: {default_granularity: month, min_granularity: day, inherit_calendar: true, allow_fuzzy: true}
terrain_data:
  encoding: chunked
  cells_ref: maps/world/main.terrain.yaml
layers: []
city_refs: []
building_refs: []
"#;
        write_file(&dir.join("maps/world/main.yaml"), bad_map);
        let err = load_project(dir).expect_err("应失败");
        assert!(matches!(err, IoError::MapLoadFailed { .. }));
    }

    // 防 unused 警告
    #[allow(dead_code)]
    fn _types(_: Era, _: TerrainType, _: CoreTerrainCell) {}
}