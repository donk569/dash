#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从 _AssetInbox/pack16（OpenGameArt: "Platformer pack 16x16" by hdst, CC0）
切出本项目需要的单帧 PNG，输出到 Assets/_Project/Resources/Pixel/*（PPU16 / Point）。

重复执行是幂等的：会覆盖同名文件，不改动其它资源。
"""
import os
import sys
from PIL import Image

ROOT = r"D:\unity\dash"
SRC = os.path.join(ROOT, "_AssetInbox", "pack16")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Pixel")


def cell(sheet, c, r, w=16, h=16, crop=None):
    im = Image.open(os.path.join(SRC, sheet)).convert("RGBA")
    t = im.crop((c * 16, r * 16, c * 16 + w, r * 16 + h))
    if crop:
        t = t.crop(crop)
    return t


def save(img, folder, name):
    d = os.path.join(DST, folder)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, name + ".png")
    img.save(path, format="PNG", optimize=False)
    print("wrote", path, img.size)


def make_arrow():
    """16x16 右向箭头，配色沿用 pack 里 Up.png 的橙色（深橙描边 + 亮橙内芯）。"""
    from PIL import ImageDraw

    dark = (216, 67, 21, 255)
    light = (255, 152, 0, 255)

    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 深色描边层
    d.rectangle([1, 5, 9, 10], fill=dark)
    d.polygon([(8, 1), (15, 7), (15, 8), (8, 14)], fill=dark)

    # 亮色内芯
    d.rectangle([2, 6, 9, 9], fill=light)
    d.polygon([(9, 3), (14, 7), (14, 8), (9, 12)], fill=light)

    return img


def main():
    # ---------- 玩家（4x4，16px 网格）----------
    save(cell("player.png", 0, 0), "Player", "player_idle_0")
    save(cell("player.png", 1, 0), "Player", "player_idle_1")
    run = [(2, 0), (3, 0), (0, 1), (1, 1), (0, 2), (1, 2)]
    for i, (c, r) in enumerate(run):
        save(cell("player.png", c, r), "Player", "player_run_%d" % i)
    save(cell("player.png", 3, 1), "Player", "player_jump_0")
    save(cell("player.png", 2, 1), "Player", "player_fall_0")
    save(cell("player.png", 0, 3), "Player", "player_dead_0")

    # ---------- 怪物（4x3，每种 2 帧）----------
    kinds = {"enemy_bandit": (0, 0), "enemy_grunt": (2, 0),
             "enemy_ninja": (0, 1), "enemy_punk": (2, 2)}
    for name, (c, r) in kinds.items():
        save(cell("enemy.png", c, r), "Enemy", name + "_0")
        save(cell("enemy.png", c + 1, r), "Enemy", name + "_1")

    # ---------- 建筑瓦片 ----------
    # 屋顶面：蓝色砖块的顶部 8px（正好 0.5u @PPU16）
    save(cell("tiles.png", 5, 0, h=8), "Tiles", "tile_roof")
    save(cell("tiles.png", 5, 0), "Tiles", "tile_wall")
    save(cell("tiles.png", 8, 2), "Tiles", "tile_window")

    # ---------- 道具 / UI ----------
    save(Image.open(os.path.join(SRC, "objects", "Heart.png")).convert("RGBA"), "UI", "ui_heart")
    for i in range(1, 4):
        save(Image.open(os.path.join(SRC, "objects", "FlagG%d.png" % i)).convert("RGBA"), "Props", "prop_flag_%d" % (i - 1))
    for i in range(1, 3):
        save(Image.open(os.path.join(SRC, "objects", "Torch%d.png" % i)).convert("RGBA"), "Props", "prop_torch_%d" % (i - 1))

    # 疾行印记：用包里 Up.png 的同一套橙色，画一个干净的右向箭头（旋转牌子会变成歪掉的字）
    save(make_arrow(), "Props", "prop_arrow")

    # 攻击反馈：黄色激光（bullets 0,2）
    save(cell("bullets.png", 0, 2), "Props", "prop_slash")

    print("done")


if __name__ == "__main__":
    sys.exit(main())
