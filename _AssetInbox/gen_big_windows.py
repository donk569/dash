"""Big sparse windows (open-plan glass), hazard-striped floating deck, star & spark sprites."""
import os
from PIL import Image, ImageDraw

TILES = r"D:\unity\dash\Assets\_Project\Art\Sprites\Tiles"
BG = r"D:\unity\dash\Assets\_Project\Art\Sprites\Bg"

WALL = (34, 28, 52, 255)
WALL_D = (22, 18, 36, 255)
WALL_L = (48, 40, 70, 255)
CY = (120, 240, 255, 255)
MG = (255, 70, 170, 255)
WARM = (255, 196, 110, 255)
DIM = (46, 40, 64, 255)
HAZ = (232, 186, 60, 255)


def big_wall(name, fill, lit, second=None):
    """32x32 tile with ONE big window -> sparse, readable facade."""
    im = Image.new("RGBA", (32, 32), fill)
    d = ImageDraw.Draw(im)
    d.line([0, 31, 31, 31], fill=WALL_D)
    d.line([31, 0, 31, 31], fill=WALL_D)
    d.line([0, 0, 31, 0], fill=WALL_L)
    # big glass panel 14x12 at (7,8)
    d.rectangle([7, 8, 20, 19], fill=lit)
    d.rectangle([7, 8, 20, 10], fill=(255, 255, 255, 60))
    d.rectangle([7, 8, 20, 19], outline=WALL_D)
    d.line([13, 8, 13, 19], fill=WALL_D)
    if second is not None:
        d.rectangle([7, 23, 20, 28], fill=second)
    im.save(os.path.join(TILES, name))


def deck():
    """16x16 hazard-striped floating deck edge (slab is 1 unit = 16px tall)."""
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 15, 8], fill=(52, 58, 78, 255))     # metal deck
    d.line([0, 0, 15, 0], fill=(140, 152, 180, 255))       # top highlight
    d.line([0, 1, 15, 1], fill=(74, 82, 104, 255))
    for x in range(-8, 20, 6):                             # hazard stripes
        d.polygon([(x, 9), (x + 3, 9), (x + 6, 15), (x + 3, 15)], fill=HAZ)
        d.polygon([(x + 3, 9), (x + 6, 9), (x + 9, 15), (x + 6, 15)], fill=(26, 22, 34, 255))
    im.save(os.path.join(TILES, "tile_slab_hazard.png"))


def sprites():
    s = Image.new("RGBA", (3, 3), (0, 0, 0, 0))
    ImageDraw.Draw(s).rectangle([0, 0, 2, 2], fill=(255, 255, 255, 255))
    s.save(os.path.join(BG, "star.png"))

    sp = Image.new("RGBA", (2, 2), (0, 0, 0, 0))
    ImageDraw.Draw(sp).rectangle([0, 0, 1, 1], fill=(255, 255, 255, 255))
    sp.save(os.path.join(BG, "spark.png"))
    print("sprites ok")


if __name__ == "__main__":
    big_wall("wall_e.png", WALL, (255, 150, 120, 200), (60, 52, 84, 255))
    big_wall("wall_f.png", (38, 30, 50, 255), (120, 200, 255, 170), None)
    big_wall("wall_g.png", (28, 24, 42, 255), (70, 60, 96, 255), None)
    deck()
    sprites()
    print("done")
