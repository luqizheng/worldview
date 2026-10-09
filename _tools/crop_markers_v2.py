from PIL import Image
import os, sys
from collections import Counter

src_dir = 'dsh-image-gen'
dst_dir = 'assets/icons/markers'
os.makedirs(dst_dir, exist_ok=True)

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

for name, sha in markers.items():
  src = f'{src_dir}/image-{sha}.jpg'
  img = Image.open(src).convert('RGBA')
  W, H = img.size
  # top-left 200x200 → 2 checker colors
  samples = []
  for y in range(0, 200, 4):
    for x in range(0, 200, 4):
      samples.append(img.getpixel((x,y))[:3])
  bucket = Counter()
  for r,g,b in samples:
    bucket[(r//12, g//12, b//12)] += 1
  top2 = bucket.most_common(2)
  cb1 = (top2[0][0][0]*12+6, top2[0][0][1]*12+6, top2[0][0][2]*12+6)
  cb2 = (top2[1][0][0]*12+6, top2[1][0][1]*12+6, top2[1][0][2]*12+6) if len(top2) > 1 else cb1

  def is_bg(r, g, b):
    for cb in (cb1, cb2):
      d2 = (r-cb[0])**2 + (g-cb[1])**2 + (b-cb[2])**2
      if d2 < 50*50:
        return True
    return False

  new = Image.new('RGBA', (W, H), (0,0,0,0))
  src_px = img.load()
  dst_px = new.load()
  for y in range(H):
    for x in range(W):
      r,g,b,a = src_px[x,y]
      if not is_bg(r, g, b):
        dst_px[x,y] = (r,g,b,255)
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
  pad = 2
  cx0, cy0 = max(0, min_x-pad), max(0, min_y-pad)
  cx1, cy1 = min(W, max_x+1+pad), min(H, max_y+1+pad)
  cropped = new.crop((cx0, cy0, cx1, cy1))
  target = sizes[name]
  out = cropped.resize((target, target), Image.NEAREST)
  dst = f'{dst_dir}/{name}.png'
  out.save(dst)
  sys.stdout.write(f'{name}: cb1={cb1} cb2={cb2} bbox=({min_x},{min_y})-({max_x},{max_y}) -> {target}x{target}\n')
sys.stdout.write('done\n')
