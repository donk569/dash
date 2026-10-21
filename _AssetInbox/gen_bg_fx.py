# -*- coding: utf-8 -*-
"""生成背景动画与特效素材：月亮光晕 / 飞行卡车 / 落地尘土 / 流星头。

输出到 Assets/_Project/Art/Sprites/Fx/ 与 Props/
"""
import os
import math
from PIL import Image

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites"
os.makedirs(os.path.join(ROOT, "Fx"), exist_ok=True)
os.makedirs(os.path.join(ROOT, "Props"), exist_ok=True)


def save(img, folder, name):
    p = os.path.join(ROOT, folder, name + ".png")
    img.save(p)
    print("wrote", p, img.size)


# ---------------- 月亮光晕（径向渐变，用于忽明忽暗） ----------------
def moon_glow(size=128):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    cx = cy = size / 2.0
    r = size / 2.0
    px = img.load()
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - cx + 0.5, y - cy + 0.5) / r
            if d > 1.0:
                continue
            # 内核偏白，外圈偏青蓝，平方衰减
            a = int(150 * (1.0 - d) ** 2.2)
            if a <= 0:
                continue
            t = d
            cr = int(232 - 40 * t)
            cg = int(238 - 20 * t)
            cb = int(255)
            px[x, y] = (cr, cg, cb, a)
    return img


# ---------------- 飞行卡车（侧视，斜向驶过用） ----------------
def flying_truck():
    W, H = 44, 20
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    def rect(x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= x < W and 0 <= y < H:
                    p[x, y] = c

    body_dark = (26, 30, 48, 255)
    body_mid = (44, 52, 78, 255)
    body_lit = (62, 74, 104, 255)
    neon = (120, 236, 255, 255)
    neon_soft = (52, 150, 190, 255)
    window = (255, 214, 128, 255)
    flame = (255, 140, 80, 255)

    # 车厢（右半，偏胖）
    rect(14, 6, 38, 15, body_mid)
    rect(14, 6, 38, 6, body_lit)
    rect(14, 15, 38, 15, body_dark)
    # 车头（左侧）
    rect(4, 8, 14, 15, body_mid)
    rect(4, 8, 14, 8, body_lit)
    rect(4, 15, 14, 15, body_dark)
    # 挡风玻璃
    rect(5, 9, 9, 12, window)
    # 侧窗三扇
    for i in range(3):
        rect(17 + i * 7, 9, 21 + i * 7, 11, window)
    # 车底霓虹条
    rect(6, 16, 37, 16, neon_soft)
    # 车头灯
    rect(3, 11, 4, 12, neon)
    # 车顶霓虹
    rect(16, 5, 36, 5, neon)
    # 尾部推进器火焰
    rect(39, 9, 42, 12, flame)
    rect(43, 10, 43, 11, (255, 232, 160, 255))
    # 轮子（悬空，做成推进舱）
    rect(12, 17, 17, 18, body_dark)
    rect(30, 17, 35, 18, body_dark)

    return img


# ---------------- 落地尘土（3 帧小方块） ----------------
def dust():
    frames = []
    for i in range(3):
        W = 10
        img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
        p = img.load()
        spread = 2 + i * 2
        for k in range(-spread, spread + 1):
            x = 5 + k
            if 0 <= x < W:
                a = max(0, 200 - i * 60 - abs(k) * 20)
                y = 7 - int(abs(k) * 0.3) - i
                for h in range(2):
                    if 0 <= y + h < W:
                        p[x, y + h] = (168, 186, 206, a)
        frames.append(img)
    return frames


# ---------------- 流星头（亮核 + 拖尾渐变） ----------------
def meteor_head():
    W, H = 8, 3
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()
    for x in range(W):
        t = x / (W - 1.0)
        a = int(255 * (0.35 + 0.65 * t))
        p[x, 1] = (255, 255, 245, a)
        if 1 <= x <= 2:
            p[x, 0] = (200, 240, 255, a // 2)
            p[x, 2] = (200, 240, 255, a // 2)
    return img


save(moon_glow(), "Props", "prop_moon_glow")
save(flying_truck(), "Props", "prop_truck")
save(meteor_head(), "Fx", "fx_meteor")

for i, f in enumerate(dust()):
    save(f, "Fx", "fx_dust_%d" % i)

print("done")
