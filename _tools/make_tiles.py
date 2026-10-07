"""Generate the World View runtime tile atlases.

Outputs (under examples/sample_world/assets/textures/tiles/):

  terrain_atlas.png   64×64 per frame, 11 frames in a 11×1 strip
  terrain_atlas.json  PixiJS-friendly sprite frame list
  road_atlas.png      11 frames: 4 straights (h/v/2diag) + 4 bends + T + cross + endcap
  road_atlas.json
  river_atlas.png     8 frames: 4 straights + 4 bends
  river_atlas.json
  border_atlas.png    4 frames: solid, dashed, dotted, double
  border_atlas.json
  index.yaml          legend + atlas manifest (single source of truth for the renderer)

Tile design rules (deliberately small and tile-friendly):
  - Per-frame size is 64×64 with a 1px inner padding so neighbouring frames
    in the atlas do not bleed via texture filtering.
  - All terrain tiles use the *same* 8×8 dot stipple pattern so they tile
    seamlessly side-by-side and read as a continuous map.
  - Pixel-art style with a 2px-wide color, deterministic RNG seeded per tile
    so re-running the script produces byte-identical PNGs (good for Git diffs).

The 11 terrain types are exactly the enum documented in
docs/数据模型规格_v0.1.md §3.1. The JSON `frames` mapping uses the same
English keys so the renderer can look up "mountain" → frame name directly.
"""
from __future__ import annotations

import json
import os
import random
from dataclasses import dataclass
from typing import Iterable

from PIL import Image, ImageDraw

# ---- Tile geometry ----------------------------------------------------------
TILE = 64                       # px per frame
PAD = 1                         # inner padding between frames
COLS_TERRAIN = 11               # 11 terrain types, single row
COLS_ROAD = 11
COLS_RIVER = 8
COLS_BORDER = 4

OUT_ROOT = os.path.join(
    os.path.dirname(__file__), "..",
    "examples", "sample_world", "assets", "textures", "tiles",
)
os.makedirs(OUT_ROOT, exist_ok=True)


# ---- Palette (kept consistent with the app icon and the doc UI spec) -------
# Each terrain has: base, mid (texture), dark (shadow), accent (highlight).
TERRAIN_PALETTE = {
    "mountain": {"base": (142, 142, 156, 255), "mid": (104, 104, 120, 255),
                 "dark": (62, 64, 80, 255),    "accent": (224, 224, 232, 255)},
    "forest":   {"base": (60, 120, 78, 255),   "mid": (40, 92, 58, 255),
                 "dark": (24, 58, 36, 255),    "accent": (140, 200, 150, 255)},
    "plain":    {"base": (170, 196, 122, 255), "mid": (140, 168, 96, 255),
                 "dark": (96, 124, 60, 255),   "accent": (212, 226, 162, 255)},
    "hill":     {"base": (146, 156, 96, 255),  "mid": (110, 122, 68, 255),
                 "dark": (74, 84, 44, 255),    "accent": (200, 210, 140, 255)},
    "water":    {"base": (60, 110, 168, 255),  "mid": (40, 86, 142, 255),
                 "dark": (22, 56, 102, 255),   "accent": (138, 184, 224, 255)},
    "desert":   {"base": (224, 198, 132, 255), "mid": (196, 168, 100, 255),
                 "dark": (148, 122, 64, 255),  "accent": (244, 226, 174, 255)},
    "swamp":    {"base": (90, 116, 92, 255),   "mid": (64, 90, 76, 255),
                 "dark": (40, 60, 56, 255),    "accent": (140, 170, 130, 255)},
    "tundra":   {"base": (210, 222, 232, 255), "mid": (170, 188, 204, 255),
                 "dark": (120, 144, 168, 255), "accent": (240, 246, 252, 255)},
    "basin":    {"base": (132, 108, 76, 255),  "mid": (102, 80, 52, 255),
                 "dark": (66, 50, 30, 255),    "accent": (190, 168, 130, 255)},
    "coast":    {"base": (190, 188, 140, 255), "mid": (156, 162, 110, 255),
                 "dark": (104, 116, 70, 255),  "accent": (224, 220, 174, 255)},
    "river":    {"base": (54, 122, 178, 255),  "mid": (34, 92, 148, 255),
                 "dark": (18, 60, 108, 255),   "accent": (132, 188, 222, 255)},
}

ROAD_COLOR = (252, 232, 168, 255)         # cream-tan
ROAD_DARK = (164, 122, 56, 255)
ROAD_SHADOW = (60, 36, 12, 180)

RIVER_COLOR = (90, 158, 212, 255)
RIVER_DARK = (32, 80, 140, 255)
RIVER_BANK = (196, 178, 124, 255)

BORDER_COLORS = {
    "border_solid":  (240, 76, 76, 255),
    "border_dashed": (252, 220, 90, 255),
    "border_dotted": (200, 220, 240, 255),
    "border_double": (240, 240, 240, 255),
}


# ---- Low-level tile primitives ---------------------------------------------
def _seeded_rng(seed: int) -> random.Random:
    """Deterministic RNG so PNGs are byte-identical across runs (Git-friendly)."""
    return random.Random(seed)


def _new_frame(rgba=(0, 0, 0, 0)) -> Image.Image:
    return Image.new("RGBA", (TILE, TILE), rgba)


def _scatter_dots(layer: Image.Image, color, density: float, size: int,
                  rng: random.Random) -> None:
    d = ImageDraw.Draw(layer, "RGBA")
    n = int(TILE * TILE * density / 100)
    for _ in range(n):
        x = rng.randint(0, TILE - size)
        y = rng.randint(0, TILE - size)
        d.rectangle((x, y, x + size - 1, y + size - 1), fill=color)


def _stipple_grid(layer: Image.Image, color) -> None:
    """8×8 dot grid that tiles seamlessly. Used as base texture for terrains."""
    d = ImageDraw.Draw(layer, "RGBA")
    for y in range(0, TILE, 8):
        for x in range(0, TILE, 8):
            d.rectangle((x, y, x + 1, y + 1), fill=color)


def _draw_corner_dots(layer: Image.Image, color) -> None:
    """Highlight dots at consistent positions so adjacent tiles blend."""
    d = ImageDraw.Draw(layer, "RGBA")
    for (x, y) in ((4, 4), (12, 12), (20, 4), (36, 20), (52, 36), (28, 52)):
        d.rectangle((x, y, x + 2, y + 2), fill=color)


# ---- Terrain tile generators -----------------------------------------------
def terrain_mountain(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["mountain"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # ridges: dark triangles
    for tx, ty in ((8, 36), (24, 18), (40, 40), (50, 14)):
        apex = (tx + 12, ty - 14)
        d.polygon([(tx, ty), (tx + 24, ty), apex], fill=p["mid"])
        d.polygon([(tx + 4, ty - 4), (tx + 20, ty - 4),
                   ((apex[0] + tx + 4) // 2, (apex[1] + ty - 4) // 2)], fill=p["accent"])
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["accent"], 0.6, 1, rng)
    return img


def terrain_forest(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["forest"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # triangular trees, 3 px trunks hidden under canopies
    trees = [(10, 14), (22, 8), (34, 18), (46, 12), (54, 30),
             (8,  38), (24, 40), (42, 44), (16, 52), (50, 50)]
    for (cx, cy) in trees:
        d.polygon([(cx, cy), (cx + 10, cy), (cx + 5, cy - 10)], fill=p["mid"])
        d.polygon([(cx + 2, cy - 3), (cx + 8, cy - 3),
                   (cx + 5, cy - 9)], fill=p["accent"])
        d.rectangle((cx + 4, cy, cx + 6, cy + 2), fill=p["dark"])
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["accent"], 1.2, 1, rng)
    return img


def terrain_plain(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["plain"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # subtle grass tufts at consistent positions
    for (x, y) in ((6, 12), (20, 22), (34, 8), (44, 28), (54, 16),
                   (12, 38), (28, 50), (40, 44), (52, 50), (8, 50)):
        d.rectangle((x, y, x + 3, y), fill=p["accent"])
        d.rectangle((x, y, x, y + 3), fill=p["accent"])
    _stipple_grid(img, p["mid"])
    _scatter_dots(img, p["accent"], 0.8, 1, rng)
    return img


def terrain_hill(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["hill"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # rounded mounds
    for (cx, cy, r) in ((14, 30, 14), (38, 22, 16), (52, 44, 10), (22, 50, 12)):
        d.ellipse((cx - r, cy - r // 2, cx + r, cy + r), fill=p["mid"])
        d.ellipse((cx - r // 2, cy - r // 2 - 2, cx + r // 2, cy),
                  fill=p["accent"])
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["accent"], 0.5, 1, rng)
    return img


def terrain_water(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["water"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # horizontal "wave" lines at regular y positions (tile seamlessly)
    for y in (12, 28, 44, 60):
        d.line([(0, y), (TILE, y)], fill=p["mid"], width=2)
        # chevron highlights
        for x in range(4, TILE, 16):
            d.line([(x, y - 2), (x + 6, y - 2)], fill=p["accent"], width=1)
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["accent"], 0.4, 1, rng)
    return img


def terrain_desert(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["desert"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # crescent dunes
    for (cx, cy, r) in ((16, 22, 14), (40, 36, 18), (50, 14, 10)):
        d.ellipse((cx - r, cy - r // 3, cx + r, cy + r // 2), fill=p["mid"])
        d.arc((cx - r + 2, cy - r // 3, cx + r - 2, cy + r // 2 - 1),
              start=200, end=340, fill=p["dark"], width=1)
        d.ellipse((cx - r + 3, cy - r // 4, cx - r + 8, cy),
                  fill=p["accent"])
    _stipple_grid(img, p["mid"])
    _scatter_dots(img, p["accent"], 0.5, 1, rng)
    return img


def terrain_swamp(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["swamp"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # dark puddles + reeds
    for (cx, cy, r) in ((16, 20, 10), (40, 36, 12), (50, 14, 8)):
        d.ellipse((cx - r, cy - r // 2, cx + r, cy + r // 2), fill=p["dark"])
        d.ellipse((cx - r + 2, cy - r // 2 + 1, cx + r - 2, cy),
                  fill=p["accent"])
    for (x, y) in ((6, 44), (22, 50), (38, 50), (54, 44)):
        d.line([(x, y), (x, y - 8)], fill=p["mid"], width=1)
        d.line([(x - 2, y - 4), (x + 2, y - 4)], fill=p["accent"], width=1)
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["mid"], 0.6, 1, rng)
    return img


def terrain_tundra(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["tundra"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # scattered ice spikes + dots
    for (cx, cy, r) in ((14, 18, 6), (38, 30, 8), (52, 14, 5), (24, 48, 7)):
        d.polygon([(cx, cy - r), (cx - r, cy + r // 2),
                   (cx + r, cy + r // 2)], fill=p["accent"])
        d.polygon([(cx, cy - r), (cx - r // 2, cy + r // 2),
                   (cx + r // 2, cy + r // 2)], fill=(255, 255, 255, 200))
    _stipple_grid(img, p["mid"])
    _scatter_dots(img, p["accent"], 1.2, 1, rng)
    return img


def terrain_basin(rng: random.Random) -> Image.Image:
    p = TERRAIN_PALETTE["basin"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # dry cracked earth: concentric rings
    for (cx, cy, r) in ((20, 22, 12), (44, 38, 14)):
        for rr in (r, r - 4, r - 8):
            if rr > 0:
                d.ellipse((cx - rr, cy - rr // 2, cx + rr, cy + rr // 2),
                          outline=p["dark"], width=1)
        d.ellipse((cx - r, cy - r // 2, cx + r, cy + r // 2), fill=p["mid"])
    # cracks radiating outward
    for (x1, y1, x2, y2) in ((0, 30, 12, 22), (0, 50, 20, 38),
                              (60, 12, 44, 30), (60, 56, 50, 44)):
        d.line([(x1, y1), (x2, y2)], fill=p["dark"], width=1)
    _stipple_grid(img, p["dark"])
    _scatter_dots(img, p["accent"], 0.4, 1, rng)
    return img


def terrain_coast(rng: random.Random) -> Image.Image:
    """Sandy strip with water at the bottom — meant to tile with water on one side."""
    p = TERRAIN_PALETTE["coast"]
    water = TERRAIN_PALETTE["water"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # beach
    d.rectangle((0, 0, TILE, 36), fill=p["base"])
    d.rectangle((0, 36, TILE, TILE), fill=water["base"])
    # foam line
    d.line([(0, 36), (TILE, 36)], fill=(255, 255, 255, 200), width=1)
    # shallow water lines
    for y in (44, 56):
        d.line([(0, y), (TILE, y)], fill=water["mid"], width=1)
    # sand dots
    _scatter_dots(img, p["accent"], 0.8, 1, rng)
    # water stipple
    d2 = ImageDraw.Draw(img, "RGBA")
    for y in (40, 52):
        for x in range(0, TILE, 8):
            d2.rectangle((x, y, x + 2, y), fill=water["accent"])
    return img


def terrain_river(rng: random.Random) -> Image.Image:
    """River running vertically through the frame, meant to be tiled horizontally."""
    p = TERRAIN_PALETTE["river"]
    img = _new_frame(p["base"])
    d = ImageDraw.Draw(img, "RGBA")
    # water column centered, with wavy banks
    bank_xs = [16, 48]
    for bx in bank_xs:
        for y in range(0, TILE, 4):
            wobble = (y // 8) % 3 - 1
            d.ellipse((bx + wobble - 1, y, bx + wobble + 2, y + 4),
                      fill=RIVER_BANK)
    # water fill
    d.rectangle((20, 0, 44, TILE), fill=p["base"])
    # current highlights
    for y in range(4, TILE, 12):
        d.line([(24, y), (40, y)], fill=p["accent"], width=1)
    return img


TERRAIN_GENERATORS = {
    "mountain": terrain_mountain,
    "forest":   terrain_forest,
    "plain":    terrain_plain,
    "hill":     terrain_hill,
    "water":    terrain_water,
    "desert":   terrain_desert,
    "swamp":    terrain_swamp,
    "tundra":   terrain_tundra,
    "basin":    terrain_basin,
    "coast":    terrain_coast,
    "river":    terrain_river,
}


# ---- Road tile generators (orthogonal + 45° angles) -----------------------
# All road strips are 12 px wide, centered on the tile. Strips are drawn as
# polygons so diagonals and bends stay clean.
ROAD_HALF = 6   # half-width of a road strip


def _road_strip_h(img: Image.Image, color=ROAD_COLOR, edge=ROAD_DARK):
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    d.rectangle((0, y - ROAD_HALF, TILE - 1, y + ROAD_HALF - 1), fill=color)
    d.rectangle((0, y - ROAD_HALF, TILE - 1, y - ROAD_HALF), fill=edge)
    d.rectangle((0, y + ROAD_HALF - 1, TILE - 1, y + ROAD_HALF - 1), fill=edge)


def _road_strip_v(img: Image.Image, color=ROAD_COLOR, edge=ROAD_DARK):
    d = ImageDraw.Draw(img, "RGBA")
    x = TILE // 2
    d.rectangle((x - ROAD_HALF, 0, x + ROAD_HALF - 1, TILE - 1), fill=color)
    d.rectangle((x - ROAD_HALF, 0, x - ROAD_HALF, TILE - 1), fill=edge)
    d.rectangle((x + ROAD_HALF - 1, 0, x + ROAD_HALF - 1, TILE - 1), fill=edge)


def _road_strip_d1(img: Image.Image, color=ROAD_COLOR, edge=ROAD_DARK):
    """Diagonal road from TL to BR (slope +1). Drawn as a parallelogram polygon."""
    d = ImageDraw.Draw(img, "RGBA")
    h = ROAD_HALF
    pts = [(-h, 0), (h, 0), (TILE + h, TILE), (TILE - h, TILE)]
    d.polygon(pts, fill=color)
    # edges: top and bottom of the strip
    d.line([(-h, 0), (TILE - h, TILE)], fill=edge, width=1)
    d.line([(h, 0), (TILE + h, TILE)], fill=edge, width=1)


def _road_strip_d2(img: Image.Image, color=ROAD_COLOR, edge=ROAD_DARK):
    """Diagonal road from TR to BL (slope -1)."""
    d = ImageDraw.Draw(img, "RGBA")
    h = ROAD_HALF
    pts = [(h, 0), (-h, 0), (TILE - h, TILE), (TILE + h, TILE)]
    d.polygon(pts, fill=color)
    d.line([(h, 0), (TILE + h, TILE)], fill=edge, width=1)
    d.line([(-h, 0), (TILE - h, TILE)], fill=edge, width=1)


def _road_bend(img: Image.Image, mask: str):
    """Draw a road bend using a clean polygon.

    mask is a 2-char string describing which strips are present:
      first char  → horizontal: 'l' (left half), 'r' (right half), '_' (none)
      second char → vertical:   't' (top half),  'b' (bottom half),'_' (none)
    Examples:
      'lt' → vertical-top + horizontal-left  (i.e. the road enters from top
                                              and exits to the left = bend_tr)
      'rt' → vertical-top + horizontal-right (bend_tl)
      'lb' → vertical-bot + horizontal-left  (bend_br)
      'rb' → vertical-bot + horizontal-right (bend_bl)
    """
    d = ImageDraw.Draw(img, "RGBA")
    cx = cy = TILE // 2
    h = ROAD_HALF
    # horizontal segment
    hmask = mask[0]
    if hmask == "l":
        d.rectangle((0, cy - h, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    elif hmask == "r":
        d.rectangle((cx - h, cy - h, TILE - 1, cy + h - 1), fill=ROAD_COLOR)
    # vertical segment
    vmask = mask[1]
    if vmask == "t":
        d.rectangle((cx - h, 0, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    elif vmask == "b":
        d.rectangle((cx - h, cy - h, cx + h - 1, TILE - 1), fill=ROAD_COLOR)
    # inner-corner smoothing disc
    d.ellipse((cx - h, cy - h, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    # outline strokes for the outer edges
    if hmask == "l":
        d.line([(0, cy - h), (cx + h, cy - h)], fill=ROAD_DARK, width=1)
        d.line([(0, cy + h - 1), (cx + h, cy + h - 1)], fill=ROAD_DARK, width=1)
    elif hmask == "r":
        d.line([(cx - h, cy - h), (TILE - 1, cy - h)], fill=ROAD_DARK, width=1)
        d.line([(cx - h, cy + h - 1), (TILE - 1, cy + h - 1)], fill=ROAD_DARK, width=1)
    if vmask == "t":
        d.line([(cx - h, 0), (cx - h, cy + h)], fill=ROAD_DARK, width=1)
        d.line([(cx + h - 1, 0), (cx + h - 1, cy + h)], fill=ROAD_DARK, width=1)
    elif vmask == "b":
        d.line([(cx - h, cy - h), (cx - h, TILE - 1)], fill=ROAD_DARK, width=1)
        d.line([(cx + h - 1, cy - h), (cx + h - 1, TILE - 1)], fill=ROAD_DARK, width=1)


def _road_t(img: Image.Image, missing: str = "r"):
    """T-junction missing one exit. missing in 't','b','l','r'."""
    d = ImageDraw.Draw(img, "RGBA")
    cx = cy = TILE // 2
    h = ROAD_HALF
    # full horizontal
    d.rectangle((0, cy - h, TILE - 1, cy + h - 1), fill=ROAD_COLOR)
    # vertical minus the missing direction
    if missing != "t":
        d.rectangle((cx - h, 0, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    if missing != "b":
        d.rectangle((cx - h, cy - h, cx + h - 1, TILE - 1), fill=ROAD_COLOR)
    d.ellipse((cx - h, cy - h, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    # edges
    d.rectangle((0, cy - h, TILE - 1, cy - h), fill=ROAD_DARK)
    d.rectangle((0, cy + h - 1, TILE - 1, cy + h - 1), fill=ROAD_DARK)
    if missing != "t":
        d.rectangle((cx - h, 0, cx - h, cy + h), fill=ROAD_DARK)
        d.rectangle((cx + h - 1, 0, cx + h - 1, cy + h), fill=ROAD_DARK)
    if missing != "b":
        d.rectangle((cx - h, cy - h, cx - h, TILE - 1), fill=ROAD_DARK)
        d.rectangle((cx + h - 1, cy - h, cx + h - 1, TILE - 1), fill=ROAD_DARK)


def _road_cross(img: Image.Image):
    d = ImageDraw.Draw(img, "RGBA")
    cx = cy = TILE // 2
    h = ROAD_HALF
    d.rectangle((0, cy - h, TILE - 1, cy + h - 1), fill=ROAD_COLOR)
    d.rectangle((cx - h, 0, cx + h - 1, TILE - 1), fill=ROAD_COLOR)
    d.ellipse((cx - h, cy - h, cx + h - 1, cy + h - 1), fill=ROAD_COLOR)
    d.rectangle((0, cy - h, TILE - 1, cy - h), fill=ROAD_DARK)
    d.rectangle((0, cy + h - 1, TILE - 1, cy + h - 1), fill=ROAD_DARK)
    d.rectangle((cx - h, 0, cx - h, TILE - 1), fill=ROAD_DARK)
    d.rectangle((cx + h - 1, 0, cx + h - 1, TILE - 1), fill=ROAD_DARK)


def _road_endcap_h(img: Image.Image):
    """Road enters from left and ends in a small plaza on the right."""
    d = ImageDraw.Draw(img, "RGBA")
    cy = TILE // 2
    h = ROAD_HALF
    d.rectangle((0, cy - h, TILE - 1, cy + h - 1), fill=ROAD_COLOR)
    d.rectangle((0, cy - h, TILE - 1, cy - h), fill=ROAD_DARK)
    d.rectangle((0, cy + h - 1, TILE - 1, cy + h - 1), fill=ROAD_DARK)
    # plaza
    d.rectangle((TILE - 18, cy - 12, TILE - 1, cy + 12), fill=ROAD_COLOR)
    d.rectangle((TILE - 18, cy - 12, TILE - 1, cy - 12), fill=ROAD_DARK)
    d.rectangle((TILE - 18, cy + 12 - 1, TILE - 1, cy + 12 - 1), fill=ROAD_DARK)


def road_tile(spec: str) -> Image.Image:
    """Build a single road tile by spec name."""
    img = _new_frame()
    if spec == "h":
        _road_strip_h(img)
    elif spec == "v":
        _road_strip_v(img)
    elif spec == "d1":
        _road_strip_d1(img)
    elif spec == "d2":
        _road_strip_d2(img)
    elif spec == "bend_tl":
        _road_bend(img, "rt")   # vertical-top + horizontal-right
    elif spec == "bend_tr":
        _road_bend(img, "lt")   # vertical-top + horizontal-left
    elif spec == "bend_bl":
        _road_bend(img, "rb")   # vertical-bot + horizontal-right
    elif spec == "bend_br":
        _road_bend(img, "lb")   # vertical-bot + horizontal-left
    elif spec == "t":
        _road_t(img, "r")       # missing the right exit
    elif spec == "cross":
        _road_cross(img)
    elif spec == "endcap":
        _road_endcap_h(img)
    return img


ROAD_FRAMES = [
    "h", "v", "d1", "d2",
    "bend_tl", "bend_tr", "bend_bl", "bend_br",
    "t", "cross", "endcap",
]


# ---- River tile generators -------------------------------------------------
# River strips are 20 px wide (with 14 px water + 3 px bank on each side) so
# they read as a wide river, not a road. Same polygon approach as roads.
RIVER_BANK_HALF = 10   # half-width including banks
RIVER_WATER_HALF = 7   # half-width of water inside the bank


def _river_strip_h(img: Image.Image) -> Image.Image:
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    d.rectangle((0, y - RIVER_BANK_HALF, TILE - 1, y + RIVER_BANK_HALF - 1),
                fill=RIVER_BANK)
    d.rectangle((0, y - RIVER_WATER_HALF, TILE - 1, y + RIVER_WATER_HALF - 1),
                fill=RIVER_COLOR)
    for x in range(0, TILE, 8):
        d.line([(x, y - 3), (x + 4, y - 3)], fill=(180, 220, 240, 220), width=1)
        d.line([(x + 2, y + 2), (x + 6, y + 2)], fill=(180, 220, 240, 180), width=1)
    return img


def _river_strip_v(img: Image.Image) -> Image.Image:
    d = ImageDraw.Draw(img, "RGBA")
    x = TILE // 2
    d.rectangle((x - RIVER_BANK_HALF, 0, x + RIVER_BANK_HALF - 1, TILE - 1),
                fill=RIVER_BANK)
    d.rectangle((x - RIVER_WATER_HALF, 0, x + RIVER_WATER_HALF - 1, TILE - 1),
                fill=RIVER_COLOR)
    for y in range(0, TILE, 8):
        d.line([(x - 3, y), (x - 3, y + 4)], fill=(180, 220, 240, 220), width=1)
        d.line([(x + 2, y + 2), (x + 2, y + 6)], fill=(180, 220, 240, 180), width=1)
    return img


def _river_strip_d1(img: Image.Image) -> Image.Image:
    d = ImageDraw.Draw(img, "RGBA")
    h = RIVER_BANK_HALF
    pts_bank = [(-h, 0), (h, 0), (TILE + h, TILE), (TILE - h, TILE)]
    d.polygon(pts_bank, fill=RIVER_BANK)
    h2 = RIVER_WATER_HALF
    pts_water = [(-h2, 0), (h2, 0), (TILE + h2, TILE), (TILE - h2, TILE)]
    d.polygon(pts_water, fill=RIVER_COLOR)
    return img


def _river_strip_d2(img: Image.Image) -> Image.Image:
    d = ImageDraw.Draw(img, "RGBA")
    h = RIVER_BANK_HALF
    pts_bank = [(h, 0), (-h, 0), (TILE - h, TILE), (TILE + h, TILE)]
    d.polygon(pts_bank, fill=RIVER_BANK)
    h2 = RIVER_WATER_HALF
    pts_water = [(h2, 0), (-h2, 0), (TILE - h2, TILE), (TILE + h2, TILE)]
    d.polygon(pts_water, fill=RIVER_COLOR)
    return img


def _river_bend(img: Image.Image, mask: str) -> Image.Image:
    d = ImageDraw.Draw(img, "RGBA")
    cx = cy = TILE // 2
    bh = RIVER_BANK_HALF
    wh = RIVER_WATER_HALF
    hmask = mask[0]
    vmask = mask[1]
    if hmask == "l":
        d.rectangle((0, cy - bh, cx + bh - 1, cy + bh - 1), fill=RIVER_BANK)
        d.rectangle((0, cy - wh, cx + bh - 1, cy + wh - 1), fill=RIVER_COLOR)
    elif hmask == "r":
        d.rectangle((cx - bh, cy - bh, TILE - 1, cy + bh - 1), fill=RIVER_BANK)
        d.rectangle((cx - bh, cy - wh, TILE - 1, cy + wh - 1), fill=RIVER_COLOR)
    if vmask == "t":
        d.rectangle((cx - bh, 0, cx + bh - 1, cy + bh - 1), fill=RIVER_BANK)
        d.rectangle((cx - wh, 0, cx + wh - 1, cy + bh - 1), fill=RIVER_COLOR)
    elif vmask == "b":
        d.rectangle((cx - bh, cy - bh, cx + bh - 1, TILE - 1), fill=RIVER_BANK)
        d.rectangle((cx - wh, cy - bh, cx + wh - 1, TILE - 1), fill=RIVER_COLOR)
    # smooth inner corner
    d.ellipse((cx - bh, cy - bh, cx + bh - 1, cy + bh - 1), fill=RIVER_BANK)
    d.ellipse((cx - wh, cy - wh, cx + wh - 1, cy + wh - 1), fill=RIVER_COLOR)
    return img


RIVER_FRAMES = [
    ("river_h",       _river_strip_h),
    ("river_v",       _river_strip_v),
    ("river_d1",      _river_strip_d1),
    ("river_d2",      _river_strip_d2),
    ("river_bend_tl", lambda i: _river_bend(i, "rt")),
    ("river_bend_tr", lambda i: _river_bend(i, "lt")),
    ("river_bend_bl", lambda i: _river_bend(i, "rb")),
    ("river_bend_br", lambda i: _river_bend(i, "lb")),
]


# ---- Border tile generators ------------------------------------------------
def border_solid() -> Image.Image:
    img = _new_frame()
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    d.rectangle((0, y - 2, TILE, y + 2), fill=BORDER_COLORS["border_solid"])
    return img


def border_dashed() -> Image.Image:
    img = _new_frame()
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    for x in range(0, TILE, 12):
        d.rectangle((x, y - 2, x + 6, y + 2), fill=BORDER_COLORS["border_dashed"])
    return img


def border_dotted() -> Image.Image:
    img = _new_frame()
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    for x in range(2, TILE, 6):
        d.rectangle((x, y - 1, x + 2, y + 1), fill=BORDER_COLORS["border_dotted"])
    return img


def border_double() -> Image.Image:
    img = _new_frame()
    d = ImageDraw.Draw(img, "RGBA")
    y = TILE // 2
    d.rectangle((0, y - 4, TILE, y - 2), fill=BORDER_COLORS["border_double"])
    d.rectangle((0, y + 2, TILE, y + 4), fill=BORDER_COLORS["border_double"])
    return img


BORDER_GENERATORS = {
    "border_solid":  border_solid,
    "border_dashed": border_dashed,
    "border_dotted": border_dotted,
    "border_double": border_double,
}


# ---- Atlas assembly & metadata --------------------------------------------
@dataclass
class Atlas:
    name: str          # file basename without extension
    cols: int
    rows: int
    frames: list       # ordered list of frame keys (left-to-right, top-to-bottom)

    def assemble(self, frame_images: Iterable[Image.Image]) -> Image.Image:
        frame_images = list(frame_images)
        w = self.cols * TILE + (self.cols + 1) * PAD
        h = self.rows * TILE + (self.rows + 1) * PAD
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        for i, img in enumerate(frame_images):
            cx = i % self.cols
            cy = i // self.cols
            x = PAD + cx * (TILE + PAD)
            y = PAD + cy * (TILE + PAD)
            canvas.paste(img, (x, y))
        return canvas

    def metadata(self, png_filename: str) -> dict:
        # PixiJS sprite sheet format (frames are top-left + size in the atlas).
        out = {
            "meta": {
                "image": png_filename,
                "format": "RGBA8888",
                "size": {"w": self.cols * TILE + (self.cols + 1) * PAD,
                         "h": self.rows * TILE + (self.rows + 1) * PAD},
                "scale": 1,
            },
            "frames": [],
        }
        for i, key in enumerate(self.frames):
            cx = i % self.cols
            cy = i // self.cols
            x = PAD + cx * (TILE + PAD)
            y = PAD + cy * (TILE + PAD)
            out["frames"].append({
                "filename": key,
                "frame": {"x": x, "y": y, "w": TILE, "h": TILE},
                "rotated": False,
                "trimmed": False,
                "sourceSize": {"w": TILE, "h": TILE},
                "pivot": {"x": 0.5, "y": 0.5},
            })
        return out


def build_terrain() -> tuple[Atlas, list[Image.Image]]:
    keys = list(TERRAIN_GENERATORS.keys())
    imgs = []
    for i, k in enumerate(keys):
        # seed by terrain name so each variant is reproducible
        rng = _seeded_rng(0xA1 ^ i ^ sum(ord(c) for c in k))
        imgs.append(TERRAIN_GENERATORS[k](rng))
    return Atlas("terrain_atlas", cols=len(keys), rows=1, frames=keys), imgs


def build_road() -> tuple[Atlas, list[Image.Image]]:
    imgs = [road_tile(s) for s in ROAD_FRAMES]
    return Atlas("road_atlas", cols=len(ROAD_FRAMES), rows=1, frames=ROAD_FRAMES), imgs


def build_river() -> tuple[Atlas, list[Image.Image]]:
    keys = [k for k, _ in RIVER_FRAMES]
    imgs = [gen(_new_frame()) for _, gen in RIVER_FRAMES]
    return Atlas("river_atlas", cols=len(keys), rows=1, frames=keys), imgs


def build_border() -> tuple[Atlas, list[Image.Image]]:
    keys = list(BORDER_GENERATORS.keys())
    imgs = [gen() for gen in BORDER_GENERATORS.values()]
    return Atlas("border_atlas", cols=len(keys), rows=1, frames=keys), imgs


# ---- Drive -----------------------------------------------------------------
def write_atlas(atlas: Atlas, imgs: list[Image.Image]) -> None:
    png_path = os.path.join(OUT_ROOT, atlas.name + ".png")
    json_path = os.path.join(OUT_ROOT, atlas.name + ".json")
    atlas.assemble(imgs).save(png_path, format="PNG", optimize=True)
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(atlas.metadata(os.path.basename(png_path)), f,
                  ensure_ascii=False, indent=2)
    print(f"  wrote {png_path} ({os.path.getsize(png_path)} bytes)")
    print(f"  wrote {json_path} ({os.path.getsize(json_path)} bytes)")


def write_index(atlases: list[Atlas]) -> None:
    """Source-of-truth YAML the renderer reads. Mirrors docs/数据模型规格 §3.1."""
    lines = [
        "# World View — runtime tile atlas manifest",
        "#",
        "# This file is the renderer-facing source of truth for tile atlases.",
        "# The terrain frame names MUST match the TerrainType enum keys defined",
        "# in docs/数据模型规格_v0.1.md §3.1 (and crates/world-view-core/src/terrain.rs).",
        "# Adding a terrain type means: edit the data-model spec → register here →",
        "# add a frame in _tools/make_tiles.py → re-run `python _tools/make_tiles.py`.",
        "",
        "version: 1",
        "tile_size: 64",
        "padding: 1",
        "format: pixijs_spritesheet",
        "",
        "atlases:",
    ]
    for a in atlases:
        lines.append(f"  - id: {a.name}")
        lines.append(f"    png: {a.name}.png")
        lines.append(f"    json: {a.name}.json")
        lines.append(f"    cols: {a.cols}")
        lines.append(f"    rows: {a.rows}")
        lines.append(f"    frame_count: {len(a.frames)}")
        lines.append("    frames:")
        for k in a.frames:
            lines.append(f"      - {k}")
        lines.append("")
    out_path = os.path.join(OUT_ROOT, "index.yaml")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"  wrote {out_path}")


def main() -> None:
    print(f"Generating tiles → {OUT_ROOT}")
    atlases = []
    for name, builder in (
        ("terrain", build_terrain),
        ("road",    build_road),
        ("river",   build_river),
        ("border",  build_border),
    ):
        print(f"[{name}]")
        atlas, imgs = builder()
        write_atlas(atlas, imgs)
        atlases.append(atlas)
    write_index(atlases)
    print("done.")


if __name__ == "__main__":
    main()
