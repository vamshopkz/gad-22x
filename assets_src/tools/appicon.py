"""GOD 2-2X app icon: the night skyline from the game with the logo. Usage: python3 appicon.py shot.png outdir"""
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
shot, out = sys.argv[1], sys.argv[2]
im = Image.open(shot).convert("RGB"); W, H = im.size; k = W / 800
bg = im.crop((int(232*k), int(60*k), int(612*k), int(440*k))).resize((1024, 1024), Image.LANCZOS)
bg = ImageEnhance.Color(ImageEnhance.Brightness(bg).enhance(1.25)).enhance(1.25)
F = "/System/Library/Fonts/Supplemental/Impact.ttf"
def logo(size, scale=1.0):
    L = Image.new("RGBA", size, (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    big = ImageFont.truetype(F, int(330*scale)); mid = ImageFont.truetype(F, int(250*scale))
    cx = size[0] // 2
    parts = [("2-2", mid, (255, 150, 20)), ("X", mid, (235, 30, 40))]
    # GOD on top, 2-2X below
    def stroke_text(xy, s, font, fill, anchor):
        d.text(xy, s, font=font, fill=fill, anchor=anchor, stroke_width=int(14*scale), stroke_fill=(10, 10, 20))
    stroke_text((cx, int(size[1]*0.52)), "GOD", big, (255, 255, 255), "ms")
    w22 = d.textlength("2-2", font=mid); wx = d.textlength("X", font=mid); gap = 14*scale
    x0 = cx - (w22 + gap + wx) / 2; y = int(size[1]*0.52 + 250*scale)
    stroke_text((x0, y), "2-2", mid, parts[0][2], "ls"); stroke_text((x0 + w22 + gap, y), "X", mid, parts[1][2], "ls")
    glow = L.filter(ImageFilter.GaussianBlur(18*scale)); g = Image.new("RGBA", size, (255, 120, 30, 0)); g.putalpha(glow.split()[3].point(lambda a: a*0.6))
    return Image.alpha_composite(g, L)
shade = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0)); sd = ImageDraw.Draw(shade)
for y in range(1024): sd.line([(0, y), (1024, y)], fill=(5, 5, 15, int(150 * max(0, (y - 380) / 644))))
full = Image.alpha_composite(bg.convert("RGBA"), shade)
full = Image.alpha_composite(full, logo((1024, 1024)))
full.convert("RGB").save(f"{out}/AppIcon.png")
bg.save(f"{out}/AppIconBg.png")
logo((1024, 1024), 0.7).save(f"{out}/AppIconFg.png")
print("ok")
