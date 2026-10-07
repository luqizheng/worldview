"""Generate the World View app icon set.

Concept: a circular "globe" sitting on a horizontal timeline spine, with an
event-marker star and a small character dot anchored on the spine. Together
they represent the four core dimensions of the app: geography (globe),
time (spine), characters (dot), events (star).

Output: writes the five files the Tauri config references plus a 512px master.
All files go to src-tauri/icons/.

Renders a 1024x1024 master, downsamples with Lanczos for the multi-res ICO,
and packs the ICNS from PNG sources.
"""
from __future__ import annotations

import math
import os
from PIL import Image, ImageDraw, ImageFilter

# ---- Color palette (kept stable across the app; see docs/界面与交互规格 §色板) ----
BG_TOP    = (15,  23,  42,   255)   # slate-950 — deep midnight ground
BG_BOTTOM = (30,  41,  82,   255)   # indigo-950 — subtle vertical gradient
GLOBE_FILL= (24,  64, 120,   255)   # ocean blue
GLOBE_RIM = (94,  180, 230,  255)   # rim light
MERIDIAN  = (143, 211, 244, 210)   # meridian cyan
EQUATOR   = (186, 220, 240, 230)   # equator highlight
LAND      = (60, 145, 110, 255)    # continent green
LAND_HI   = (134, 205, 158, 255)   # continent highlight
SPINE     = (244, 196,  86,  255)  # timeline gold (amber-400-ish)
SPINE_HI  = (253, 224, 142, 255)   # timeline highlight
CHAR      = (252, 232, 168, 255)   # character dot (warm cream)
EVENT     = (252, 211,  77, 255)   # event star (amber-400)
EVENT_GLOW= (253, 230, 138, 200)
GRID      = (148, 163, 184, 80)    # faint background grid

W = H = 1024
CX, CY = W / 2, H / 2

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "src-tauri", "icons")
os.makedirs(OUT_DIR, exist_ok=True)


def _gradient_bg(size: int) -> Image.Image:
    """Vertical gradient background with rounded square mask."""
    w, h = size, size
    base = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = base.load()
    for y in range(h):
        t = y / (h - 1)
        r = int(BG_TOP[0] * (1 - t) + BG_BOTTOM[0] * t)
        g = int(BG_TOP[1] * (1 - t) + BG_BOTTOM[1] * t)
        b = int(BG_TOP[2] * (1 - t) + BG_BOTTOM[2] * t)
        for x in range(w):
            px[x, y] = (r, g, b, 255)
    # rounded square mask
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    radius = int(size * 0.22)
    md.rounded_rectangle((0, 0, w - 1, h - 1), radius=radius, fill=255)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(base, (0, 0), mask)
    return out


def _draw_grid(layer: Image.Image) -> None:
    """Faint lat/long-style grid behind the globe (only inside the disc)."""
    d = ImageDraw.Draw(layer, "RGBA")
    R = W * 0.36
    # horizontal latitude rings
    for k in (-2, -1, 1, 2):
        ry = R * abs(k) / 3
        if ry <= 1 or ry >= R:
            continue
        chord = math.sqrt(max(0.0, R * R - ry * ry))
        d.line(
            [(CX - chord, CY + ry), (CX + chord, CY + ry)],
            fill=GRID, width=2,
        )
    # vertical longitude meridians (foreshortened ellipses)
    for k in (-3, -2, -1, 1, 2, 3):
        rx = R * abs(k) / 4
        if rx <= 1 or rx >= R:
            continue
        bbox = (CX - rx, CY - R, CX + rx, CY + R)
        d.ellipse(bbox, outline=GRID, width=2)


def _draw_globe(layer: Image.Image) -> None:
    """Sphere body with rim light, equator, meridians, and two stylized land masses."""
    R = W * 0.36
    d = ImageDraw.Draw(layer, "RGBA")

    # outer rim light (slightly bigger, then we cover the inner part with fill)
    d.ellipse((CX - R - 8, CY - R - 8, CX + R + 8, CY + R + 8), fill=GLOBE_RIM)
    # globe body
    d.ellipse((CX - R, CY - R, CX + R, CY + R), fill=GLOBE_FILL)

    # meridians: nested ellipses
    for k in (1, 2, 3):
        rx = R * k / 4
        d.ellipse((CX - rx, CY - R, CX + rx, CY + R), outline=MERIDIAN, width=3)
    # equator
    d.line([(CX - R, CY), (CX + R, CY)], fill=EQUATOR, width=4)
    # polar caps hint
    d.ellipse((CX - R * 0.35, CY - R - 4, CX + R * 0.35, CY - R + 18), fill=MERIDIAN)
    d.ellipse((CX - R * 0.35, CY + R - 18, CX + R * 0.35, CY + R + 4), fill=MERIDIAN)

    # stylized continents: two irregular blobs
    d.ellipse((CX - R * 0.55, CY - R * 0.45, CX - R * 0.05, CY - R * 0.05), fill=LAND)
    d.ellipse((CX + R * 0.05, CY - R * 0.20, CX + R * 0.55, CY + R * 0.10), fill=LAND)
    # highlights on continents
    d.ellipse((CX - R * 0.50, CY - R * 0.40, CX - R * 0.20, CY - R * 0.15), fill=LAND_HI)
    d.ellipse((CX + R * 0.15, CY - R * 0.15, CX + R * 0.40, CY + R * 0.05), fill=LAND_HI)


def _draw_timeline(layer: Image.Image) -> None:
    """Horizontal timeline spine cutting across the globe (geography anchored on time)."""
    d = ImageDraw.Draw(layer, "RGBA")
    y = CY
    spine_left  = W * 0.10
    spine_right = W * 0.90
    # soft glow underlay
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.line([(spine_left, y), (spine_right, y)], fill=SPINE_HI, width=18)
    glow = glow.filter(ImageFilter.GaussianBlur(radius=8))
    layer.alpha_composite(glow)
    # main spine
    d.line([(spine_left, y), (spine_right, y)], fill=SPINE, width=6)
    # tick marks: past, present, future
    for tx, label_w in ((0.20, 8), (0.50, 12), (0.80, 8)):
        x = spine_left + (spine_right - spine_left) * tx
        d.line([(x, y - label_w), (x, y + label_w)], fill=SPINE_HI, width=4)


def _draw_markers(layer: Image.Image) -> None:
    """Character dot (present) + event star (future) anchored on the spine."""
    d = ImageDraw.Draw(layer, "RGBA")
    y = CY
    spine_left  = W * 0.10
    spine_right = W * 0.90
    span = spine_right - spine_left

    # --- Character dot at ~35% ("present" on the timeline) ---
    cx_char = spine_left + span * 0.35
    r_char = 26
    # warm halo
    halo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hd.ellipse((cx_char - r_char * 2, y - r_char * 2, cx_char + r_char * 2, y + r_char * 2),
               fill=(252, 232, 168, 110))
    halo = halo.filter(ImageFilter.GaussianBlur(radius=12))
    layer.alpha_composite(halo)
    # build the disc as its own layer so we can clip the silhouette to it
    disc = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dd = ImageDraw.Draw(disc)
    dd.ellipse((cx_char - r_char, y - r_char, cx_char + r_char, y + r_char), fill=CHAR)
    # silhouette shapes (will be clipped to disc)
    head_r = 7
    head_cy = y - 7
    dd.ellipse((cx_char - head_r, head_cy - head_r, cx_char + head_r, head_cy + head_r),
               fill=(30, 50, 90, 255))
    # shoulders: a wider circle lower down, clipped at disc edge
    sbox = (cx_char - 16, y - 2, cx_char + 16, y + 30)
    dd.ellipse(sbox, fill=(30, 50, 90, 255))
    # rim outline (slightly outside)
    rim_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(rim_layer).ellipse(
        (cx_char - r_char - 1, y - r_char - 1, cx_char + r_char + 1, y + r_char + 1),
        outline=(180, 110, 30, 255), width=4,
    )
    # rim outline (slightly outside)
    rim_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(rim_layer).ellipse(
        (cx_char - r_char - 1, y - r_char - 1, cx_char + r_char + 1, y + r_char + 1),
        outline=(180, 110, 30, 255), width=4,
    )
    layer.alpha_composite(disc)
    layer.alpha_composite(rim_layer)

    # --- Event star at ~70% ("event" marker in the future) ---
    cx_e = spine_left + span * 0.70
    r_e_outer = 34
    r_e_inner = 13
    # soft outer glow
    star_glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sg = ImageDraw.Draw(star_glow)
    sg.ellipse((cx_e - r_e_outer * 2, y - r_e_outer * 2, cx_e + r_e_outer * 2, y + r_e_outer * 2),
               fill=EVENT_GLOW)
    star_glow = star_glow.filter(ImageFilter.GaussianBlur(radius=14))
    layer.alpha_composite(star_glow)
    # 8-point star
    pts = []
    for i in range(16):
        a = -math.pi / 2 + i * (math.pi / 8)
        rr = r_e_outer if i % 2 == 0 else r_e_inner
        pts.append((cx_e + rr * math.cos(a), y + rr * math.sin(a)))
    d.polygon(pts, fill=EVENT)
    d.polygon(pts, outline=(160, 100, 24, 255))
    # crisp spine pass-through so the timeline remains continuous
    d.line([(spine_left, y), (spine_right, y)], fill=SPINE, width=6)


def _draw_compass_rose(layer: Image.Image) -> None:
    """Tiny compass rose in the upper-right to anchor the worldview metaphor."""
    d = ImageDraw.Draw(layer, "RGBA")
    cx = W * 0.82
    cy = H * 0.22
    r = 26
    # outer ring
    d.ellipse((cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4), outline=(186, 220, 240, 140), width=2)
    # 4 cardinal points
    pts = []
    for i in range(8):
        a = -math.pi / 2 + i * (math.pi / 4)
        rr = r if i % 2 == 0 else r * 0.35
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=(186, 220, 240, 200))
    # north needle brighter
    d.polygon([(cx, cy - r), (cx - 6, cy), (cx + 6, cy)], fill=(244, 196, 86, 255))


def render_master() -> Image.Image:
    img = _gradient_bg(W)
    _draw_grid(img)
    _draw_globe(img)
    _draw_timeline(img)
    _draw_markers(img)
    _draw_compass_rose(img)
    return img


def main() -> None:
    master = render_master()

    # PNG variants (Tauri's bundle config explicitly references these)
    targets = {
        "32x32.png":      32,
        "128x128.png":   128,
        "128x128@2x.png": 256,
        "icon.png":      512,
    }
    for name, size in targets.items():
        out = master.resize((size, size), Image.LANCZOS)
        out.save(os.path.join(OUT_DIR, name), format="PNG", optimize=True)

    # Windows .ico with multiple resolutions so the shell picks the right one
    ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master.resize((256, 256), Image.LANCZOS).save(
        os.path.join(OUT_DIR, "icon.ico"),
        format="ICO",
        sizes=ico_sizes,
    )

    # macOS .icns — Pillow's ICNS writer accepts a sizes= kwarg
    master.resize((1024, 1024), Image.LANCZOS).save(
        os.path.join(OUT_DIR, "icon.icns"),
        format="ICNS",
    )

    # Sanity report
    for f in sorted(os.listdir(OUT_DIR)):
        path = os.path.join(OUT_DIR, f)
        if os.path.isfile(path):
            print(f"{f}\t{os.path.getsize(path)} bytes")


if __name__ == "__main__":
    main()
