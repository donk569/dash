"""Generate a Kayn-style scythe to replace prop_sword.png.

Same canvas height (44 px) and pivot (0.5, 0.13) as the katana, so SwordSwing's
sprite-length scaling and the prefab reference keep working unchanged.

Palette follows prop_sword: steel highlight/mid/shadow, dark grip, gold bands.
"""
import os
from PIL import Image, ImageDraw

OUT = r"D:\unity\dash\Assets\_Project\Art\Sprites\Props\prop_sword.png"
W, H = 56, 44

HI = (222, 244, 255, 255)
MID = (162, 196, 224, 255)
LOW = (92, 120, 152, 255)
DARK = (46, 34, 40, 255)
GOLD = (255, 208, 108, 255)
GOLD2 = (176, 128, 44, 255)
CYAN = (120, 240, 255, 255)
K = (5, 9, 18, 255)

img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# ---- shaft (x 27..30, from the grip at the bottom to the blade at the top)
d.rectangle([27, 12, 30, 40], fill=DARK)
d.line([27, 12, 27, 40], fill=LOW)      # lit edge
d.line([30, 12, 30, 40], fill=K)        # shadow edge

# ---- grip wrap + gold collar (pivot sits at image y = 38.3)
d.rectangle([26, 32, 31, 41], fill=DARK)
for y in (34, 36, 38, 40):
    d.line([26, y, 31, y], fill=GOLD2)
d.line([26, 32, 31, 32], fill=GOLD)
# pommel spike
d.polygon([(27, 41), (30, 41), (28, 44), (29, 42)], fill=(70, 52, 58, 255))

# ---- blade: long thin crescent sweeping left, sharp tip at the far left
outer = [(26, 13), (30, 4), (41, 1), (50, 4), (55, 13)]
inner = [(55, 13), (49, 11), (42, 8), (35, 7), (29, 11), (26, 17)]
d.polygon(outer + inner, fill=MID)

# bright cutting edge along the outer arc
for i in range(len(outer) - 1):
    d.line([outer[i], outer[i + 1]], fill=HI, width=2)
# dark spine along the inner arc
for i in range(len(inner) - 1):
    d.line([inner[i], inner[i + 1]], fill=LOW)

# ---- a small glowing notch (Kayn-ish accent) where the blade meets the shaft
d.point((27, 15), fill=CYAN)
d.point((27, 16), fill=CYAN)

# ---- outline pass: any transparent pixel touching the weapon gets a dark rim
px = img.load()
rim = []
for y in range(H):
    for x in range(W):
        if px[x, y][3] != 0:
            continue
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and px[nx, ny][3] != 0:
                    rim.append((x, y))
                    break
            else:
                continue
            break
for p in rim:
    px[p] = K

img.save(OUT)
print("wrote", OUT, img.size)

preview = img.resize((W * 8, H * 8), Image.NEAREST)
preview.save(r"D:\unity\dash\Assets\Screenshots\scythe_preview.png")
print("preview", preview.size)
