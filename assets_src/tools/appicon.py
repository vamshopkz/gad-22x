"""Herbal app icon: the night skyline from the game with the logo. Usage: python3 appicon.py shot.png outdir"""
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
shot, out = sys.argv[1], sys.argv[2]
im = Image.open(shot).convert("RGB"); W, H = im.size; k = W / 800
bg = im.crop((int(232*k), int(60*k), int(612*k), int(440*k))).resize((1024, 1024), Image.LANCZOS)
bg = ImageEnhance.Color(ImageEnhance.Brightness(bg).enhance(1.25)).enhance(1.25)
F = "/System/Library/Fonts/Supplemental/Impact.ttf"
def logo(size, scale=1.0):
    """HERBAL in white with a green outline glow and a leaf over the A."""
    L = Image.new("RGBA", size, (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    font = ImageFont.truetype(F, int(250*scale))
    cx, y = size[0] // 2, int(size[1]*0.6)
    d.text((cx, y), "HERBAL", font=font, fill=(255, 255, 255), anchor="ms", stroke_width=int(14*scale), stroke_fill=(8, 40, 14))
    w = d.textlength("HERBAL", font=font)
    # a leaf above the text: two arcs meeting at the tips, with a vein
    lx, ly, lw, lh = cx + w*0.18, y - 300*scale, 150*scale, 90*scale
    import math
    pts = [(lx + lw*math.cos(a)*0.5*(1 if i < 20 else 1), ly - lh*math.sin(a)*0.5*(1 if i < 20 else -1)) for i, a in enumerate([k*math.pi/19 for k in range(20)] + [k*math.pi/19 for k in range(19, -1, -1)])]
    pts = [(lx - lw/2 + lw*k/19, ly - lh/2*math.sin(k*math.pi/19)) for k in range(20)] + [(lx + lw/2 - lw*k/19, ly + lh/2*math.sin(k*math.pi/19)) for k in range(20)]
    leaf = Image.new("RGBA", size, (0, 0, 0, 0)); ld = ImageDraw.Draw(leaf)
    ld.polygon(pts, fill=(70, 220, 90), outline=(8, 40, 14))
    ld.line([(lx - lw/2, ly), (lx + lw/2, ly)], fill=(20, 110, 40), width=int(8*scale))
    leaf = leaf.rotate(30, center=(lx, ly), resample=Image.BICUBIC)
    L = Image.alpha_composite(L, leaf)
    glow = L.filter(ImageFilter.GaussianBlur(20*scale)); g = Image.new("RGBA", size, (60, 255, 110, 0)); g.putalpha(glow.split()[3].point(lambda a: a*0.7))
    return Image.alpha_composite(g, L)
shade = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0)); sd = ImageDraw.Draw(shade)
for y in range(1024): sd.line([(0, y), (1024, y)], fill=(5, 5, 15, int(150 * max(0, (y - 380) / 644))))
full = Image.alpha_composite(bg.convert("RGBA"), shade)
full = Image.alpha_composite(full, logo((1024, 1024)))
full.convert("RGB").save(f"{out}/AppIcon.png")
bg.save(f"{out}/AppIconBg.png")
logo((1024, 1024), 0.7).save(f"{out}/AppIconFg.png")
print("ok")
