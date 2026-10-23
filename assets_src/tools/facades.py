"""
Compose realistic facade texture sets (albedo, normal, metal/smoothness mask, night emission)
from CC0 Poly Haven materials. One texture = 4 window bays x 4 floors (14 m x 13.2 m; storefront rows are 4.4 m).
Usage: python3 facades.py <polyhaven dir> <unity Resources/Facades dir>
"""
import os, sys, random
import numpy as np
from PIL import Image, ImageFilter

PH, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
S = 2048                       # working resolution
BAYS, ROWS = 4, 4

def load(name, kind="diff"):
    p = f"{PH}/tex/{name}_{kind}.jpg"
    return np.asarray(Image.open(p).convert("RGB"), dtype=np.float32) / 255.0

def tile(tex, px_per_tile, w=S, h=S):
    """Repeat a texture so that one copy covers px_per_tile pixels."""
    im = Image.fromarray((tex * 255).astype(np.uint8)).resize((px_per_tile, px_per_tile), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    ry, rx = h // px_per_tile + 2, w // px_per_tile + 2
    return np.tile(a, (ry, rx, 1))[:h, :w]

def gauss(img, r):
    im = Image.fromarray(np.clip(img * 255, 0, 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0

def save(arr, name, size, linear=False):
    im = Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8))
    if size != arr.shape[0]: im = im.resize((size, size), Image.LANCZOS)
    im.save(f"{OUT}/{name}.png", optimize=True)

def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    n = np.dstack([-gx * strength, -gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    # Unity reads +Y up in texture space; image row 0 is the top, so flip the y gradient.
    n[..., 1] *= -1
    return n * 0.5 + 0.5

def blend_normals(base, detail):
    b = base * 2 - 1; d = detail * 2 - 1
    r = np.dstack([b[..., 0] + d[..., 0], b[..., 1] + d[..., 1], b[..., 2] * d[..., 2]])
    r /= np.linalg.norm(r, axis=2, keepdims=True)
    return r * 0.5 + 0.5

def rect(mask, x0, y0, x1, y1, v=1.0):
    mask[int(y0):int(y1), int(x0):int(x1)] = v

def sky_reflection(h, w, tint, rng):
    """Glass: reflected sky gradient (bright top), a soft horizon line and faint clouds."""
    y = np.linspace(0, 1, h)[:, None]
    base = np.array(tint)[None, None, :] * (0.55 + 0.6 * (1 - y))[..., None]
    n = rng.random((max(2, h // 16), max(2, w // 16)))
    n = np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0
    return np.clip(base + (n[..., None] - 0.5) * 0.04, 0, 1)

def interior(h, w, rng, lit, kind):
    """What you see through a window: a room with curtains / blinds or an office ceiling."""
    room = np.zeros((h, w, 3), np.float32)
    wall = np.array(rng.choice([(0.55, 0.5, 0.45), (0.45, 0.47, 0.5), (0.6, 0.55, 0.48), (0.35, 0.33, 0.32)]))
    room[:] = wall * (0.25 if not lit else 0.9)
    if kind == "office":
        for yy in range(int(h * 0.08), int(h * 0.2), 6): room[yy:yy + 2, :] = (1, 0.98, 0.9) if lit else (0.3, 0.3, 0.3)
        room[int(h * 0.65):, :] *= 0.6
        for _ in range(3):
            x = rng.integers(0, max(1, w - 20)); room[int(h * 0.55):int(h * 0.7), x:x + rng.integers(8, 20)] *= 0.4
    else:
        cur = np.array(rng.choice([(0.85, 0.82, 0.75), (0.7, 0.2, 0.2), (0.3, 0.45, 0.7), (0.9, 0.9, 0.9), (0.55, 0.45, 0.3)]))
        side = int(w * rng.uniform(0.15, 0.3))
        room[:, :side] = cur * (0.95 if lit else 0.35); room[:, w - side:] = cur * (0.95 if lit else 0.35)
        if rng.random() < 0.3:  # blinds
            for yy in range(0, h, 5): room[yy:yy + 2, :] = room[yy:yy + 2, :] * 0.6 + 0.3
    if lit:
        y = np.linspace(0, 1, h)[:, None, None]
        room *= (1.1 - 0.5 * y)
    return np.clip(room, 0, 1)


def facade(name, wall_tex, wall_m, kind, win, frame, glass_tint, seed, rows_h=None, size=1024, wall_tint=(1, 1, 1),
           accent_tex=None, accent_m=2.0, lit_rate=0.45, roughness_wall=0.85, sill=True):
    rng = np.random.default_rng(seed)
    bw, bh = S // BAYS, S // ROWS
    ppm = bw / 3.5                                  # pixels per metre (one bay = 3.5 m)
    alb = tile(load(wall_tex), int(ppm * wall_m)) * np.array(wall_tint)[None, None, :]
    nrm = tile(load(wall_tex, "nor"), int(ppm * wall_m))
    rough = tile(load(wall_tex, "rough"), int(ppm * wall_m))[..., 0] * roughness_wall
    height = np.full((S, S), 0.5, np.float32)
    metal = np.zeros((S, S), np.float32)
    smooth = 1 - rough
    emit = np.zeros((S, S, 3), np.float32)
    glassmask = np.zeros((S, S), np.float32)
    if accent_tex:  # second material in vertical stripes (villa wood, panels)
        acc = tile(load(accent_tex), int(ppm * accent_m)); an = tile(load(accent_tex, "nor"), int(ppm * accent_m))
        for b in range(BAYS):
            if b % 2 == 1:
                alb[:, b * bw:(b + 1) * bw] = acc[:, b * bw:(b + 1) * bw]
                nrm[:, b * bw:(b + 1) * bw] = an[:, b * bw:(b + 1) * bw]
    fr = np.array(frame)
    for r in range(ROWS):
        for b in range(BAYS):
            x0, y0 = b * bw, r * bh
            lit = rng.random() < lit_rate
            # window rectangle in this bay (fractions of the bay)
            wx0, wy0, wx1, wy1 = win
            X0, Y0, X1, Y1 = x0 + wx0 * bw, y0 + wy0 * bh, x0 + wx1 * bw, y0 + wy1 * bh
            ww, wh = int(X1 - X0), int(Y1 - Y0)
            ft = max(4, int(ppm * 0.07))            # frame thickness 7 cm
            # recess shadow around the opening
            rect(height, X0 - ft, Y0 - ft, X1 + ft, Y1 + ft, 0.35)
            g = sky_reflection(wh, ww, glass_tint, rng) * 0.65 + interior(wh, ww, rng, lit, "office" if kind == "glass" else "home") * 0.45
            alb[int(Y0):int(Y0) + wh, int(X0):int(X0) + ww] = g
            if lit: emit[int(Y0):int(Y0) + wh, int(X0):int(X0) + ww] = interior(wh, ww, rng, True, "office" if kind == "glass" else "home") * 0.9
            glassmask[int(Y0):int(Y0) + wh, int(X0):int(X0) + ww] = 1
            height[int(Y0):int(Y0) + wh, int(X0):int(X0) + ww] = 0.15
            # frames: outer + mullions/transom
            def frm(a, b_, c, d):
                alb[int(b_):int(d), int(a):int(c)] = fr; height[int(b_):int(d), int(a):int(c)] = 0.42
                glassmask[int(b_):int(d), int(a):int(c)] = 0; metal[int(b_):int(d), int(a):int(c)] = 0.6; smooth[int(b_):int(d), int(a):int(c)] = 0.55
                emit[int(b_):int(d), int(a):int(c)] = 0
            frm(X0, Y0, X1, Y0 + ft); frm(X0, Y1 - ft, X1, Y1); frm(X0, Y0, X0 + ft, Y1); frm(X1 - ft, Y0, X1, Y1)
            if kind in ("home", "brick", "stone"):
                frm((X0 + X1) / 2 - ft / 2, Y0, (X0 + X1) / 2 + ft / 2, Y1)
                frm(X0, Y0 + wh * 0.3, X1, Y0 + wh * 0.3 + ft * 0.8)
            elif kind in ("glass", "villa", "store", "ribbon"):
                frm((X0 + X1) / 2 - ft / 3, Y0, (X0 + X1) / 2 + ft / 3, Y1)
            if sill and kind not in ("glass", "store"):
                sl = int(ppm * 0.08)
                alb[int(Y1):int(Y1) + sl, int(X0 - ft * 2):int(X1 + ft * 2)] = np.array((0.78, 0.77, 0.74))
                height[int(Y1):int(Y1) + sl, int(X0 - ft * 2):int(X1 + ft * 2)] = 0.75
                alb[int(Y1) + sl:int(Y1) + sl * 3, int(X0 - ft * 2):int(X1 + ft * 2)] *= 0.72   # shadow under the sill
            if kind == "brick":  # concrete lintel
                ln = int(ppm * 0.18)
                alb[int(Y0 - ft - ln):int(Y0 - ft), int(X0 - ft * 2):int(X1 + ft * 2)] = np.array((0.7, 0.68, 0.64))
                height[int(Y0 - ft - ln):int(Y0 - ft), int(X0 - ft * 2):int(X1 + ft * 2)] = 0.6
        if kind == "glass":  # floor slab band (spandrel)
            sb = int(bh * 0.1)
            alb[r * bh:r * bh + sb, :] = np.array(glass_tint) * 0.35
            height[r * bh:r * bh + sb, :] = 0.3
            metal[r * bh:r * bh + sb, :] = 0.3; smooth[r * bh:r * bh + sb, :] = 0.8
        if kind == "store":  # sign band on top of each row
            sb = int(bh * 0.16)
            alb[r * bh:r * bh + sb, :] = np.array((0.2, 0.21, 0.23))
            height[r * bh:r * bh + sb, :] = 0.55; smooth[r * bh:r * bh + sb, :] = 0.5
    # glass: smooth and a bit metallic (reflective)
    smooth = np.where(glassmask > 0.5, 0.94, smooth)
    metal = np.where(glassmask > 0.5, 0.25 if kind == "glass" else 0.12, metal)
    # ambient occlusion from height (darken recesses)
    ao = 1 - np.clip(gauss(np.dstack([0.5 - height] * 3), 6)[..., 0] * 0.8, 0, 0.5)
    alb = alb * ao[..., None]
    hn = normal_from_height(gauss(np.dstack([height] * 3), 1.2)[..., 0], 14.0)
    nrm = blend_normals(hn, nrm)
    mask = np.dstack([metal, np.zeros_like(metal), np.zeros_like(metal), smooth])
    big = name in ("f1", "f2", "f4", "f6")
    save(alb, name + "_a", 2048 if big else size)
    save(nrm, name + "_n", size, True)
    Image.fromarray(np.clip(mask * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS).save(f"{OUT}/{name}_m.png")
    save(emit, name + "_e", size // 2)
    print("facade", name)

# 0 glass curtain-wall tower, 7 dark/bronze glass office
facade("f0", "metal_plate", 1.5, "glass", (0.02, 0.1, 0.98, 0.99), (0.75, 0.77, 0.8), (0.35, 0.55, 0.65), 1, lit_rate=0.5)
facade("f7", "metal_plate", 1.5, "glass", (0.02, 0.1, 0.98, 0.99), (0.15, 0.15, 0.16), (0.32, 0.28, 0.22), 7, lit_rate=0.5)
# 1 plastered residential, 2 brick residential, 9 grey plaster flats
facade("f1", "beige_wall_001", 2.5, "home", (0.27, 0.3, 0.73, 0.78), (0.95, 0.95, 0.94), (0.45, 0.55, 0.62), 2, wall_tint=(1.05, 1.02, 0.98))
facade("f2", "red_brick", 1.6, "brick", (0.27, 0.32, 0.73, 0.78), (0.94, 0.94, 0.92), (0.42, 0.52, 0.6), 3)
facade("f9", "grey_plaster", 2.5, "home", (0.22, 0.28, 0.78, 0.8), (0.2, 0.2, 0.22), (0.45, 0.55, 0.62), 9)
# 3 modern white panels with ribbon windows
facade("f3", "rectangular_facade_tiles", 3.0, "ribbon", (0.05, 0.3, 0.95, 0.8), (0.18, 0.19, 0.2), (0.4, 0.5, 0.58), 4, wall_tint=(1.08, 1.08, 1.08))
# 4 shopfronts (row = 4.4 m storey): big glass, lit shop inside
facade("f4", "granite_tile", 2.0, "store", (0.04, 0.24, 0.96, 0.98), (0.12, 0.12, 0.13), (0.5, 0.6, 0.65), 5, lit_rate=0.85, sill=False, wall_tint=(0.95, 0.95, 0.95))
# 5 industrial corrugated metal, high windows
facade("f5", "corrugated_iron", 2.0, "ribbon", (0.08, 0.12, 0.92, 0.35), (0.3, 0.32, 0.33), (0.5, 0.58, 0.62), 6, lit_rate=0.25, sill=False)
# 6 modern villa: white plaster + oak cladding, floor-to-ceiling glass
facade("f6", "white_plaster_02", 2.5, "villa", (0.08, 0.08, 0.92, 0.97), (0.08, 0.08, 0.09), (0.5, 0.62, 0.7), 8, accent_tex="oak_veneer_01", accent_m=1.6, lit_rate=0.6, sill=False, wall_tint=(1.08, 1.08, 1.06))
# 8 stone classic (bank, hotel, hospital)
facade("f8", "marble_01", 2.2, "stone", (0.28, 0.2, 0.72, 0.82), (0.25, 0.22, 0.18), (0.42, 0.5, 0.58), 10, wall_tint=(1.05, 1.03, 1.0))
