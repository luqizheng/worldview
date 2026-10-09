#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
make_marker_icons.py — 生成 marker icon atlas（与 make_tiles.py 风格一致）

输入：手工定义的 16x16 像素矩阵（列表中每个图标）
输出：
  - assets/icons/icons_atlas.png
  - assets/icons/icons_atlas.json  （PixiJS 风格帧元数据，与现有 atlases 同形）
  - assets/icons/icons_atlas.png.import  （Godot 4.x 的 PNG 导入配置）

约定：
  - 调色板见 PALETTE 字典；颜色索引 0 = 透明（必须出现在每个图标中作为背景）。
  - icon 列表按 icons_atlas.json 写入顺序拼接。
  - padding = 1（与现有 atlases 一致），cell = 16x16。
  - 输出 PNG 宽 = icons * (cell + padding) + padding
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Sequence

from PIL import Image

# -------------------------------------------------------------------- palette
PALETTE: dict[str, tuple[int, int, int, int]] = {
    ".": (0, 0, 0, 0),          # 透明
    "k": (40, 28, 16, 255),     # 深褐轮廓（线稿）
    "K": (92, 67, 38, 255),     # 暖褐填充（建筑物/石头）
    "t": (212, 184, 122, 255),  # 羊皮纸 tan（屋顶/皮肤）
    "T": (232, 212, 162, 255),  # 羊皮纸沙色高亮
    "r": (139, 58, 46, 255),    # 朱红（都城旗/事件点核心）
    "y": (240, 200, 80, 255),   # 暖黄（事件点高亮）
    "c": (96, 152, 184, 255),   # 浅蓝（传送门魔法）
    "g": (96, 132, 72, 255),    # 苔绿（聚落周边/废墟植被）
    "s": (210, 210, 210, 255),  # 银灰（高光/烟雾）
    "w": (248, 244, 232, 255),  # 羊皮纸底色（用于 marker 的圆形/方框底）
}

CELL = 16
PADDING = 1


# ---------------------------------------------------------------- icon defs
# 每个 icon 是 16 行字符串，每行 16 个字符，字符取自 PALETTE。
# 字符 '.' 必须出现以保证透明背景。

ICON_VILLAGE = [
    "................",
    "................",
    "......kkkk......",
    ".....kttttk.....",
    "....ktttttTtk...",  # 屋顶
    "....ktttttTtk...",
    "....kKKKKKKKk...",
    "...kKttttttKk...",  # 墙 + 窗
    "...kKtKKKttKk...",
    "...kKtKKKttKk...",
    "...kKKKKKKKKk...",
    "...kK.KKKK.Kk...",
    "...kkkkkkkkkk...",
    "....KKKKKKKK....",
    "....KKKKKKKK....",
    "................",
]

ICON_TOWN = [
    "................",
    "................",
    ".kkkk..kkkk.....",
    "ktttk..ktttk....",  # 两个屋顶
    "kttTk..ktttk....",
    "kTtTkkkktTtkk...",
    "kKKKKKKKKKKKKk..",  # 围墙
    "kKtttttKttttKk..",
    "kKtKKKttKttttKk.",
    "kKtKKKttKKKKKKk.",
    "kKKKKKKKKK..KKk.",
    "kK.KKKKK.KK..KKk",
    "kkkkkkkkkkkkkkkk.",
    ".KKKKKKKKKKKKKKk.",
    ".KKKKKKKKKKKKKKk.",
    "................",
]

ICON_CAPITAL = [
    "................",
    "................",
    ".......r........",  # 旗
    "......rrr.......",
    "......k.r.......",
    "....kkkkkk......",  # 旗杆顶
    "...kktttttk.....",
    "..kttttttTtk....",  # 主塔顶
    ".ktttttttttTk...",
    ".kKKKKKKKKKKk...",  # 主塔身
    ".kKtttttKtttKk..",
    ".kKKKKttKKKKKk..",
    ".kKKKtttttttKk..",
    ".kKKKKkkkkkKKk..",  # 城堡主体
    ".kkkkkkkkkkkkk..",
    "................",
]

# NOTE: 16x16 用 16 字符宽 — 上方部分用了 17/18 字符会被截断，重新规范化：
ICON_VILLAGE = [
    "................",
    "................",
    "......kkkk......",
    ".....kttttk.....",
    "....kttttttk....",
    "....kttttttk....",
    "....kKKKKKKk....",
    "...kKttttttKk...",
    "...kKtKKKttKk...",
    "...kKtKKKttKk...",
    "...kKKKKKKKKk...",
    "...kK.KKKK.Kk...",
    "...kkkkkkkkkk...",
    "....KKKKKKKK....",
    "....KKKKKKKK....",
    "................",
]

ICON_TOWN = [
    "................",
    "................",
    ".kkkk..kkkk.....",
    "ktttk..ktttk....",
    "ktttk..ktttk....",
    "kKKKKkkKKKKkk...",
    "kKtttttKttttKk..",
    "kKtttttKttttKk..",
    "kKKKKKKKKKKKKk..",
    "kKttKttKttttKk..",
    "kKttKttKKKKKKk..",
    "kKKKKKKKK..KKk..",
    "kkkkkkkkkkkkkkk.",
    ".KKKKKKKKKKKKKKk",
    ".KKKKKKKKKKKKKKk",
    "................",
]

ICON_CAPITAL = [
    "................",
    "................",
    ".......r........",
    "......rrr.......",
    "......rk........",
    "......rk........",
    "....kkkkkk......",
    "...kttttttk.....",
    "..ktttttttttk...",
    ".ktttttttttttk..",
    ".kKKKKKKKKKKKk..",
    ".kKttttttttttKk.",
    ".kKKKKkkkkKKKKk.",
    ".kKKKk....kKKKk.",
    ".kkkk......kkkk.",
    "................",
]

ICON_RUINS = [
    "................",
    "................",
    "...k.....k......",
    "..kTk...kTk.....",  # 残柱顶
    "..kTk...kTk.....",
    "..kKK...kKK.....",
    "..kKKkkkkKK.....",
    "...kkttttk......",  # 横梁
    "....kttttk......",
    "...kttttttk.....",
    "..ktttttttttk...",
    ".kttTtttttTtk...",
    ".kkkkk....kkkkk.",  # 倒塌底
    ".KKKKK....KKKKK.",
    "................",
    "................",
]

ICON_EVENT = [
    "................",
    "................",
    "....wwwwww......",
    "...wTTTTTTw.....",  # 圆环底
    "...wTttyyTw.....",
    "...wTtyyyTw.....",
    "...wTtyyyTw.....",
    "...wTtyyyTw.....",
    "...wTtttttTw....",
    "...wTtyyTtTw....",
    "...wTtyyTtTw....",
    "...wTtyyyytTw...",
    "...wTtyyyyytTw..",
    "...wwwwwwwwww...",
    "....kkkkkk......",
    "................",
]

ICON_PORTAL = [
    "................",
    "................",
    "...cc..cc.......",  # 闪光点
    "....cccc........",
    "..ckkkkkkcc.....",
    ".cktttttTkkc....",  # 拱门外圈
    ".cktttttttTkc...",
    ".ckTtc..ctTkc...",  # 拱门内芯
    ".ckTtc..ctTkc...",
    ".ckTtc..ctTkc...",
    ".ckttttttttkc...",
    ".ckttttttttkc...",
    "..ckkkkkkkkc....",
    "....ccccc.......",  # 拱门底
    "....cc.cc.......",
    "................",
]

ICON_FACTION_DOT = [
    "................",
    "................",
    "................",
    ".....kkkkk......",
    "....k.....k.....",
    "...k..rrr..k....",  # 红点居中
    "...k.rrrrr.k....",
    "...k.rrrrr.k....",
    "...k..rrr..k....",
    "....k.....k.....",
    ".....kkkkk......",
    "................",
    "................",
    "................",
    "................",
    "................",
]

# 命名约定：扁平语义名（去掉 settlement_ 前缀，按 marker 类型直呼）
# 数据契约：Marker.icon（数据模型规格 §2.4）按 filename 引用本清单中的帧名。
# 替换指南：m00395 起统一改为更精确的语义名；v1 占位像素由 _tools/make_marker_icons.py
# 生成，后续由美术替换为正式版（v1_draft frames 仅为 wireframe 层级，不够精美）。
ICONS: list[tuple[str, list[str]]] = [
    ("village", ICON_VILLAGE),
    ("town", ICON_TOWN),
    ("capital", ICON_CAPITAL),
    ("ruin_site", ICON_RUINS),
    ("quest_marker", ICON_EVENT),
    ("portal_arch", ICON_PORTAL),
    ("faction_marker", ICON_FACTION_DOT),
]


# ---------------------------------------------------------------- rendering
def render_atlas(icons: Sequence[tuple[str, list[str]]]) -> tuple[Image.Image, list[dict]]:
    """拼接 atlas，返回 (PIL.Image, frames元数据列表)。"""
    n = len(icons)
    width = (CELL + PADDING) * n + PADDING
    height = CELL + 2 * PADDING
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))

    frames: list[dict] = []
    for i, (name, grid) in enumerate(icons):
        if len(grid) != CELL:
            raise ValueError(f"icon '{name}' must have {CELL} rows, got {len(grid)}")
        for r, row in enumerate(grid):
            if len(row) != CELL:
                raise ValueError(
                    f"icon '{name}' row {r} must have {CELL} cols, got {len(row)}"
                )
            for c, ch in enumerate(row):
                if ch not in PALETTE:
                    raise ValueError(
                        f"icon '{name}' row {r} col {c}: unknown char '{ch}'"
                    )
                rgba = PALETTE[ch]
                x = PADDING + i * (CELL + PADDING) + c
                y = PADDING + r
                img.putpixel((x, y), rgba)

        x = PADDING + i * (CELL + PADDING)
        frames.append({
            "filename": name,
            "frame": {"x": x, "y": PADDING, "w": CELL, "h": CELL},
            "rotated": False,
            "trimmed": False,
            "sourceSize": {"w": CELL, "h": CELL},
            "pivot": {"x": 0.5, "y": 0.5},
        })

    return img, frames


def write_json(path: Path, width: int, height: int, frames: list[dict]) -> None:
    meta = {
        "meta": {
            "image": path.with_suffix("").name + ".png",
            "format": "RGBA8888",
            "size": {"w": width, "h": height},
            "scale": 1,
        },
        "frames": frames,
    }
    path.write_text(json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8")


# 与现有 .png.import 同形（无 mipmap，sRGB，无损）
GODOT_IMPORT_TEMPLATE = """[remap]

importer="texture"
type="CompressedTexture2D"
uid="uid://{uid}"
path="res://.godot/imported/{png}.png-{hash}.ctex"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://{relpath}"
dest_files=["res://.godot/imported/{png}.png-{hash}.ctex"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""


def main() -> None:
    here = Path(__file__).resolve().parent
    out_dir = (here.parent / "assets" / "icons").resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    img, frames = render_atlas(ICONS)
    png_path = out_dir / "icons_atlas.png"
    json_path = out_dir / "icons_atlas.json"
    import_path = out_dir / "icons_atlas.png.import"

    img.save(png_path)
    write_json(json_path, img.width, img.height, frames)

    # 占位 uid/hash（Godot 首次打开会按实际内容重写），保留 .import 文件便于编辑器识别
    relpath = png_path.relative_to(here.parent).as_posix()
    import_body = GODOT_IMPORT_TEMPLATE.format(
        uid="b1markericons000",  # 占位 uid
        png=png_path.name,
        hash="placeholder0000000000000000",
        relpath=relpath,
    )
    import_path.write_text(import_body, encoding="utf-8")

    # 同时写一个 index.yaml，纳入 examples/sample_world 的 manifest 体系外、作为全局资源清单
    index_yaml = (
        "# World View — global marker icon atlas manifest\n"
        "#\n"
        "# 由 _tools/make_marker_icons.py 生成。\n"
        "# 命名约定：settlement_<size> / ruins / event_point / portal / faction_dot\n"
        "# 数据契约：Marker.icon（数据模型规格 §2.4）按 filename 引用本清单中的帧名。\n"
        "\n"
        "version: 1\n"
        "tile_size: 16\n"
        "padding: 1\n"
        "format: pixijs_spritesheet\n"
        "\n"
        "atlas:\n"
        "  id: marker_icons\n"
        "  png: icons_atlas.png\n"
        "  json: icons_atlas.json\n"
        f"  cols: {len(ICONS)}\n"
        f"  rows: 1\n"
        f"  frame_count: {len(ICONS)}\n"
        "  frames:\n"
    )
    for name, _ in ICONS:
        index_yaml += f"    - {name}\n"
    (out_dir / "index.yaml").write_text(index_yaml, encoding="utf-8")

    # 写一份 _preview.png：每个 icon 单独两倍放大，便于审阅
    preview = Image.new("RGBA", (CELL * 4 * len(ICONS), CELL * 4), (0, 0, 0, 0))
    for i, (_, grid) in enumerate(ICONS):
        cell_img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        for r, row in enumerate(grid):
            for c, ch in enumerate(row):
                if ch != ".":
                    cell_img.putpixel((c, r), PALETTE[ch])
        scaled = cell_img.resize((CELL * 4, CELL * 4), Image.NEAREST)
        preview.paste(scaled, (i * CELL * 4, 0), scaled)
    preview.save(out_dir / "_preview.png")

    print(f"wrote {png_path}")
    print(f"wrote {json_path}")
    print(f"wrote {import_path}")
    print(f"wrote {out_dir / 'index.yaml'}")
    print(f"wrote {out_dir / '_preview.png'}")


if __name__ == "__main__":
    main()
