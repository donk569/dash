"""Slice the CraftPix cyberpunk Punk strips into per-frame PNGs for the game.

48x48 frames, side view. Names follow the existing player_* convention so the
PixelArtImporter (PPU16, pivot 0.5/0.045) handles them automatically.
"""
import os
from PIL import Image

SRC = r"D:\unity\dash\_AssetInbox\cyberpunk\2 Punk"
DST = r"D:\unity\dash\Assets\_Project\Art\Sprites\Player"
FRAME = 48

STRIPS = [
    ("idle", "punk_idle", 4),
    ("run", "punk_run", 6),
    ("jump", "punk_jump", 4),
    ("attack1", "punk_attack", 6),
    ("hurt", "punk_hurt", 2),
    ("death", "punk_death", 6),
]

for strip, prefix, count in STRIPS:
    im = Image.open(os.path.join(SRC, "Punk_%s.png" % strip)).convert("RGBA")
    assert im.width == FRAME * count, (strip, im.size)
    for i in range(count):
        frame = im.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME))
        out = os.path.join(DST, "%s_%d.png" % (prefix, i))
        frame.save(out)
    print("wrote", prefix, count, "frames")

# preview strip for eyeballing
sheet = Image.new("RGBA", (FRAME * 6, FRAME * len(STRIPS)), (30, 30, 42, 255))
for r, (strip, prefix, count) in enumerate(STRIPS):
    im = Image.open(os.path.join(SRC, "Punk_%s.png" % strip)).convert("RGBA")
    sheet.alpha_composite(im.crop((0, 0, im.width, FRAME)), (0, r * FRAME))
sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(
    r"D:\unity\dash\Assets\Screenshots\punk_frames.png")
print("preview ok")
