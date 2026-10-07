//! 领域层统一错误。

use thiserror::Error;

#[derive(Debug, Error)]
pub enum CoreError {
    #[error("YAML 解析错误：{0}")]
    YamlParse(#[from] serde_yaml::Error),

    #[error("YAML 序列化错误：{0}")]
    YamlSerialize(serde_yaml::Error),

    #[error("字段错误：{field} = {value}（{reason}）")]
    Field {
        field: String,
        value: String,
        reason: String,
    },

    #[error("范围错误：{0}")]
    Range(String),

    #[error("编码错误：{0}")]
    Encoding(String),

    #[error("校验错误：{0}")]
    Validation(String),
}

pub type CoreResult<T> = Result<T, CoreError>;