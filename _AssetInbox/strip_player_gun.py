"""Strip the baked-in pistol from the shoot/crouch frames and save clean attack poses.

The pistol is drawn with the navy (50,55,81) / lavender (129,112,154) colours in a
small region next to the right fist. We erase those plus the outline pixels that
only touch the pistol, never touching skin/jacket/hair pixels.
"""
import os
from PIL import Image

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites\Player"
NAVY = (50, 55, 81, 255)
NAVY2 = (48, 55, 81, 255)
LAV = (129, 112, 154, 255)
K = (5, 9, 18, 255)
S = (255, 177, 100, 255)
Y = (255, 216, 0, 255)
O = (236, 120, 9, 255)
P = (68, 43, 97, 255)

JOBS = [
    ("player_shoot_0", "player_attack_0", (40, 9, 53, 19)),
    ("player_crouch_0", "player_charge_0", (40, 27, 53, 37)),
]


def strip(src, dst, box):
    img = Image.open(os.path.join(ROOT, src + ".png")).convert("RGBA")
    x0, y0, x1, y1 = box
    prop = set()
    for y in range(y0, y1):
        for x in range(x0, x1):
            c = img.getpixel((x, y))
            if c in (NAVY, NAVY2, LAV):
                prop.add((x, y))
    # outline pixels touching only the prop
    remove = set(prop)
    for (x, y) in prop:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                p = (x + dx, y + dy)
                if p in prop:
                    continue
                if img.getpixel(p) == K:
                    remove.add(p)
    for p in remove:
        img.putpixel(p, (0, 0, 0, 0))
    img.save(os.path.join(ROOT, dst + ".png"))
    print("wrote", dst, "erased", len(remove))


if __name__ == "__main__":
    for src, dst, box in JOBS:
        strip(src, dst, box)

    sheet = Image.new("RGBA", (61 * 2, 57), (24, 24, 34, 255))
    for i, (_, dst, _) in enumerate(JOBS):
        im = Image.open(os.path.join(ROOT, dst + ".png")).convert("RGBA")
        sheet.paste(im, (61 * i, 0), im)
    sheet = sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST)
    sheet.save(r"D:\unity\dash\Assets\Screenshots\attack_clean.png")
    print("preview", sheet.size)
