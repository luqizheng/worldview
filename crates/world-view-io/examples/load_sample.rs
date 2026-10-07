use std::path::PathBuf;
use world_view_io::load_project;

fn main() {
    // 从 crate 根向上两级 = workspace 根
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    let workspace_root = manifest_dir.parent().and_then(|p| p.parent()).unwrap();
    let dir = workspace_root.join("examples").join("sample_world");
    println!("loading: {}", dir.display());
    let loaded = load_project(&dir).expect("load ok");
    println!("maps loaded: {}", loaded.maps.len());
    for (path, lm) in &loaded.maps {
        println!(
            "  {}: id={}, type={}, grid={}x{}",
            path,
            lm.map.id,
            lm.map.r#type.as_str(),
            lm.grid.width(),
            lm.grid.height()
        );
    }
    // 输出前 4 行地形分布预览
    if let Some(world) = loaded.maps.get("maps/world/main.yaml") {
        println!("\n=== terrain sample (前 5 单元) ===");
        for row in 0..1 {
            for col in 0..5 {
                if let Some(cell) = world.grid.get_cell(col, row) {
                    println!(
                        "  ({col}, {row}): type={:?}, move_cost={}, buildable={}",
                        cell.r#type.as_str(),
                        cell.move_cost,
                        cell.buildable
                    );
                }
            }
        }
    }
}