"""Kazakhstan-style number plates atlas (4x4 cells of 512x128) for car plates. Usage: python3 plates.py out.png"""
import sys, random
from PIL import Image, ImageDraw, ImageFont
OUT = sys.argv[1]
random.seed(22)
F = "/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf"
try: font = ImageFont.truetype(F, 96)
except OSError: font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 92)
small = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 26)
atlas = Image.new("RGB", (2048, 512), (240, 240, 236))
L = "ABCEHKMOPTXY"
specials = ["777 GOD 02", "222 GOD 02", "001 BMW 02", "100 AAA 01"]
for i in range(16):
    cx, cy = (i % 4) * 512, (i // 4) * 128
    im = Image.new("RGB", (512, 128), (246, 246, 242)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([3, 3, 508, 124], 12, outline=(20, 20, 20), width=5)
    d.rectangle([10, 10, 76, 117], fill=(0, 158, 196))           # flag strip
    d.ellipse([28, 30, 58, 60], fill=(254, 197, 0))              # sun
    for k in range(10): d.line([(43, 45), (43 + 22 * __import__('math').cos(k * 0.628), 45 + 22 * __import__('math').sin(k * 0.628))], fill=(254, 197, 0), width=2)
    d.text((43, 92), "KZ", font=small, fill=(255, 255, 255), anchor="mm")
    txt = specials[i] if i < 4 else f"{random.randint(100, 999)} {''.join(random.choice(L) for _ in range(3))} {random.choice(['01','02','17','05','13'])}"
    num, let, reg = txt.split()
    d.text((246, 70), num + " " + let, font=font, fill=(15, 15, 15), anchor="mm")
    d.line([(424, 10), (424, 117)], fill=(20, 20, 20), width=4)
    d.text((466, 70), reg, font=font, fill=(15, 15, 15), anchor="mm")
    atlas.paste(im, (cx, cy))
atlas.save(OUT, optimize=True); print("plates", OUT)
