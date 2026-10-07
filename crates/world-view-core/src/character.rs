//! 人物 / 角色数据契约（MVP 阶段：仅作数据契约样例，不进 UI）。
//!
//! 字段定义以 `docs/事件与人物登记规格_v0.1.md` §2 + `docs/数据模型规格_v0.1.md` §6 为唯一真源。
//!
//! MVP 阶段：这些类型仅供 `world-view-core` 单测验证 YAML 解析与跨文件引用格式；
//! UI 不加载、MVP UI store 不持有 Character 实例。
//! 完整人物登记系统在 M5 落地。

use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use crate::model::TimePoint;

/// 人物 / 角色。
///
/// MVP 字段子集：`id` / `name` / `gender` / `race` / `age` / `faction` / `residence` / `attributes` /
/// `faction_memberships` / `relationships` / `notes`。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Character {
    pub id: String,
    pub name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub gender: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub race: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub age: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub faction: Option<String>,
    pub residence: Residence,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub attributes: Vec<CharacterAttribute>,
    #[serde(default, skip_serializing_if = "HashMap::is_empty")]
    pub attributes_raw: HashMap<String, serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub relationships: Vec<Relationship>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub faction_memberships: Vec<FactionMembership>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
}

/// 居住地引用。`map_ref` 走相对工程根路径；`building_ref` 可空（M4 阶段房屋数据上线）。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct Residence {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub map_ref: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub building_ref: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub note: Option<String>,
}

/// 人物属性（基于模板的强类型，或裸字段）。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(untagged)]
pub enum CharacterAttribute {
    /// 基于模板的属性（带 template 引用）
    Templated {
        template: String,
        #[serde(flatten)]
        values: HashMap<String, serde_yaml::Value>,
    },
    /// 裸字段（无 template 引用）
    Bare {
        name: String,
        #[serde(flatten)]
        values: HashMap<String, serde_yaml::Value>,
    },
}

/// 人际关系。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Relationship {
    pub target: String,
    pub kind: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub note: Option<String>,
}

/// 势力成员资格。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct FactionMembership {
    pub faction: String,
    pub rank: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub since: Option<TimePoint>,
}

/// 人物属性模板集合（`characters/_templates.yaml`）。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct CharacterTemplates {
    pub version: String,
    pub templates: Vec<CharacterTemplate>,
}

/// 单条模板。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct CharacterTemplate {
    pub id: String,
    pub name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub schema: Option<serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub defaults: Option<serde_yaml::Value>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub applies_to: Vec<String>,
}

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE_LIN_YU: &str = r#"
id: char_lin_yu
name: 林语
gender: female
race: human
age: 27
faction: fac_merchant_league
residence:
  map_ref: maps/city/port_aurora.yaml
  building_ref: null
  note: 居所：极光港南区渔市坊第 12 号院落。
attributes:
  - template: tpl_hp
    current: 100
    max: 100
    regen_per_day: 1
  - name: 魅力
    value: 78
relationships:
  - target: char_unknown
    kind: 朋友
faction_memberships:
  - faction: fac_merchant_league
    rank: 商人学徒
    since:
      year: 1035
      month: 6
      day: 1
notes: 数据契约样例
"#;

    const SAMPLE_TEMPLATES: &str = r#"
version: "0.1"
templates:
  - id: tpl_hp
    name: 生命值
    description: HP 上下限
    schema:
      type: object
    defaults:
      current: 100
      max: 100
    applies_to: [character, npc]
"#;

    #[test]
    fn character_yaml_roundtrip() {
        let ch: Character = serde_yaml::from_str(SAMPLE_LIN_YU).expect("parse");
        assert_eq!(ch.id, "char_lin_yu");
        assert_eq!(ch.name, "林语");
        assert_eq!(ch.residence.map_ref.as_deref(), Some("maps/city/port_aurora.yaml"));
        assert_eq!(ch.attributes.len(), 2);

        // Round-trip
        let yaml = serde_yaml::to_string(&ch).expect("serialize");
        let ch2: Character = serde_yaml::from_str(&yaml).expect("re-parse");
        assert_eq!(ch.id, ch2.id);
        assert_eq!(ch.residence.map_ref, ch2.residence.map_ref);
    }

    #[test]
    fn character_templates_roundtrip() {
        let t: CharacterTemplates = serde_yaml::from_str(SAMPLE_TEMPLATES).expect("parse");
        assert_eq!(t.version, "0.1");
        assert_eq!(t.templates.len(), 1);
        assert_eq!(t.templates[0].id, "tpl_hp");
        assert_eq!(t.templates[0].applies_to, vec!["character", "npc"]);
    }

    #[test]
    fn sample_lin_yu_yaml_loads() {
        // 验证示例工程的 lin_yu.yaml 能被解析（spec §B5 要求）。
        let yaml = include_str!("../../../examples/sample_world/characters/lin_yu.yaml");
        let ch: Character = serde_yaml::from_str(yaml).expect("parse sample");
        assert_eq!(ch.id, "char_lin_yu");
        assert!(ch.residence.map_ref.as_deref().unwrap_or("").contains("port_aurora"));
    }

    #[test]
    fn sample_templates_yaml_loads() {
        let yaml = include_str!("../../../examples/sample_world/characters/_templates.yaml");
        let t: CharacterTemplates = serde_yaml::from_str(yaml).expect("parse sample");
        assert!(t.templates.iter().any(|tpl| tpl.id == "tpl_hp"));
    }
}