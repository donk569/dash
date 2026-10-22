"""Slice Biker / Cyborg idle strips from the cyberpunk pack into NPC frames."""
import os
from PIL import Image

SRC = r"D:\unity\dash\_AssetInbox\cyberpunk"
DST = r"D:\unity\dash\Assets\_Project\Art\Sprites\Npc"
FRAME = 48

JOBS = [
    ("1 Biker", "Biker_idle", "npc_biker", 4),
    ("3 Cyborg", "Cyborg_idle", "npc_cyborg", 4),
]

os.makedirs(DST, exist_ok=True)
for folder, strip, prefix, count in JOBS:
    im = Image.open(os.path.join(SRC, folder, strip + ".png")).convert("RGBA")
    assert im.width == FRAME * count, (strip, im.size)
    for i in range(count):
        frame = im.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME))
        frame.save(os.path.join(DST, "%s_%d.png" % (prefix, i)))
    print("wrote", prefix, count)

sheet = Image.new("RGBA", (FRAME * 8, FRAME), (30, 32, 44, 255))
for r, (folder, strip, prefix, count) in enumerate(JOBS):
    im = Image.open(os.path.join(SRC, folder, strip + ".png")).convert("RGBA")
    sheet.alpha_composite(im.crop((0, 0, FRAME * count, FRAME)), (r * FRAME * count, 0))
sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(
    r"D:\unity\dash\Assets\Screenshots\npc_sheet.png")
print("preview ok")
