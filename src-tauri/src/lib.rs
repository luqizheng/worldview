//! Tauri 主进程入口。
//!
//! 本 crate **只负责**：
//! - 把 `world-view-io` 的纯业务函数包成 Tauri `#[tauri::command]`；
//! - 注册 `tauri::generate_handler!`；
//! - 启动 builder。
//!
//! 业务逻辑（layout、save、create_city_map）一律在 `world-view-io` 中；本文件不持有状态。

use serde::{Deserialize, Serialize};
use world_view_io::{create_city_map, load_project, save_project, IoError};
use world_view_core::model::{LoadedProject, Map, Project};

// ====== 命令入参 / 出参 ======

#[derive(Debug, Serialize, Deserialize)]
pub struct LoadedProjectDto {
    pub project: Project,
    /// 简化表示：MVP 仅序列化 Project；maps 列表供前端 `switchMap` 索引
    pub maps: Vec<MapSummary>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct MapSummary {
    pub path: String,
    pub id: String,
    pub name: String,
    pub r#type: String,
}

impl From<&world_view_core::model::LoadedMap> for MapSummary {
    fn from(lm: &world_view_core::model::LoadedMap) -> Self {
        Self {
            path: lm.path.clone(),
            id: lm.map.id.clone(),
            name: lm.map.name.clone(),
            r#type: lm.map.r#type.as_str().to_string(),
        }
    }
}

#[derive(Debug, Serialize)]
pub struct CommandError {
    pub kind: String,
    pub path: Option<String>,
    pub cause: String,
}

impl From<IoError> for CommandError {
    fn from(e: IoError) -> Self {
        let (kind, path) = match &e {
            IoError::ProjectNotFound { path } => ("ProjectNotFound", Some(path.clone())),
            IoError::ProjectYamlParse { path, .. } => ("ProjectYamlParse", Some(path.clone())),
            IoError::MapLoadFailed { path, .. } => ("MapLoadFailed", Some(path.clone())),
            IoError::CreateCityFailed { path, .. } => ("CreateCityFailed", Some(path.clone())),
        };
        Self {
            kind: kind.into(),
            path,
            cause: e.to_string(),
        }
    }
}

// ====== Tauri commands ======

/// 加载工程目录。前端 await 后拿到 `LoadedProjectDto`。
#[tauri::command]
fn open_project(dir: String) -> Result<LoadedProjectDto, CommandError> {
    let path = std::path::PathBuf::from(dir);
    let loaded: LoadedProject = load_project(&path).map_err(CommandError::from)?;
    let dto = LoadedProjectDto {
        project: loaded.project,
        maps: loaded.maps.values().map(MapSummary::from).collect(),
    };
    Ok(dto)
}

/// 保存工程。前端拿整个 LoadedProject（from previous open + edits）传回。
/// MVP 简化：客户端把 LoadedProject 完整传回；服务端再加载一次 disk 上的 map 合并后落盘。
///
/// 注意：本命令 MVP 仅校验 → 写盘；前端调参用 `LoadedProjectDto`。
#[tauri::command]
fn save_project(dir: String, project: Project) -> Result<(), Vec<world_view_core::validate::ValidationError>> {
    let path = std::path::PathBuf::from(dir);
    // 重新加载得到 LoadedMap（含已编辑地形），与 caller 的 project 合并
    let mut loaded = load_project(&path).map_err(|e| {
        vec![world_view_core::validate::ValidationError {
            path: "project.yaml".into(),
            reason: e.to_string(),
        }]
    })?;
    loaded.project = project;
    save_project(&path, &loaded)
}

/// 在指定工程目录下创建城市地图骨架。
///
/// 返回新建的 `Map` + 同步更新的 `project.map_index` 路径列表。
#[tauri::command]
fn create_city_map(
    dir: String,
    mut project: Project,
    parent_world_path: String,
    city_id: String,
    city_name: String,
    parent_city_id: String,
) -> Result<Map, CommandError> {
    let path = std::path::PathBuf::from(dir);
    let map = create_city_map(
        &path,
        &mut project,
        &parent_world_path,
        &city_id,
        &city_name,
        &parent_city_id,
    )
    .map_err(CommandError::from)?;
    // 同步落盘 project.yaml（map_index 已更新）
    if let Err(e) = save_project_partial(&path, &project) {
        return Err(CommandError::from(e));
    }
    Ok(map)
}

fn save_project_partial(
    dir: &std::path::Path,
    project: &Project,
) -> Result<(), IoError> {
    let yaml = serde_yaml::to_string(project).expect("Project 序列化不应失败");
    std::fs::write(dir.join("project.yaml"), yaml).map_err(|e| IoError::ProjectYamlParse {
        path: "project.yaml".into(),
        cause: e.to_string(),
    })?;
    Ok(())
}

// ====== 入口 ======

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            open_project,
            save_project,
            create_city_map,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}