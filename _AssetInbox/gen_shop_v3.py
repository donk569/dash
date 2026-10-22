"""Shop v3: a neon storefront that reads clearly at gameplay zoom."""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = r"D:\unity\dash\Assets\_Project\Art\Sprites\Props\prop_shop.png"

K = (6, 8, 16, 255)
BODY = (26, 24, 40, 255)
BODY_L = (40, 36, 58, 255)
CY = (120, 240, 255, 255)
CY_D = (56, 132, 190, 255)
MG = (255, 70, 170, 255)
OR = (255, 150, 60, 255)
WARM = (255, 206, 130, 255)
GLASS = (255, 224, 170, 220)

W, H = 44, 56
im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(im)

# body
d.rectangle([5, 12, 38, 55], fill=BODY)
d.rectangle([5, 12, 38, 55], outline=K)
d.rectangle([6, 13, 37, 54], outline=BODY_L)

# glowing top sign (cyan) with SHOP
d.rectangle([3, 2, 40, 13], fill=(12, 16, 28, 255))
d.rectangle([3, 2, 40, 13], outline=CY)
d.rectangle([4, 3, 39, 4], fill=(190, 250, 255, 255))
d.text((15, 4), "SHOP", font=ImageFont.load_default(), fill=CY)

# striped awning
for i, x in enumerate(range(5, 38, 6)):
    d.rectangle([x, 14, min(x + 5, 37), 19], fill=OR if i % 2 == 0 else (250, 250, 250, 255))
d.line([5, 19, 37, 19], fill=K)

# warm display window
d.rectangle([8, 21, 35, 34], fill=GLASS)
d.rectangle([8, 21, 35, 34], outline=K)
for x, c in ((10, CY), (14, MG), (18, (62, 204, 138, 255)), (22, OR), (26, (250, 250, 250, 255)), (30, CY)):
    d.rectangle([x, 23, x + 2, 31], fill=c)
d.line([8, 27, 35, 27], fill=(150, 110, 70, 255))

# door
d.rectangle([17, 38, 26, 55], fill=(18, 22, 34, 255))
d.rectangle([17, 38, 26, 55], outline=CY_D)
d.line([22, 38, 22, 55], fill=CY_D)
d.point((24, 47), fill=WARM)

# side neon strips
d.line([6, 20, 6, 54], fill=MG)
d.line([37, 20, 37, 54], fill=MG)

im.save(OUT)
print("shop v3", im.size)
