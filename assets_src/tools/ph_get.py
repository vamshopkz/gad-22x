"""Download CC0 assets from Poly Haven (textures 1k jpg, models fbx 1k, one hdri 2k)."""
import json, os, sys, urllib.request
OUT = sys.argv[1]
TEX = ["asphalt_02", "checkered_pavement_tiles", "concrete_tiles_02", "grass_path_2", "aerial_grass_rock", "sand_01",
       "beige_wall_001", "grey_plaster", "plastered_wall", "red_brick", "brick_wall_001", "concrete_wall_003",
       "rectangular_facade_tiles", "rectangular_facade_tiles_02", "oak_veneer_01", "dark_wood", "marble_01",
       "laminate_floor_02", "wood_floor", "interior_tiles", "terrazzo_tiles", "granite_tile", "grey_roof_tiles_02",
       "clay_roof_tiles_02", "gravel_concrete", "corrugated_iron", "metal_plate", "fabric_pattern_07", "leather_white",
       "velour_velvet", "white_plaster_02", "painted_concrete", "wood_floor_deck",
       # 0.7: house walls and roofs
       "white_bricks", "weathered_plank_siding", "black_painted_planks", "white_planks_clean", "stacked_stone_wall",
       "white_stucco", "roof_slates_02", "red_slate_roof_tiles_01", "exterior_wall_cladding", "yellow_bricks"]
MOD = ["sofa_02", "sofa_03", "modern_arm_chair_01", "modern_coffee_table_01", "coffee_table_round_01", "potted_plant_04",
       "Television_01", "modern_ceiling_lamp_01", "metal_office_desk", "wooden_bookshelf_worn", "side_table_01",
       "planter_box_01", "outdoor_table_chair_set_01", "CashRegister_01", "steel_frame_shelves_01", "classic_laptop",
       "mid_century_lounge_chair", "exterior_aircon_unit", "wooden_table_02", "GothicBed_01", "ArmChair_01",
       "ClassicNightstand_01"]
HDRI = ["kloofendal_48d_partly_cloudy_puresky"]

def get(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0: return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    req = urllib.request.Request(url, headers={"User-Agent": "gad22x-asset-fetch"})
    with urllib.request.urlopen(req, timeout=120) as r, open(path, "wb") as f: f.write(r.read())

def files(asset):
    req = urllib.request.Request("https://api.polyhaven.com/files/" + asset, headers={"User-Agent": "gad22x-asset-fetch"})
    return json.load(urllib.request.urlopen(req, timeout=60))

for t in TEX:
    f = files(t)
    for m, key in (("Diffuse", "diff"), ("nor_gl", "nor"), ("Rough", "rough")):
        e = f.get(m, {}).get("1k", {}).get("jpg") or f.get(m, {}).get("1k", {}).get("png")
        if e: get(e["url"], f"{OUT}/tex/{t}_{key}.jpg")
    print("tex", t, flush=True)
for mname in MOD:
    f = files(mname)
    e = f["fbx"]["1k"]["fbx"]
    get(e["url"], f"{OUT}/models/{mname}/{mname}.fbx")
    for rel, inc in e.get("include", {}).items():
        if rel.endswith(".exr"): continue
        get(inc["url"], f"{OUT}/models/{mname}/{os.path.basename(rel)}")
    print("model", mname, flush=True)
for h in HDRI:
    f = files(h)
    get(f["hdri"]["2k"]["hdr"]["url"], f"{OUT}/hdri/{h}_2k.hdr")
    print("hdri", h)
