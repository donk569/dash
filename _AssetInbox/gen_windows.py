"""Windows with people in them, for the building facades under each rooftop.

- prop_window         : 18x16 frame, transparent glass so whatever is behind shows through
- npc_window_*_0..3   : head + shoulders cropped from the Biker / Cyborg idle frames
"""
import os
from PIL import Image, ImageDraw

ROOT = r"D:\unity\dash\Assets\_Project\Art\Sprites"
SRC = r"D:\unity\dash\_AssetInbox\cyberpunk"

K = (5, 9, 18, 255)
CY = (120, 240, 255, 255)
CY_D = (58, 140, 205, 255)
GRAY = (70, 82, 104, 255)
GRAY_D = (42, 50, 66, 255)
WARM = (255, 196, 110, 255)


def window():
    W, H = 18, 16
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # outer dark frame
    d.rectangle([0, 0, W - 1, H - 1], outline=K)
    # frame body
    d.rectangle([1, 1, W - 2, H - 2], fill=GRAY_D)
    # glass (semi transparent so the NPC behind is dimmed)
    d.rectangle([3, 3, W - 4, H - 4], fill=(12, 16, 28, 165))
    # mullions
    d.line([(W // 2, 3), (W // 2, H - 4)], fill=GRAY)
    d.line([(3, H // 2 + 1), (W - 4, H // 2 + 1)], fill=GRAY)
    # sill
    d.rectangle([0, H - 3, W - 1, H - 1], fill=GRAY)
    d.line([0, H - 3, W - 1, H - 3], fill=CY_D)
    im.save(os.path.join(ROOT, "Props", "prop_window.png"))
    print("wrote prop_window", im.size)


def npc_heads():
    """Crop head+shoulders out of the idle strips so they read as 'someone in the window'."""
    jobs = [("1 Biker", "Biker_idle", "npc_window_biker"),
            ("3 Cyborg", "Cyborg_idle", "npc_window_cyborg")]
    out = os.path.join(ROOT, "Npc")
    for folder, strip, prefix in jobs:
        im = Image.open(os.path.join(SRC, folder, strip + ".png")).convert("RGBA")
        for i in range(4):
            frame = im.crop((i * 48, 0, (i + 1) * 48, 48))
            # head + shoulders: roughly x 12..36, y 2..22
            head = frame.crop((14, 2, 34, 22)).resize((16, 16), Image.NEAREST)
            head.save(os.path.join(out, "%s_%d.png" % (prefix, i)))
        print("wrote", prefix, 4)

    # preview: window + each head
    win = Image.open(os.path.join(ROOT, "Props", "prop_window.png"))
    sheet = Image.new("RGBA", (18 * 9, 16), (30, 32, 44, 255))
    for i in range(4):
        sheet.alpha_composite(win, (i * 20, 0))
        head = Image.open(os.path.join(out, "npc_window_biker_%d.png" % i))
        sheet.alpha_composite(head, (i * 20 + 1, 2))
        sheet.alpha_composite(head, (i * 20 + 1, 2))
    img = sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST)
    img.save(r"D:\unity\dash\Assets\Screenshots\window_preview.png")
    print("preview ok")


if __name__ == "__main__":
    window()
    npc_heads()
