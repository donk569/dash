# -*- coding: utf-8 -*-
"""生成挥砍剑 / 金币 / 商店图标 / 探照灯光锥。"""
import math
import os
from PIL import Image

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites"


def save(img, folder, name):
    os.makedirs(os.path.join(ROOT, folder), exist_ok=True)
    p = os.path.join(ROOT, folder, name + ".png")
    img.save(p)
    print("wrote", p, img.size)


def sword():
    """竖排像素剑：剑尖朝上。16x44。"""
    W, H = 16, 44
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    def px(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c

    def rect(x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                px(x, y, c)

    edge = (222, 244, 255, 255)     # 刃口高光
    blade = (162, 196, 224, 255)    # 刃身
    blade_dark = (92, 120, 152, 255)
    gold = (255, 208, 108, 255)
    gold_dark = (176, 128, 44, 255)
    grip = (46, 34, 40, 255)
    cyan = (120, 240, 255, 255)

    # 剑尖
    px(8, 0, edge)
    px(7, 1, edge); px(8, 1, edge); px(9, 1, blade)
    # 剑身（3px 宽，左亮右暗）
    for y in range(2, 30):
        px(7, y, edge)
        px(8, y, blade)
        px(9, y, blade_dark)
    # 刃中线反光
    for y in range(4, 28, 5):
        px(7, y, cyan)

    # 护手（横向 11px）
    rect(3, 30, 12, 32, gold)
    rect(3, 32, 12, 32, gold_dark)
    px(2, 31, gold); px(13, 31, gold)
    # 护手宝石
    px(8, 31, cyan)

    # 剑柄
    rect(7, 33, 9, 40, grip)
    for y in range(34, 40, 2):
        px(8, y, gold_dark)
    # 柄头
    rect(6, 41, 10, 42, gold)
    px(8, 43, cyan)

    return img


def coin():
    """10x10 金币，带高光和内圈。"""
    W = H = 10
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    outer = (255, 206, 92, 255)
    inner = (255, 236, 160, 255)
    rim = (188, 132, 32, 255)

    cx = cy = 4.5
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - cx, y - cy)
            if d <= 4.6:
                p[x, y] = outer if d > 3.2 else inner
            elif d <= 5.0:
                p[x, y] = rim
    # 高光
    p[3, 2] = (255, 255, 240, 255)
    p[2, 3] = (255, 255, 240, 255)
    # 中间竖槽
    for y in range(3, 7):
        p[4, y] = rim
        p[5, y] = rim
    return img


def shop_icon():
    """24x24 商店招牌：霓虹边框 + 金币 + 箭头。"""
    W = H = 24
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    def rect(x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= x < W and 0 <= y < H:
                    p[x, y] = c

    cyan = (120, 240, 255, 255)
    dark = (10, 18, 32, 235)
    gold = (255, 206, 92, 255)
    gold_l = (255, 240, 180, 255)

    rect(1, 1, 22, 22, dark)
    rect(1, 1, 22, 1, cyan)
    rect(1, 22, 22, 22, cyan)
    rect(1, 1, 1, 22, cyan)
    rect(22, 1, 22, 22, cyan)

    # 金币（中间偏上）
    cx, cy, r = 11, 9, 5
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if x * x + y * y <= r * r:
                p[cx + x, cy + y] = gold if (x * x + y * y) <= (r - 1) * (r - 1) else (188, 132, 32, 255)
    p[cx - 2, cy - 2] = gold_l
    p[cx - 1, cy - 2] = gold_l

    # 下方箭头
    rect(10, 17, 13, 17, cyan)
    rect(11, 18, 12, 18, cyan)
    return img


def light_cone():
    """探照灯光锥：底宽顶窄的三角形渐变，从上方打下来。64x160。"""
    W, H = 64, 160
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    for y in range(H):
        t = y / (H - 1.0)
        half = 2 + t * 30           # 越往下越宽
        a = int(70 * (1.0 - t) ** 1.4)
        if a <= 1:
            continue
        for x in range(W):
            d = abs(x - W / 2.0)
            if d <= half:
                k = 1.0 - (d / max(1.0, half)) ** 2
                aa = int(a * k)
                if aa > 0:
                    p[x, y] = (170, 230, 255, aa)
    return img


save(sword(), "Props", "prop_sword")
save(coin(), "UI", "ui_coin")
save(shop_icon(), "Props", "prop_shop")
save(light_cone(), "Fx", "fx_lightcone")

# 预览
prev = Image.new("RGBA", (200, 180), (18, 20, 30, 255))
for i, (n, f, s) in enumerate([("prop_sword", "Props", 3), ("ui_coin", "UI", 8), ("prop_shop", "Props", 5)]):
    im = Image.open(os.path.join(ROOT, f, n + ".png")).convert("RGBA")
    prev.paste(im.resize((im.width * s, im.height * s), Image.NEAREST), (8 + i * 70, 8), im.resize((im.width * s, im.height * s), Image.NEAREST))
cone = Image.open(os.path.join(ROOT, "Fx", "fx_lightcone.png")).convert("RGBA")
prev.paste(cone, (10, 140), cone)
prev.resize((prev.width * 2, prev.height * 2), Image.NEAREST).save(r"D:\unity\dash\_AssetInbox\prev_sword.png")
print("preview ok")
