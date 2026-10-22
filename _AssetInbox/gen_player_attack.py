"""Generate 3 iai attack frames for the player (v2).

v2 fixes from review: tapered sleeve (wide shoulder -> narrow wrist), armpit gap
filled so the arm connects to the torso, hand centred on the sleeve axis, and a
wrist cuff line. Frames: guard (hand at the belly), mid cut, full extension.

Sword grip -> art px:  x = 30.5 + local_x/0.04875 ,  y = 54.435 - local_y/0.04875
  guard  (-0.02, 1.12) -> (30, 31)   (hand kept at 32,30 to stay clear of the off hand)
  mid    ( 0.55, 1.27) -> (42, 28)
  end    ( 0.72, 1.24) -> (45, 29)
"""
import os
from PIL import Image

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites\Player"
BASE = os.path.join(ROOT, "player_idle_0.png")

K = (5, 9, 18, 255)
Y = (255, 216, 0, 255)
O = (236, 120, 9, 255)
S = (255, 177, 100, 255)
B = (177, 92, 81, 255)

FRAMES = {
    "player_attack_0": {"hand": (32, 30), "elbow": (36, 25), "shoulder": (35, 19)},
    "player_attack_1": {"hand": (42, 28), "elbow": (39, 24), "shoulder": (35, 19)},
    "player_attack_2": {"hand": (45, 29), "elbow": (41, 23), "shoulder": (35, 19)},
}

# radius at shoulder / elbow / hand
R_SHOULDER, R_ELBOW, R_HAND = 3.1, 2.6, 2.0


def lerp(a, b, t):
    return a + (b - a) * t


def px_set(points, radii):
    """Pixels within an interpolated radius of a 3-point polyline."""
    out = set()
    for i in range(len(points) - 1):
        (x0, y0), (x1, y1) = points[i], points[i + 1]
        r0, r1 = radii[i], radii[i + 1]
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for s in range(steps + 1):
            u = s / max(1, steps)
            cx, cy, r = lerp(x0, x1, u), lerp(y0, y1, u), lerp(r0, r1, u)
            r2 = r * r
            for y in range(int(cy - r) - 1, int(cy + r) + 2):
                for x in range(int(cx - r) - 1, int(cx + r) + 2):
                    if (x - cx) ** 2 + (y - cy) ** 2 <= r2:
                        out.add((x, y))
    return out


def erase_old_arm(img):
    skin = set()
    for y in range(13, 26):
        for x in range(34, 47):
            if img.getpixel((x, y)) == S:
                skin.add((x, y))
    remove = set(skin)
    for (x, y) in skin:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                p = (x + dx, y + dy)
                if img.getpixel(p) == K:
                    remove.add(p)
    for p in remove:
        img.putpixel(p, (0, 0, 0, 0))


def fill_armpit(img, core):
    """Paint jacket yellow into transparent gaps between the torso and the sleeve."""
    for y in range(18, 30):
        xs = [x for (x, cy) in core if cy == y]
        if not xs:
            continue
        left = min(xs)
        # torso right edge on this row: rightmost non-transparent px left of the sleeve
        torso = None
        for x in range(left - 1, 22, -1):
            c = img.getpixel((x, y))
            if c[3] != 0:
                torso = x
                break
        if torso is None:
            continue
        for x in range(torso + 1, left):
            if img.getpixel((x, y))[3] == 0:
                img.putpixel((x, y), Y)


def draw_arm(img, hand, elbow, shoulder):
    pts = [shoulder, elbow, hand]
    radii = [R_SHOULDER, R_ELBOW, R_HAND]
    core = px_set(pts, radii)
    outline = px_set(pts, [r + 1.1 for r in radii]) - core

    fill_armpit(img, core)

    for p in outline:
        img.putpixel(p, K)
    for p in core:
        img.putpixel(p, Y)

    # sleeve shadow: bottom pixel of each column that is part of the sleeve
    for x in range(img.width):
        ys = sorted(y for (cx, y) in core if cx == x)
        if len(ys) >= 3:
            img.putpixel((x, ys[-1]), O)

    # wrist cuff: 2 px perpendicular to the forearm, just before the hand
    dx, dy = hand[0] - elbow[0], hand[1] - elbow[1]
    ln = max(1.0, (dx * dx + dy * dy) ** 0.5)
    ux, uy = dx / ln, dy / ln
    wx, wy = hand[0] - ux * 3, hand[1] - uy * 3
    for off in (-1, 0, 1):
        img.putpixel((round(wx - uy * off), round(wy + ux * off)), K)

    # hand: 3x3 skin block centred on the grip point, outlined, brick shadow bottom
    hx, hy = hand
    for dx2 in (-1, 0, 1):
        for dy2 in (-1, 0, 1):
            if abs(dx2) + abs(dy2) <= 2:
                img.putpixel((hx + dx2, hy + dy2), S)
    for dx2, dy2 in [(-2, 0), (2, 0), (0, -2), (-2, -1), (2, -1), (-1, 2), (0, 2), (1, 2)]:
        img.putpixel((hx + dx2, hy + dy2), K)
    img.putpixel((hx - 1, hy + 1), B)


def build(name, cfg):
    img = Image.open(BASE).convert("RGBA")
    erase_old_arm(img)
    draw_arm(img, cfg["hand"], cfg["elbow"], cfg["shoulder"])
    # strip stray brick pixels left over from the erased arm
    for y in range(13, 26):
        for x in range(34, 47):
            if img.getpixel((x, y)) == B:
                img.putpixel((x, y), (0, 0, 0, 0))
    img.save(os.path.join(ROOT, name + ".png"))
    print("wrote", name)


if __name__ == "__main__":
    for name, cfg in FRAMES.items():
        build(name, cfg)

    sheet = Image.new("RGBA", (61 * 3, 57), (24, 24, 34, 255))
    for i, name in enumerate(FRAMES):
        im = Image.open(os.path.join(ROOT, name + ".png")).convert("RGBA")
        sheet.paste(im, (61 * i, 0), im)
    sheet = sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST)
    sheet.save(r"D:\unity\dash\Assets\Screenshots\attack_frames.png")
    print("preview", sheet.size)
