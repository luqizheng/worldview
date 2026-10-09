"""Generate 6-frame node sprite atlas (16x16 each, 96x16 total).
Frames: 0=square_filled / 1=square_hollow / 2=circle_filled /
        3=triangle_filled / 4=diamond_filled / 5=diamond_glow
"""
from PIL import Image, ImageDraw

OUTLINE = (90, 58, 26, 255)        # #5a3a1a
GLOW    = (138, 74, 200, 255)      # #8a4ac8 magic_glow
WHITE   = (255, 248, 231, 255)     # #fff8e7

W, H, FRAME = 96, 16, 16
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

def cell_x(i):
    return i * FRAME

# Frame 0: solid filled square 8x8 (4px from edge in 16x16 = (4,4)→(11,11))
i = 0
x0, y0 = cell_x(i) + 4, 4
draw.rectangle([x0, y0, x0+7, y0+7], fill=OUTLINE)

# Frame 1: hollow square 8x8 outline only
i = 1
x0, y0 = cell_x(i) + 4, 4
draw.rectangle([x0, y0, x0+7, y0+7], outline=OUTLINE, width=1)

# Frame 2: filled circle (8x8 bbox)
i = 2
x0, y0 = cell_x(i) + 4, 4
draw.ellipse([x0, y0, x0+7, y0+7], fill=OUTLINE)

# Frame 3: filled triangle pointing up (8x6)
i = 3
x0, y0 = cell_x(i) + 4, 5
draw.polygon([(x0+3, y0), (x0+6, y0+5), (x0, y0+5)], fill=OUTLINE)

# Frame 4: filled diamond 8x8
i = 4
x0, y0 = cell_x(i) + 4, 4
cx, cy = x0 + 3, y0 + 3
draw.polygon([(cx, y0), (x0+7, cy), (cx, y0+7), (x0, cy)], fill=OUTLINE)

# Frame 5: diamond + 2px glow halo
i = 5
x0, y0 = cell_x(i) + 4, 4
cx, cy = x0 + 3, y0 + 3
# glow halo (2px outer)
for r, alpha in [(5, 50), (4, 90)]:
    glow_layer = (GLOW[0], GLOW[1], GLOW[2], alpha)
    draw.polygon([(cx, y0-r), (x0+7+r, cy), (cx, y0+7+r), (x0-r, cy)], fill=glow_layer)
# diamond
draw.polygon([(cx, y0), (x0+7, cy), (cx, y0+7), (x0, cy)], fill=OUTLINE)

img.save('assets/icons/route_nodes_atlas.png')
print('route_nodes_atlas.png saved (96x16)')
