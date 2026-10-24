"""
Room atlas for the window interior-mapping shader. 4 room kinds side by side (living room, bedroom,
office, shop/cafe); each column = back wall (top 512), side wall (256), floor (128), ceiling (128).
Usage: python3 rooms.py <polyhaven dir> <out png>
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PH, OUT = sys.argv[1], sys.argv[2]
W, H = 512, 1024
rng = np.random.default_rng(5)

def tex(name, size, kind="diff"):
    im = Image.open(f"{PH}/tex/{name}_{kind}.jpg").convert("RGB")
    return im.resize(size, Image.LANCZOS)

def tint(im, c):
    a = np.asarray(im, np.float32) / 255 * np.array(c)[None, None, :]
    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))

def shade_box(d, box, col, dark=0.75):
    x0, y0, x1, y1 = box
    for i in range(int(y1 - y0)):
        k = 1.0 - (i / max(1, y1 - y0)) * (1 - dark)
        d.line([(x0, y0 + i), (x1, y0 + i)], fill=tuple(int(v * k) for v in col))

def room(kind):
    col = Image.new("RGB", (W, H))
    wall_c = {0: (0.95, 0.9, 0.82), 1: (0.85, 0.88, 0.95), 2: (0.92, 0.93, 0.95), 3: (0.95, 0.92, 0.88)}[kind]
    wall = tint(tex("plastered_wall", (W, W)), wall_c)
    back = wall.copy(); d = ImageDraw.Draw(back)
    floor_name = {0: "laminate_floor_02", 1: "wood_floor", 2: "interior_tiles", 3: "terrazzo_tiles"}[kind]
    # skirting + floor line
    d.rectangle([0, 470, W, 512], fill=(70, 60, 52))
    if kind == 0:     # living room: sofa, picture, lamp, plant, shelf
        d.rectangle([150, 120, 360, 250], fill=(40, 40, 45)); d.rectangle([160, 130, 350, 240], fill=(70, 110, 160))
        d.rectangle([160, 200, 350, 240], fill=(160, 150, 110))
        sofa = (int(rng.integers(60, 140)), int(rng.integers(70, 110)), int(rng.integers(90, 150)))
        shade_box(d, (90, 330, 420, 470), sofa); shade_box(d, (90, 290, 420, 350), tuple(int(c * 0.9) for c in sofa))
        shade_box(d, (70, 300, 110, 470), tuple(int(c * 0.8) for c in sofa)); shade_box(d, (400, 300, 440, 470), tuple(int(c * 0.8) for c in sofa))
        d.ellipse([200, 320, 250, 360], fill=(200, 180, 120)); d.ellipse([280, 320, 330, 360], fill=(180, 80, 80))
        d.line([(470, 470), (470, 200)], fill=(30, 30, 30), width=5); d.polygon([(440, 210), (500, 210), (485, 160), (455, 160)], fill=(250, 235, 190))
        d.rectangle([20, 380, 60, 470], fill=(150, 90, 60)); d.ellipse([0, 280, 80, 400], fill=(40, 110, 50))
    elif kind == 1:   # bedroom: bed, nightstands, curtains, art
        shade_box(d, (100, 260, 410, 380), (110, 80, 60)); d.rectangle([110, 360, 400, 470], fill=(230, 230, 235))
        d.rectangle([110, 380, 400, 470], fill=(int(rng.integers(80, 200)), 110, 150))
        d.rectangle([140, 330, 230, 365], fill=(245, 245, 245)); d.rectangle([280, 330, 370, 365], fill=(245, 245, 245))
        for x in (40, 430): shade_box(d, (x, 390, x + 50, 470), (120, 90, 70)); d.ellipse([x + 5, 340, x + 45, 385], fill=(250, 230, 180))
        d.rectangle([180, 120, 330, 220], fill=(30, 30, 30)); d.rectangle([186, 126, 324, 214], fill=(190, 120, 90))
    elif kind == 2:   # office: desks, monitors, whiteboard, plant
        d.rectangle([60, 90, 300, 220], fill=(245, 245, 245)); d.rectangle([60, 90, 300, 96], fill=(120, 120, 120))
        for x in (40, 200, 360):
            shade_box(d, (x, 360, x + 140, 380), (210, 210, 210)); d.rectangle([x + 10, 380, x + 16, 470], fill=(80, 80, 80)); d.rectangle([x + 124, 380, x + 130, 470], fill=(80, 80, 80))
            d.rectangle([x + 40, 300, x + 100, 350], fill=(20, 20, 25)); d.rectangle([x + 44, 304, x + 96, 344], fill=(60, 120, 200))
            shade_box(d, (x + 50, 400, x + 90, 460), (40, 40, 45))
        d.ellipse([440, 260, 510, 380], fill=(40, 120, 60)); d.rectangle([455, 380, 495, 470], fill=(200, 200, 200))
    else:             # shop/cafe: shelves with goods, counter
        for row in range(4):
            y = 140 + row * 80
            d.rectangle([20, y + 60, 492, y + 66], fill=(110, 80, 55))
            for x in range(24, 488, 22):
                c = tuple(int(v) for v in rng.integers(60, 255, 3))
                hgt = int(rng.integers(30, 58)); d.rectangle([x, y + 60 - hgt, x + 18, y + 60], fill=c)
        shade_box(d, (60, 400, 452, 470), (140, 100, 70))
    back = back.filter(ImageFilter.GaussianBlur(0.6))
    col.paste(back, (0, 0))
    side = tint(tex("plastered_wall", (W, 256)), tuple(v * 0.9 for v in wall_c)); ds = ImageDraw.Draw(side)
    ds.rectangle([0, 236, W, 256], fill=(70, 60, 52))
    if kind in (0, 1): ds.rectangle([180, 40, 300, 236], fill=(140, 100, 70))     # a door
    if kind == 2: ds.rectangle([60, 60, 220, 160], fill=(230, 230, 230))
    col.paste(side, (0, 512))
    col.paste(tex(floor_name, (W, 128)), (0, 768))
    ceil = Image.new("RGB", (W, 128), (235, 235, 232)); dc = ImageDraw.Draw(ceil)
    if kind == 2:
        for x in range(40, W, 140): dc.rectangle([x, 30, x + 90, 90], fill=(255, 252, 240))
    else: dc.ellipse([W // 2 - 40, 34, W // 2 + 40, 94], fill=(255, 245, 220))
    col.paste(ceil, (0, 896))
    return col

atlas = Image.new("RGB", (W * 4, H))
for k in range(4): atlas.paste(room(k), (k * W, 0))
atlas.save(OUT, optimize=True)
print("rooms", OUT)
