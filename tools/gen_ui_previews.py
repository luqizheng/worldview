"""生成 UI 面板用的占位预览贴图（spec §6 scenes/ui/preview/）。"""
from PIL import Image, ImageDraw
import os

OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "scenes",
    "ui",
    "preview",
)
os.makedirs(OUT, exist_ok=True)


def building_preview() -> Image.Image:
    """64x64：一栋简化的农舍（屋顶 + 墙体 + 门）。"""
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # 墙
    d.rectangle([16, 30, 47, 54], fill=(198, 178, 138, 255), outline=(120, 104, 78, 255))
    # 屋顶
    d.polygon([(12, 30), (32, 12), (51, 30)], fill=(150, 86, 66, 255), outline=(104, 58, 44, 255))
    # 门
    d.rectangle([28, 40, 36, 54], fill=(96, 70, 50, 255))
    # 窗
    d.rectangle([20, 36, 25, 41], fill=(230, 226, 200, 255))
    return img


def terrain_preview() -> Image.Image:
    """64x64：3x3 草地格，带草纹理点缀。"""
    img = Image.new("RGBA", (64, 64), (63, 122, 63, 255))
    d = ImageDraw.Draw(img)
    for i in (0, 21, 42, 63):
        d.line([(i, 0), (i, 63)], fill=(48, 100, 48, 255))
        d.line([(0, i), (63, i)], fill=(48, 100, 48, 255))
    for x, y in ((10, 12), (30, 8), (48, 22), (18, 34), (38, 44), (8, 52), (52, 54)):
        d.point((x, y), fill=(120, 168, 96, 255))
        d.point((x + 1, y + 1), fill=(120, 168, 96, 255))
    return img


building_preview().save(os.path.join(OUT, "building_preview.png"))
terrain_preview().save(os.path.join(OUT, "terrain_preview.png"))
print("written:", os.listdir(OUT))
