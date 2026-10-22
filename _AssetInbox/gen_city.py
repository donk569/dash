"""Cyberpunk city pass: coloured background layers, 4 facade tiles, fire escapes & pipes.

Goal: kill the "one flat navy silhouette + one wall tile" look without rebuilding the level.
All patterns wrap horizontally so parallax / tiling stays seamless.
"""
import os
import random
from PIL import Image, ImageDraw

BG = r"D:\unity\dash\Assets\_Project\Art\Sprites\Bg"
PROPS = r"D:\unity\dash\Assets\_Project\Art\Sprites\Tiles"

CY = (120, 240, 255, 255)
MG = (255, 70, 170, 255)
YE = (255, 214, 120, 255)
OR = (236, 120, 9, 255)
GREEN = (62, 204, 138, 255)
WARM = (255, 196, 110, 255)
K = (5, 9, 18, 255)


def city_layer(w, h, base, body, lit_colors, buildings, seed, min_top, max_top, alpha=255):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(seed)
    for i in range(buildings):
        bw = rng.randint(w // buildings - 12, w // buildings + 12)
        x = i * (w // buildings) + rng.randint(-6, 6)
        top = rng.randint(min_top, max_top)
        col = body if rng.random() > 0.35 else tuple(min(255, c + 10) for c in body[:3]) + (alpha,)
        d.rectangle([x, top, x + bw, h], fill=col)
        # roof clutter
        if rng.random() < 0.5:
            cx = x + rng.randint(2, max(3, bw - 8))
            cw = rng.randint(2, 6)
            d.rectangle([cx, top - rng.randint(4, 10), cx + cw, top], fill=col)
        # lit windows
        for wy in range(top + 6, h - 4, 7):
            for wx in range(x + 3, x + bw - 3, 6):
                if rng.random() < 0.42:
                    c = rng.choice(lit_colors)
                    d.rectangle([wx, wy, wx + 2, wy + 2], fill=c)
    return im


def backgrounds():
    # far: cold violet silhouettes with sparse neon
    far = city_layer(384, 224, None, (26, 22, 44, 255), [CY, MG, (120, 110, 190, 255)],
                     buildings=12, seed=11, min_top=70, max_top=150)
    far.save(os.path.join(BG, "bg_far.png"))

    # mid: purple-blue with more neon signs
    mid = city_layer(1009, 224, None, (20, 18, 38, 255), [CY, MG, YE, GREEN],
                     buildings=26, seed=22, min_top=60, max_top=150)
    d = ImageDraw.Draw(mid)
    rng = random.Random(7)
    for i in range(14):
        x = rng.randint(0, 1009 - 40)
        y = rng.randint(70, 160)
        c = rng.choice([MG, CY, YE, GREEN])
        d.rectangle([x, y, x + 26, y + 4], fill=c)          # neon strip
        d.rectangle([x, y, x + 2, y + 4], fill=(255, 255, 255, 255))
    mid.save(os.path.join(BG, "bg_mid.png"))

    # near: darkest, big shapes, few lights
    near = city_layer(493, 209, None, (12, 14, 26, 255), [CY, MG],
                      buildings=9, seed=33, min_top=40, max_top=120)
    near.save(os.path.join(BG, "bg_near.png"))
    print("backgrounds ok")


# ---------------------------------------------------------------- facade tiles (16x16, tileable)
def facade_tiles():
    os.makedirs(PROPS, exist_ok=True)

    # A: concrete panels with seams
    a = Image.new("RGBA", (16, 16), (44, 50, 68, 255))
    d = ImageDraw.Draw(a)
    d.line([0, 0, 15, 0], fill=(56, 64, 84, 255))
    d.line([0, 8, 15, 8], fill=(34, 39, 54, 255))
    d.line([0, 0, 0, 15], fill=(34, 39, 54, 255))
    d.line([15, 0, 15, 15], fill=(34, 39, 54, 255))
    a.save(os.path.join(PROPS, "wall_a.png"))

    # B: cyber brick (plum bricks + cyan mortar)
    b = Image.new("RGBA", (16, 16), (62, 40, 58, 255))
    d = ImageDraw.Draw(b)
    for row in range(0, 16, 4):
        d.line([0, row, 15, row], fill=(38, 24, 38, 255))
        off = 0 if (row // 4) % 2 == 0 else 4
        for x in range(off, 16, 8):
            d.line([x, row, x, row + 3], fill=(38, 24, 38, 255))
    d.line([0, 0, 15, 0], fill=(78, 52, 72, 255))
    b.save(os.path.join(PROPS, "wall_b.png"))

    # C: glass grid
    c = Image.new("RGBA", (16, 16), (16, 30, 44, 255))
    d = ImageDraw.Draw(c)
    d.line([0, 0, 15, 0], fill=(30, 70, 96, 255))
    d.line([0, 8, 15, 8], fill=(30, 70, 96, 255))
    d.line([0, 0, 0, 15], fill=(30, 70, 96, 255))
    d.line([8, 0, 8, 15], fill=(30, 70, 96, 255))
    d.rectangle([2, 2, 6, 6], fill=(24, 52, 74, 255))
    d.rectangle([10, 10, 14, 14], fill=(24, 52, 74, 255))
    c.save(os.path.join(PROPS, "wall_c.png"))

    # D: panelled wall with a neon floor line（水平线读起来像楼层，比竖条重复感低）
    e = Image.new("RGBA", (16, 16), (34, 30, 46, 255))
    d = ImageDraw.Draw(e)
    d.line([0, 0, 15, 0], fill=MG)                       # neon floor line
    d.line([0, 1, 15, 1], fill=(70, 24, 62, 255))
    d.line([0, 15, 15, 15], fill=(24, 20, 34, 255))
    d.line([7, 0, 7, 15], fill=(28, 24, 38, 255))        # panel seam
    e.save(os.path.join(PROPS, "wall_d.png"))
    print("wall tiles ok")


# ---------------------------------------------------------------- facade props
def facade_props():
    # fire escape: 12 x 40, steel with cyan glints
    W, H = 12, 40
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for y in range(0, H - 8, 10):
        d.rectangle([1, y, 10, y + 2], fill=(56, 62, 82, 255))     # platform
        d.line([1, y + 2, 1, y + 9], fill=(40, 46, 62, 255))        # rails
        d.line([10, y + 2, 10, y + 9], fill=(40, 46, 62, 255))
        d.line([2, y + 9, 9, y + 2], fill=(74, 82, 104, 255))       # ladder
    d.line([1, 0, 1, H - 1], fill=(30, 34, 48, 255))
    d.point((6, 5), fill=CY)
    im.save(os.path.join(PROPS, "prop_fire_escape.png"))

    # pipes + cable bundle
    W, H = 8, 48
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([2, 0, 2, H - 1], fill=(58, 64, 84, 255), width=2)
    d.line([5, 0, 5, H - 1], fill=(40, 46, 62, 255))
    for y in range(3, H, 11):
        d.line([1, y, 6, y], fill=(74, 82, 104, 255))
    d.point((2, 8), fill=CY)
    im.save(os.path.join(PROPS, "prop_pipes.png"))

    # neon strip (vertical, glowing)
    W, H = 4, 48
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 0, 2, H - 1], fill=MG)
    d.line([1, 0, 1, H - 1], fill=(255, 200, 235, 255))
    im.save(os.path.join(PROPS, "prop_neon_strip.png"))
    print("facade props ok")


if __name__ == "__main__":
    backgrounds()
    facade_tiles()
    facade_props()
