//! World View 领域层。
//!
//! 本 crate **不接触文件系统、不调用 GUI**：仅做数据模型定义、YAML/二进制序列化与校验器。
//! 字段以 `docs/数据模型规格_v0.1.md` 为唯一真源；MVP 阶段只实现 §1 §2 §3 子集。

#![forbid(unsafe_code)]

pub mod error;
pub mod model;
pub mod terrain;
pub mod validate;

pub use error::{CoreError, CoreResult};
pub use model::{Layer, Map, Project};
pub use terrain::{TerrainCell, TerrainGrid, TerrainType};