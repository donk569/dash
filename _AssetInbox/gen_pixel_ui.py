# -*- coding: utf-8 -*-
"""生成像素风 UI 贴图（面板九宫格 / 进度条 / 数字位图字体 / 扫描线）。

全部按 720 虚拟高度设计，2px 为一个视觉单位，纯色块 + 硬边，不用任何渐变/模糊。
输出到 Assets/_Project/Art/Sprites/UI/
"""
import os
from PIL import Image

OUT = r"D:\unity\dash\Assets\_Project\Art\Sprites\UI"
os.makedirs(OUT, exist_ok=True)

# ---- 调色板（和场景的赛博夜景同族）----
CY_BRIGHT = (143, 248, 255, 255)   # 高亮青（边框）
CY_MID = (42, 107, 130, 255)       # 中青（内圈）
CY_DIM = (26, 58, 74, 255)         # 暗青
BG_PANEL = (11, 18, 32, 255)       # 面板底
BG_DEEP = (6, 10, 18, 255)         # 更深的底
YELLOW = (255, 209, 102, 255)
PURPLE = (186, 140, 255, 255)
RED = (255, 77, 109, 255)
WHITE = (255, 255, 255, 255)


def new(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w, h), color)


def px(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def rect(img, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(img, x, y, c)


def panel(border_color, bg=BG_PANEL, accent=None):
    """24x24 九宫格面板：2px 外框 + 1px 内线 + 切角 + 角落像素点。"""
    img = new(24, 24, (0, 0, 0, 0))

    # 内部填充
    rect(img, 1, 1, 22, 22, bg)

    # 2px 外框（亮 + 暗各 1px，形成立体感）
    rect(img, 0, 0, 23, 0, border_color)
    rect(img, 0, 23, 23, 23, border_color)
    rect(img, 0, 0, 0, 23, border_color)
    rect(img, 23, 0, 23, 23, border_color)
    rect(img, 1, 1, 22, 1, CY_MID)
    rect(img, 1, 22, 22, 22, CY_MID)
    rect(img, 1, 1, 1, 22, CY_MID)
    rect(img, 22, 1, 22, 22, CY_MID)

    # 内 1px 暗线，跟填充分层
    rect(img, 2, 2, 21, 2, BG_DEEP)
    rect(img, 2, 21, 21, 21, BG_DEEP)
    rect(img, 2, 2, 2, 21, BG_DEEP)
    rect(img, 21, 2, 21, 21, BG_DEEP)

    # 四角切 2px（像素风常见做法，避免圆角那种"现代感"）
    for cx, cy in [(0, 0), (23, 0), (0, 23), (23, 23)]:
        px(img, cx, cy, (0, 0, 0, 0))

    # 角落装饰点（明亮点，四个 L 形）
    if accent is None:
        accent = border_color
    for (ax, ay) in [(3, 3), (20, 3), (3, 20), (20, 20)]:
        px(img, ax, ay, accent)

    return img


def bar_base():
    """16x8 进度条底：1px 外框 + 深底。"""
    img = new(16, 8, (0, 0, 0, 0))
    rect(img, 0, 0, 15, 7, CY_MID)
    rect(img, 1, 1, 14, 6, BG_DEEP)
    return img


def bar_fill():
    """16x8 进度条填充：顶部 1px 高光，底部 1px 暗边。"""
    img = new(16, 8, (0, 0, 0, 0))
    rect(img, 0, 0, 15, 7, (54, 214, 234, 255))
    rect(img, 0, 0, 15, 0, (168, 248, 255, 255))
    rect(img, 0, 7, 15, 7, (20, 120, 150, 255))
    return img


# ---- 3x5 像素数字字体，2 倍放大到 6x10 ----
GLYPHS = {
    "0": ["111", "101", "101", "101", "111"],
    "1": ["010", "110", "010", "010", "111"],
    "2": ["111", "001", "111", "100", "111"],
    "3": ["111", "001", "111", "001", "111"],
    "4": ["101", "101", "111", "001", "001"],
    "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"],
    "7": ["111", "001", "001", "001", "001"],
    "8": ["111", "101", "111", "101", "111"],
    "9": ["111", "101", "111", "001", "111"],
    "/": ["001", "001", "010", "100", "100"],
    ".": ["000", "000", "000", "000", "010"],
    "s": ["111", "100", "111", "001", "111"],
    "m": ["101", "111", "111", "101", "101"],
    "x": ["101", "101", "010", "101", "101"],
}
ORDER = list(GLYPHS.keys())


def digit_sheet():
    cw, ch, pad = 6, 10, 1
    cols = len(ORDER)
    img = new(cols * (cw + pad) + pad, ch + pad * 2)
    for i, k in enumerate(ORDER):
        ox = pad + i * (cw + pad)
        oy = pad
        g = GLYPHS[k]
        for ry, row in enumerate(g):
            for rx, bit in enumerate(row):
                if bit == "1":
                    for sy in range(2):
                        for sx in range(2):
                            px(img, ox + rx * 2 + sx, oy + ry * 2 + sy, WHITE)
    return img, cw, ch, pad, cols


def scanline():
    """2x2 斜纹：隔行压暗，用作全屏遮罩。"""
    img = new(2, 2, (0, 0, 0, 0))
    px(img, 0, 0, (0, 0, 0, 110))
    px(img, 1, 1, (0, 0, 0, 110))
    return img


def save(img, name):
    p = os.path.join(OUT, name + ".png")
    img.save(p)
    print("wrote", p, img.size)


save(panel(CY_BRIGHT), "ui_panel")
save(panel(YELLOW), "ui_panel_hl")
save(bar_base(), "ui_bar_bg")
save(bar_fill(), "ui_bar_fill")

d, cw, ch, pad, cols = digit_sheet()
save(d, "ui_digits")
print("DIGITS=%d,%d,%d,%d,%d" % (cw, ch, pad, cols, d.width))

save(scanline(), "ui_scanline")

# 预览：拼一张效果图
preview = new(320, 150, (18, 20, 30, 255))
p0 = panel(CY_BRIGHT)
p1 = panel(YELLOW)
preview.paste(p0, (8, 8), p0)
preview.paste(p1, (40, 8), p1)
b0 = bar_base()
preview.paste(b0, (8, 44), b0)
preview.paste(bar_fill(), (8, 44), bar_fill())
preview.paste(d, (8, 60), d)
sc = scanline()
for y in range(80, 150, 2):
    for x in range(0, 320, 2):
        preview.paste(sc, (x, y), sc)
preview = preview.resize((preview.width * 4, preview.height * 4), Image.NEAREST)
preview.save(r"D:\unity\dash\_AssetInbox\ui_preview.png")
print("preview", preview.size)
