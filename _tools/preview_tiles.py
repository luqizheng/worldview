"""Render a small preview world map using the generated atlases.

Produces `examples/sample_world/assets/textures/tiles/_preview.png` — a single
PNG that shows a tiled world map with mixed terrain, a road network, a river,
and a couple of borders. Useful for visual sanity-checking the atlas.

Reads back the PNG atlases we just wrote and consumes them as if they were a
real runtime asset, exercising the same JSON metadata format.
"""
from __future__ import annotations

import json
import os
from PIL import Image, ImageDraw

TILE = 64
PAD = 1
OUT = os.path.join(
    os.path.dirname(__file__), "..",
    "examples", "sample_world", "assets", "textures", "tiles",
)


def load_atlas(name: str) -> tuple[Image.Image, dict[str, tuple[int, int]]]:
    """Return (canvas, name→(col,row)) for an atlas written by make_tiles.py."""
    png = Image.open(os.path.join(OUT, name + "_atlas.png")).convert("RGBA")
    with open(os.path.join(OUT, name + "_atlas.json"), encoding="utf-8") as f:
        meta = json.load(f)
    idx = {}
    cols = png.size[0] // (TILE + PAD)
    for fr in meta["frames"]:
        x = fr["frame"]["x"]
        y = fr["frame"]["y"]
        # Convert atlas-px back to (col, row)
        idx[fr["filename"]] = (x, y)
    return png, idx


def crop_frame(atlas: Image.Image, frame_xy: tuple[int, int]) -> Image.Image:
    x, y = frame_xy
    return atlas.crop((x, y, x + TILE, y + TILE))


def main() -> None:
    terrain, tidx = load_atlas("terrain")
    road,    ridx = load_atlas("road")
    river,   rvdx = load_atlas("river")
    border,  bidx = load_atlas("border")

    # 12×8 world grid
    cols, rows = 12, 8
    W = cols * TILE
    H = rows * TILE
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))

    # ---- Build a small hand-authored world layout (matches the sample_world
    # theme: 艾瑟兰大陆 with a central mountain range and a port city) -----
    # terrain_key for each cell
    grid = [
        # y=0
        ["water", "water", "water", "coast", "coast", "coast", "plain", "plain",
         "plain", "forest", "forest", "forest"],
        # y=1
        ["water", "water", "coast", "coast", "plain", "plain", "plain", "plain",
         "forest", "forest", "forest", "forest"],
        # y=2
        ["coast", "coast", "plain", "plain", "plain", "hill", "hill", "forest",
         "forest", "forest", "desert", "desert"],
        # y=3
        ["plain", "plain", "plain", "hill", "mountain", "mountain", "hill",
         "hill", "forest", "desert", "desert", "desert"],
        # y=4
        ["plain", "plain", "plain", "hill", "mountain", "mountain", "mountain",
         "hill", "swamp", "swamp", "desert", "desert"],
        # y=5
        ["plain", "plain", "plain", "hill", "mountain", "mountain", "hill",
         "hill", "swamp", "tundra", "tundra", "tundra"],
        # y=6
        ["basin", "basin", "plain", "hill", "hill", "hill", "plain", "plain",
         "tundra", "tundra", "tundra", "tundra"],
        # y=7
        ["basin", "plain", "plain", "plain", "plain", "plain", "plain", "plain",
         "plain", "tundra", "tundra", "tundra"],
    ]
    for y, row in enumerate(grid):
        for x, key in enumerate(row):
            if key == "river":
                # river cells fall through to plain underneath
                canvas.paste(crop_frame(terrain, tidx["plain"]), (x * TILE, y * TILE),
                             crop_frame(terrain, tidx["plain"]))
                canvas.paste(crop_frame(river, rvdx["river_v"]), (x * TILE, y * TILE),
                             crop_frame(river, rvdx["river_v"]))
            else:
                canvas.paste(crop_frame(terrain, tidx[key]), (x * TILE, y * TILE),
                             crop_frame(terrain, tidx[key]))

    # ---- Road network: a horizontal trunk through the middle + branches ----
    road_layout = {
        # horizontal main road on row 4
        (1, 4): "h", (2, 4): "h", (3, 4): "h", (4, 4): "h",
        (5, 4): "h", (6, 4): "h", (7, 4): "h", (8, 4): "h",
        # a branch up to row 1
        (5, 3): "bend_bl",
        (5, 2): "v", (5, 1): "t",
        # branch down
        (3, 5): "bend_tl",
        (3, 6): "v", (3, 7): "endcap",
        # cross near port
        (2, 1): "bend_tr",
        # a small cross at the eastern hills
        (7, 3): "bend_br",
    }
    for (x, y), key in road_layout.items():
        canvas.paste(crop_frame(road, ridx[key]), (x * TILE, y * TILE),
                     crop_frame(road, ridx[key]))

    # ---- River: a vertical river down column 9, with bends ---------------
    river_layout = {
        (9, 0): "river_v",
        (9, 1): "river_bend_bl",
        (9, 2): "river_h",
        (10, 2): "river_bend_br",
        (10, 3): "river_v",
        (10, 4): "river_v",
        (10, 5): "river_bend_tl",
        (9, 5): "river_v",
        (9, 6): "river_v",
        (9, 7): "river_v",
    }
    for (x, y), key in river_layout.items():
        canvas.paste(crop_frame(river, rvdx[key]), (x * TILE, y * TILE),
                     crop_frame(river, rvdx[key]))

    # ---- Borders: a dashed line along the eastern mountain range ---------
    for y in (3, 4, 5):
        canvas.paste(crop_frame(border, bidx["border_dashed"]),
                     (4 * TILE, y * TILE),
                     crop_frame(border, bidx["border_dashed"]))

    # ---- Annotation: a small red dot at the port city (col 1, row 1) -----
    dot = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    ImageDraw.Draw(dot, "RGBA").ellipse(
        (TILE // 2 - 10, TILE // 2 - 10, TILE // 2 + 10, TILE // 2 + 10),
        fill=(244, 80, 80, 255),
    )
    ImageDraw.Draw(dot, "RGBA").ellipse(
        (TILE // 2 - 4, TILE // 2 - 4, TILE // 2 + 4, TILE // 2 + 4),
        fill=(255, 240, 200, 255),
    )
    canvas.paste(dot, (1 * TILE, 1 * TILE), dot)

    out_path = os.path.join(OUT, "_preview.png")
    canvas.save(out_path, format="PNG", optimize=True)
    print(f"wrote {out_path} ({os.path.getsize(out_path)} bytes)")


if __name__ == "__main__":
    main()
