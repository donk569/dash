#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
赛博朋克版素材管线：
1) Warped City（ansimuz, CC0）的角色帧裁到统一包围盒、背景/道具/特效拷进 Resources
2) 程序生成赛博风格地表瓦片（深蓝底 + 青色霓虹边），和包内配色统一

输出：Assets/_Project/Resources/Pixel/{Player,Enemy,Tiles,Props,Fx,Bg}
"""
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw

ROOT = r"D:\unity\dash"
SRC = os.path.join(ROOT, "_AssetInbox", "warped", "warped city files")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Pixel")


def out(*parts):
    p = os.path.join(DST, *parts)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def save(img, *parts):
    p = out(*parts) + ".png"
    img.save(p, format="PNG")
    print("wrote", p, img.size)


def load(rel):
    return Image.open(os.path.join(SRC, rel)).convert("RGBA")


def union_bbox(paths, pad=2):
    x0 = y0 = 10 ** 9
    x1 = y1 = -1
    for p in paths:
        a = np.array(load(p))[:, :, 3] > 0
        ys, xs = np.where(a)
        if len(xs) == 0:
            continue
        x0 = min(x0, xs.min())
        x1 = max(x1, xs.max())
        y0 = min(y0, ys.min())
        y1 = max(y1, ys.max())
    return (max(0, x0 - pad), max(0, y0 - pad), x1 + 1 + pad, y1 + 1 + pad)


def crop_common(paths, box, sub_index, name_fmt, folder="Player"):
    """把一组帧裁到同一个 box，保证动画对齐。"""
    for i, p in enumerate(paths):
        im = load(p).crop(box)
        save(im, folder, name_fmt % i)


# ---------------- 角色 ----------------

def player():
    groups = {
        "player_idle": ["SPRITES/player/idle/idle-%d.png" % i for i in range(1, 5)],
        "player_run": ["SPRITES/player/run/run-%d.png" % i for i in range(1, 9)],
        "player_jump": ["SPRITES/player/jump/jump-%d.png" % i for i in range(1, 5)],
        "player_runs": ["SPRITES/player/run-shoot/run-shoot-%d.png" % i for i in range(1, 9)],
        "player_hurt": ["SPRITES/player/hurt/hurt.png"],
        "player_shoot": ["SPRITES/player/shoot/shoot.png"],
        "player_crouch": ["SPRITES/player/crouch/crouch.png"],
    }
    allpaths = [p for v in groups.values() for p in v]
    box = union_bbox(allpaths)
    print("player union box", box)

    for key, paths in groups.items():
        crop_common(paths, box, 0, key + "_%d")

    # 拆姿势：jump 前两帧上升、后两帧下落
    for i, p in enumerate(groups["player_jump"]):
        im = load(p).crop(box)
        if i < 2:
            save(im, "Player", "player_rise_%d" % i)
        else:
            save(im, "Player", "player_fall_%d" % (i - 2))


# ---------------- 怪物 / 特效 ----------------

def enemies():
    for i in range(1, 5):
        save(load("SPRITES/misc/drone/drone-%d.png" % i).crop(
            load("SPRITES/misc/drone/drone-%d.png" % i).getbbox()), "Enemy", "enemy_drone_%d" % (i - 1))
    for i in range(1, 7):
        im = load("SPRITES/misc/turret/turret-%d.png" % i)
        save(im.crop(im.getbbox()), "Enemy", "enemy_turret_%d" % (i - 1))


def fx():
    for i in range(1, 7):
        im = load("SPRITES/misc/enemy-explosion/enemy-explosion-%d.png" % i)
        save(im.crop(im.getbbox()), "Fx", "fx_explosion_%d" % (i - 1))
    for i in range(1, 4):
        im = load("SPRITES/misc/shot-hit/shot-hit-%d.png" % i)
        save(im.crop(im.getbbox()), "Fx", "fx_hit_%d" % (i - 1))
    for i in range(1, 4):
        im = load("SPRITES/misc/shot/shot-%d.png" % i)
        save(im.crop(im.getbbox()), "Fx", "fx_shot_%d" % (i - 1))


def props():
    names = {
        "banner_neon": ["ENVIRONMENT/props/banner-neon/banner-neon-%d.png" % i for i in range(1, 5)],
        "banner_coke": ["ENVIRONMENT/props/banner-coke/banner-coke-%d.png" % i for i in range(1, 4)],
        "banner_sushi": ["ENVIRONMENT/props/banner-sushi/banner-sushi-%d.png" % i for i in range(1, 4)],
        "banner_big": ["ENVIRONMENT/props/banner-big/banner-big-%d.png" % i for i in range(1, 5)],
        "banner_side": ["ENVIRONMENT/props/banner-side/banner-side-%d.png" % i for i in range(1, 5)],
        "monitor_face": ["ENVIRONMENT/props/monitorface/monitor-face-%d.png" % i for i in range(1, 5)],
    }
    for key, paths in names.items():
        for i, p in enumerate(paths):
            im = load(p)
            save(im.crop(im.getbbox()), "Props", "%s_%d" % (key, i))

    arrow = load("ENVIRONMENT/props/banner-arrow.png").transpose(Image.FLIP_LEFT_RIGHT)
    save(arrow.crop(arrow.getbbox()), "Props", "prop_arrow")
    for src_name, dst_name in [("banner-open", "prop_open"), ("banner-small", "prop_sign"), ("banners", "prop_banners")]:
        im = load("ENVIRONMENT/props/%s.png" % src_name)
        save(im.crop(im.getbbox()), "Props", dst_name)

    arrow = load("ENVIRONMENT/props/banner-arrow.png").transpose(Image.FLIP_LEFT_RIGHT)
    save(arrow.crop(arrow.getbbox()), "Props", "prop_arrow")
    for src_name, dst_name in [("banner-open", "prop_open"), ("banner-small", "prop_sign"), ("banners", "prop_banners")]:
        im = load("ENVIRONMENT/props/%s.png" % src_name)
        save(im.crop(im.getbbox()), "Props", dst_name)

    for name in ["antenna", "hotel-sign", "control-box-1", "control-box-2", "control-box-3"]:
        rel = "ENVIRONMENT/props/%s.png" % name
        im = load(rel)
        save(im.crop(im.getbbox()), "Props", name.replace("-", "_"))


def backgrounds():
    save(load("demo/assets/environment/bg-1.png"), "Bg", "bg_sky")
    save(load("demo/assets/environment/bg-2.png"), "Bg", "bg_far")
    save(load("demo/assets/environment/bg-3.png"), "Bg", "bg_mid")
    save(load("ENVIRONMENT/background/near-buildings-bg.png"), "Bg", "bg_near")


# ---------------- 赛博地表瓦片（自绘，配色跟包一致） ----------------

DARK = (16, 20, 36, 255)
DARK2 = (22, 27, 48, 255)
SEAM = (30, 37, 62, 255)
MID = (44, 56, 92, 255)
RIM = (128, 244, 255, 255)
CYAN = (48, 214, 236, 255)
CYAN_DIM = (26, 120, 150, 255)


def tiles():
    # 屋顶面 16x8：顶部霓虹亮边 -> 中间过渡 -> 底部暗
    roof = Image.new("RGBA", (16, 8), DARK)
    d = ImageDraw.Draw(roof)
    d.rectangle([0, 0, 15, 0], fill=RIM)
    d.rectangle([0, 1, 15, 1], fill=MID)
    d.rectangle([0, 2, 15, 3], fill=(34, 43, 72, 255))
    d.rectangle([0, 4, 15, 7], fill=DARK)
    d.line([(0, 7), (15, 7)], fill=SEAM)
    save(roof, "Tiles", "tile_roof")

    # 楼体 16x16：暗面 + 面板缝 + 一点冷光
    wall = Image.new("RGBA", (16, 16), DARK2)
    d = ImageDraw.Draw(wall)
    d.line([(15, 0), (15, 15)], fill=SEAM)
    d.line([(0, 15), (15, 15)], fill=SEAM)
    d.line([(1, 1), (1, 14)], fill=(28, 34, 56, 255))
    d.point([(5, 5), (9, 5), (5, 10), (9, 10)], fill=(38, 46, 76, 255))
    save(wall, "Tiles", "tile_wall")

    # 霓虹灯条（贴在楼体侧面，参考图里那种青色小窗）
    neon = Image.new("RGBA", (16, 16), DARK2)
    d = ImageDraw.Draw(neon)
    d.rectangle([0, 0, 15, 15], fill=DARK2)
    d.rectangle([4, 6, 11, 9], fill=CYAN_DIM)
    d.rectangle([5, 7, 10, 8], fill=CYAN)
    d.rectangle([3, 5, 12, 10], outline=(20, 60, 80, 255))
    save(neon, "Tiles", "tile_neon")

    # 悬浮平台：上方同屋顶面，下方带喷口
    float_t = Image.new("RGBA", (16, 16), DARK)
    d = ImageDraw.Draw(float_t)
    d.rectangle([0, 0, 15, 0], fill=RIM)
    d.rectangle([0, 1, 15, 1], fill=MID)
    d.rectangle([0, 2, 15, 9], fill=(30, 38, 64, 255))
    d.rectangle([0, 10, 15, 15], fill=DARK)
    d.rectangle([6, 11, 9, 12], fill=CYAN_DIM)
    d.rectangle([7, 13, 8, 14], fill=CYAN)
    save(float_t, "Tiles", "tile_float")

    # 危险/装饰：青色导管
    pipe = Image.new("RGBA", (16, 8), DARK)
    d = ImageDraw.Draw(pipe)
    d.rectangle([0, 2, 15, 5], fill=(24, 30, 50, 255))
    d.line([(0, 3), (15, 3)], fill=CYAN_DIM)
    d.line([(0, 4), (15, 4)], fill=(14, 60, 78, 255))
    save(pipe, "Tiles", "tile_pipe")


def ui():
    keep = os.path.join(ROOT, "_AssetInbox", "ui_heart_keep.png")
    if os.path.exists(keep):
        save(Image.open(keep).convert("RGBA"), "UI", "ui_heart")


def main():
    ui()
    player()
    enemies()
    fx()
    props()
    backgrounds()
    tiles()
    print("done")


if __name__ == "__main__":
    main()
