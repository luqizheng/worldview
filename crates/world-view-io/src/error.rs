//! I/O 层统一错误。

use thiserror::Error;

#[derive(Debug, Error)]
pub enum IoError {
    #[error("工程目录不存在: {path}")]
    ProjectNotFound { path: String },

    #[error("project.yaml 解析失败 ({path}): {cause}")]
    ProjectYamlParse { path: String, cause: String },

    #[error("地图加载失败 ({path}): {cause}")]
    MapLoadFailed { path: String, cause: String },

    #[error("创建城市地图失败 ({path}): {cause}")]
    CreateCityFailed { path: String, cause: String },
}

pub type IoResult<T> = Result<T, IoError>;