"""Generate rooftop decor, neon signs, a new dash icon and a nicer shop sprite.

All art is 1:1 pixel art at PPU16, cyberpunk palette (cyan / magenta / warm windows).
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites"

K = (5, 9, 18, 255)
CY = (120, 240, 255, 255)
CY_D = (58, 140, 205, 255)
MG = (255, 70, 170, 255)
YE = (255, 214, 120, 255)
OR = (236, 120, 9, 255)
GRAY = (70, 82, 104, 255)
GRAY_D = (42, 50, 66, 255)
WHITE = (252, 252, 252, 255)
RED = (219, 27, 52, 255)
GREEN = (62, 204, 138, 255)
PURPLE = (147, 39, 143, 255)


def save(img, folder, name):
    d = os.path.join(ROOT, folder)
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name + ".png")
    img.save(p)
    print("wrote", p, img.size)


def outlined(img, rim=K):
    """1px dark rim around every opaque cluster."""
    px = img.load()
    w, h = img.size
    rim_px = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] != 0:
                continue
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] != 0:
                        rim_px.append((x, y))
                        break
                else:
                    continue
                break
    for p in rim_px:
        px[p] = rim
    return img


# ---------------------------------------------------------------- dash icon
def dash_icon():
    im = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # three speed chevrons
    for i, x in enumerate((2, 8, 14)):
        c = CY if i < 2 else WHITE
        d.line([(x, 5), (x + 5, 12), (x, 19)], fill=c, width=2)
    # energy tail
    d.line([(1, 12), (4, 12)], fill=CY_D, width=1)
    outlined(im)
    save(im, "UI", "ui_dash_icon")


# ---------------------------------------------------------------- shop
def shop():
    W, H = 48, 64
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # body
    d.rectangle([8, 6, 40, 62], fill=(24, 28, 42, 255))
    d.rectangle([8, 6, 40, 62], outline=GRAY_D)
    # neon frame
    d.rectangle([10, 8, 38, 60], outline=CY)
    # awning
    for i in range(0, 30, 6):
        d.rectangle([9 + i, 10, 13 + i, 14], fill=MG if (i // 6) % 2 else WHITE)
    # warm window
    d.rectangle([12, 18, 36, 38], fill=(255, 196, 110, 255))
    d.rectangle([12, 18, 36, 38], outline=OR)
    # shelves / goods
    for y in (24, 30):
        d.line([13, y, 35, y], fill=(176, 120, 60, 255))
    for x, c in ((15, CY), (19, MG), (23, GREEN), (27, RED), (31, YE)):
        d.rectangle([x, 19, x + 2, 23], fill=c)
    # door
    d.rectangle([20, 42, 30, 62], fill=(30, 36, 52, 255))
    d.rectangle([20, 42, 30, 62], outline=CY_D)
    d.line([25, 42, 25, 62], fill=CY_D)
    # sign board on top
    d.rectangle([6, 0, 42, 7], fill=(18, 22, 34, 255))
    d.rectangle([6, 0, 42, 7], outline=MG)
    d.text((16, 0), "SHOP", font=ImageFont.load_default(), fill=MG)
    save(outlined(im), "Props", "prop_shop")


# ---------------------------------------------------------------- rooftop props
def laundry():
    im = Image.new("RGBA", (36, 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([1, 3, 34, 3], fill=GRAY, width=1)          # line
    for x, c in ((4, CY), (12, MG), (20, WHITE), (28, YE)):
        d.rectangle([x, 4, x + 5, 13], fill=c)          # cloth
        d.rectangle([x, 4, x + 5, 6], fill=(255, 255, 255, 90))
        d.point((x + 2, 3), fill=GRAY_D)
        d.point((x + 4, 3), fill=GRAY_D)
    save(outlined(im), "Props", "prop_laundry")


def ac_unit():
    im = Image.new("RGBA", (16, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 3, 14, 10], fill=GRAY)
    d.rectangle([1, 3, 14, 10], outline=K)
    d.ellipse([4, 5, 10, 11], outline=GRAY_D)
    d.ellipse([5, 6, 9, 10], outline=CY_D)
    d.line([11, 4, 13, 4], fill=WHITE)
    save(im, "Props", "prop_ac")


def water_tank():
    im = Image.new("RGBA", (14, 18), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([2, 4, 11, 15], fill=GRAY)
    d.rectangle([2, 4, 11, 15], outline=K)
    d.ellipse([2, 1, 11, 7], fill=GRAY_D, outline=K)
    d.line([4, 15, 4, 17], fill=K)
    d.line([9, 15, 9, 17], fill=K)
    d.line([4, 8, 9, 8], fill=CY_D)
    save(im, "Props", "prop_tank")


def plant():
    im = Image.new("RGBA", (12, 14), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([3, 9, 8, 13], fill=OR)
    d.rectangle([3, 9, 8, 13], outline=K)
    d.line([5, 9, 4, 4], fill=GREEN)
    d.line([6, 9, 7, 3], fill=GREEN)
    d.line([6, 7, 9, 6], fill=GREEN)
    d.line([5, 6, 2, 5], fill=GREEN)
    save(outlined(im), "Props", "prop_plant")


def table():
    im = Image.new("RGBA", (18, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 3, 16, 5], fill=(196, 132, 72, 255))
    d.rectangle([1, 3, 16, 5], outline=K)
    d.line([3, 5, 3, 11], fill=GRAY_D)
    d.line([14, 5, 14, 11], fill=GRAY_D)
    d.rectangle([6, 1, 8, 3], fill=CY)
    save(im, "Props", "prop_table")


def cat():
    im = Image.new("RGBA", (10, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([1, 2, 7, 7], fill=(28, 24, 36, 255))
    d.ellipse([2, 1, 6, 5], fill=(28, 24, 36, 255))
    d.point((3, 1), fill=MG)
    d.point((5, 1), fill=MG)
    d.point((3, 2), fill=CY)
    d.point((5, 2), fill=CY)
    d.line([7, 4, 9, 1], fill=(28, 24, 36, 255))
    save(im, "Props", "prop_cat")


# ---------------------------------------------------------------- neon signs (text baked)
def sign(name, text, color):
    font = ImageFont.load_default()
    tw = font.getbbox(text)[2] - font.getbbox(text)[0]
    W = max(28, tw + 8)
    H = 12
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W - 1, H - 1], fill=(14, 18, 30, 255), outline=color)
    d.text((4, 1), text, font=font, fill=color)
    save(im, "Props", name)


if __name__ == "__main__":
    dash_icon()
    shop()
    laundry()
    ac_unit()
    water_tank()
    plant()
    table()
    cat()
    sign("sign_24h", "24H", CY)
    sign("sign_laundry", "LAUNDRY", MG)
    sign("sign_ac", "AC", YE)
    sign("sign_ramen", "RAMEN", RED)
    sign("sign_roof", "ROOFTOP", GREEN)
