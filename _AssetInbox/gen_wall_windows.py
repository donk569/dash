"""Facade tiles that read like the background skyline: dark wall + lit window grids."""
import os
from PIL import Image, ImageDraw

TILES = r"D:\unity\dash\Assets\_Project\Art\Sprites\Tiles"

WALL = (30, 26, 48, 255)      # same family as the background silhouettes
WALL_D = (20, 17, 33, 255)
WALL_L = (42, 37, 64, 255)
CY = (120, 240, 255, 255)
MG = (255, 70, 170, 255)
WARM = (255, 196, 110, 255)
GREEN = (62, 204, 138, 255)


def tiled_windows(name, base, win_colors, seed_shift=0):
    """16x16 tile: one window cell, so tiling produces a regular grid of windows."""
    im = Image.new("RGBA", (16, 16), base)
    d = ImageDraw.Draw(im)
    # subtle panel seams
    d.line([0, 15, 15, 15], fill=WALL_D)
    d.line([15, 0, 15, 15], fill=WALL_D)
    d.line([0, 0, 15, 0], fill=WALL_L)
    # window 4x6 at (3,4)
    for i, c in enumerate(win_colors):
        ox = (i * 7 + seed_shift) % 9
        d.rectangle([3 + ox % 2, 4, 6 + ox % 2, 9], fill=c)
        d.rectangle([3 + ox % 2, 4, 6 + ox % 2, 5], fill=(255, 255, 255, 120))
    im.save(os.path.join(TILES, name))


def make():
    os.makedirs(TILES, exist_ok=True)

    # E: cyan windows, cold wall
    tiled_windows("wall_e.png", (28, 26, 46, 255), [CY, CY, (70, 140, 180, 255)])

    # F: magenta + warm windows (busier street)
    tiled_windows("wall_f.png", (34, 26, 44, 255), [MG, WARM, MG], seed_shift=3)

    # G: sparse windows (most cells dark) -> calmer building
    im = Image.new("RGBA", (16, 16), (26, 23, 40, 255))
    d = ImageDraw.Draw(im)
    d.line([0, 15, 15, 15], fill=WALL_D)
    d.line([15, 0, 15, 15], fill=WALL_D)
    d.rectangle([4, 5, 7, 10], fill=(40, 36, 58, 255))
    d.line([4, 5, 7, 5], fill=(52, 48, 72, 255))
    im.save(os.path.join(TILES, "wall_g.png"))

    # replace the old bright neon-line wall with a calmer panel wall
    im = Image.new("RGBA", (16, 16), (33, 28, 46, 255))
    d = ImageDraw.Draw(im)
    d.line([0, 0, 15, 0], fill=(48, 42, 66, 255))
    d.line([0, 15, 15, 15], fill=WALL_D)
    d.line([7, 0, 7, 15], fill=(28, 24, 38, 255))
    d.point((12, 6), fill=CY)
    im.save(os.path.join(TILES, "wall_d.png"))
    print("walls ok")


if __name__ == "__main__":
    make()
