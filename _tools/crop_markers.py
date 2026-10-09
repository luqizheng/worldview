from PIL import Image
import os, sys
from collections import Counter

src_dir = 'dsh-image-gen'
dst_dir = 'assets/icons/markers'
os.makedirs(dst_dir, exist_ok=True)

# 6 marker sha256 prefix (with checkerboard bg)
markers = {
  'event_point':   'fe9d6207',
  'task_marker':   '9d491cbd',
  'danger_point':  'b4f44cf3',
  'character_dot': '5c3fca26',
  'vehicle_icon':  '0ac4e889',
  'army_group':    '1d3f10dc',
}
sizes = {'event_point': 16, 'task_marker': 16, 'danger_point': 16,
         'character_dot': 8, 'vehicle_icon': 12, 'army_group': 16}

# checkerboard pattern detection: 4 corners are typically gray
def is_checker(r, g, b):
    # gray = high R+G+B / 3 with small variance
    avg = (r + g + b) / 3
    var = max(r, g, b) - min(r, g, b)
    return abs(var) < 20 and avg > 100 and avg < 220

for name, sha in markers.items():
  src = f'{src_dir}/image-{sha}.jpg'
  img = Image.open(src).convert('RGBA')
  W, H = img.size
  new = Image.new('RGBA', (W, H), (0,0,0,0))
  src_px = img.load()
  dst_px = new.load()
  for y in range(H):
    for x in range(W):
      r,g,b,a = src_px[x,y]
      if not is_checker(r, g, b):
        # also kill near-white for crispness
        if (r+g+b)/3 < 240:
          dst_px[x,y] = (r,g,b,255)
  # bbox
  min_x, min_y, max_x, max_y = W, H, 0, 0
  for y in range(H):
    for x in range(W):
      if dst_px[x,y][3] > 0:
        if x < min_x: min_x = x
        if y < min_y: min_y = y
        if x > max_x: max_x = x
        if y > max_y: max_y = y
  if max_x < min_x:
    sys.stdout.write(f'{name}: EMPTY\n')
    continue
  # also clip 2px outer transparent border
  pad = 0
  cx0, cy0 = max(0, min_x-pad), max(0, min_y-pad)
  cx1, cy1 = min(W, max_x+1+pad), min(H, max_y+1+pad)
  cropped = new.crop((cx0, cy0, cx1, cy1))
  target = sizes[name]
  out = cropped.resize((target, target), Image.NEAREST)
  dst = f'{dst_dir}/{name}.png'
  out.save(dst)
  sys.stdout.write(f'{name}: bbox=({min_x},{min_y})-({max_x},{max_y}) -> {target}x{target}\n')
sys.stdout.write('done\n')
