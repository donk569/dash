"""Rescale facade detail to match the buildings.

Reference scale: player is ~2.2 world units tall, building body ~14-20 units.
So a window should be ~2.5 x 2.2 u (40x36 px @PPU16), a fire escape ~3 x 7 u
(48x112 px), pipes ~0.75 x 6 u.
"""
import os
from PIL import Image, ImageDraw

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites"
SRC = r"D:\unity\dash\_AssetInbox\cyberpunk"

K = (5, 9, 18, 255)
CY = (120, 240, 255, 255)
CY_D = (58, 140, 205, 255)
GRAY = (70, 82, 104, 255)
GRAY_D = (42, 50, 66, 255)
MG = (255, 70, 170, 255)
STEEL = (86, 96, 120, 255)
STEEL_D = (52, 60, 78, 255)


def window():
    W, H = 40, 36
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W - 1, H - 1], outline=K)
    d.rectangle([1, 1, W - 2, H - 2], fill=GRAY_D)
    d.rectangle([4, 4, W - 5, H - 6], fill=(12, 16, 28, 170))
    d.line([(W // 2, 4), (W // 2, H - 6)], fill=GRAY, width=2)
    d.line([(4, H // 2), (W - 5, H // 2)], fill=GRAY, width=2)
    # sill
    d.rectangle([0, H - 6, W - 1, H - 1], fill=GRAY)
    d.line([0, H - 6, W - 1, H - 6], fill=CY_D)
    d.line([1, 2, W - 2, 2], fill=(96, 108, 134, 255))
    im.save(os.path.join(ROOT, "Props", "prop_window.png"))
    print("window", im.size)


def heads():
    jobs = [("1 Biker", "Biker_idle", "npc_window_biker"),
            ("3 Cyborg", "Cyborg_idle", "npc_window_cyborg")]
    out = os.path.join(ROOT, "Npc")
    for folder, strip, prefix in jobs:
        im = Image.open(os.path.join(SRC, folder, strip + ".png")).convert("RGBA")
        for i in range(4):
            frame = im.crop((i * 48, 0, (i + 1) * 48, 48))
            head = frame.crop((12, 0, 36, 24)).resize((24, 24), Image.NEAREST)
            head.save(os.path.join(out, "%s_%d.png" % (prefix, i)))
        print("heads", prefix)


def fire_escape():
    W, H = 48, 112
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for y in range(0, H - 20, 28):
        d.rectangle([2, y, W - 3, y + 4], fill=STEEL)             # platform
        d.line([2, y + 4, 2, y + 26], fill=STEEL_D)               # rails
        d.line([W - 3, y + 4, W - 3, y + 26], fill=STEEL_D)
        d.line([2, y + 26, W - 3, y + 26], fill=STEEL_D)
        d.line([5, y + 26, W - 6, y + 4], fill=STEEL, width=2)    # ladder
        for x in range(6, W - 8, 7):
            d.line([x, y + 4, x, y + 26], fill=STEEL_D)
        d.point((W - 5, y + 2), fill=CY)
    d.line([2, 0, 2, H - 1], fill=STEEL_D, width=2)
    im.save(os.path.join(ROOT, "Tiles", "prop_fire_escape.png"))
    print("fire escape", im.size)


def pipes():
    W, H = 12, 96
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([3, 0, 3, H - 1], fill=STEEL, width=3)
    d.line([8, 0, 8, H - 1], fill=STEEL_D, width=2)
    for y in range(6, H, 18):
        d.line([1, y, 9, y], fill=STEEL, width=2)
    d.point((3, 12), fill=CY)
    im.save(os.path.join(ROOT, "Tiles", "prop_pipes.png"))
    print("pipes", im.size)


def neon_strip():
    W, H = 6, 96
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 0, 4, H - 1], fill=MG)
    d.line([2, 0, 2, H - 1], fill=(255, 200, 235, 255))
    d.line([1, 0, 1, H - 1], fill=(120, 24, 82, 255))
    im.save(os.path.join(ROOT, "Tiles", "prop_neon_strip.png"))
    print("neon", im.size)


if __name__ == "__main__":
    window()
    heads()
    fire_escape()
    pipes()
    neon_strip()
