"""
Realistic heads for the game's humans from Blender Studio's Human Base Meshes (CC0).
For the male and female realistic bodies: cut the head (down to the mid neck), keep a game mesh at
multires level 1 with the body's UVs rescaled to fill the texture, bake from the sculpt (level 3) a
tangent-space normal map and ambient occlusion, bake the 3D position of every texel, and paint the
face colour from those positions (lips, brows, blush, lids, stubble) in numpy.
Writes <out>/head_<g>.bytes (mesh), head_<g>_n.png, head_<g>_a.png (+ head_m_s.png stubble) and heads.json.

Run: Blender -b human_base_meshes_bundle.blend --python heads.py -- <out dir>
"""
import bpy, bmesh, sys, os, json, struct, math
import numpy as np
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
S = 1024
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 16
sc.view_settings.view_transform = 'Standard'
meta = {}


def mesh_at(src, level, name):
    for m in src.modifiers:
        if m.type == 'MULTIRES':
            m.levels = level; m.render_levels = level; m.sculpt_levels = level
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    ob = bpy.data.objects.new(name, me)
    ob.matrix_world = src.matrix_world.copy()
    sc.collection.objects.link(ob)
    return ob


def cut(ob, zmin, E):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    mw = ob.matrix_world
    def drop(v):
        p = mw @ v.co
        # below the cut, or the slope of the shoulders beside the neck
        return p.z < zmin or (p.z < E.z - 0.115 and abs(p.x - E.x) > 0.056)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if drop(v)], context='VERTS')
    # drop the little islands and threads the cut leaves behind (keep only big connected parts)
    bm.faces.ensure_lookup_table()
    seen, small = set(), []
    for f in bm.faces:
        if f.index in seen: continue
        stack, part = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop(); part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        if len(part) < 200: small.extend(part)
    bmesh.ops.delete(bm, geom=small, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(ob.data); bm.free()


def fill_uv(ob):
    """A fresh unwrap of the head that fills the whole texture."""
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def image(name, float_buf):
    if name in bpy.data.images: bpy.data.images.remove(bpy.data.images[name])
    im = bpy.data.images.new(name, S, S, alpha=True, float_buffer=float_buf)
    im.colorspace_settings.name = 'Non-Color'
    im.generated_color = (0, 0, 0, 0)
    return im


def material(ob, E):
    mat = bpy.data.materials.new("bake_" + ob.name); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sub = nt.nodes.new("ShaderNodeVectorMath"); sub.operation = 'SUBTRACT'; sub.inputs[1].default_value = E
    mul = nt.nodes.new("ShaderNodeVectorMath"); mul.operation = 'MULTIPLY_ADD'
    mul.inputs[1].default_value = (2.0, 2.0, 2.0); mul.inputs[2].default_value = (0.5, 0.5, 0.5)
    nt.links.new(geo.outputs["Position"], sub.inputs[0]); nt.links.new(sub.outputs[0], mul.inputs[0])
    nt.links.new(mul.outputs[0], em.inputs["Color"]); nt.links.new(em.outputs[0], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = tex
    ob.data.materials.clear(); ob.data.materials.append(mat)
    return tex


def bake(low, high, kind, img, tex):
    tex.image = img
    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True)
    if high is not None: high.select_set(True)
    bpy.context.view_layer.objects.active = low
    b = sc.render.bake
    b.use_selected_to_active = high is not None
    b.cage_extrusion = 0.006; b.max_ray_distance = 0.03; b.margin = 8
    b.normal_space = 'TANGENT'
    sc.cycles.samples = 64 if kind == 'AO' else 1
    bpy.ops.object.bake(type=kind)
    return np.array(img.pixels[:], dtype=np.float32).reshape(S, S, 4)


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def noise(seed, scale):
    """Value noise at texel scale (blurred random field)."""
    rng = np.random.default_rng(seed)
    n = S // scale
    g = rng.random((n + 1, n + 1)).astype(np.float32)
    y, x = np.mgrid[0:S, 0:S] / scale
    x0, y0 = x.astype(int), y.astype(int); fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = g[y0, x0] * (1 - fx) + g[y0, x0 + 1] * fx
    c = g[y0 + 1, x0] * (1 - fx) + g[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + c * fy


def paint(pos, ao, female, stubble):
    valid = pos[..., 3] > 0.5
    p = (pos[..., :3] - 0.5) / 2.0
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    front = smooth(-y, -0.02, 0.02)
    col = np.ones((S, S, 3), np.float32)
    col *= (1 + (noise(1, 4) - 0.5) * 0.05 + (noise(2, 32) - 0.5) * 0.06)[..., None]

    def mix(mask, c):
        nonlocal col
        col = col * (1 - mask[..., None]) + np.array(c, np.float32) * mask[..., None]

    # cheeks and nose tip: warmer
    blush = np.exp(-(((x - 0.045) / 0.022) ** 2 + ((z + 0.035) / 0.02) ** 2)) * front
    mix(blush * (0.45 if female else 0.25), (1.0, 0.8, 0.78))
    nose = np.exp(-((x / 0.012) ** 2 + ((z + 0.035) / 0.012) ** 2)) * front
    mix(nose * 0.25, (1.0, 0.82, 0.8))
    # lids and sockets: a soft shadow round the eyes; female: a touch of eyeshadow on the upper lid
    sock = np.exp(-(((x - 0.033) / 0.02) ** 2 + ((z - 0.002) / 0.014) ** 2)) * front
    mix(sock * 0.3, (0.82, 0.72, 0.72))
    if female:
        lid = np.exp(-(((x - 0.034) / 0.016) ** 2 + ((z - 0.01) / 0.006) ** 2)) * front
        mix(lid * 0.45, (0.72, 0.6, 0.62))
    # stubble / beard shadow (male variant)
    if stubble:
        jaw = smooth(-z, 0.045, 0.06) * smooth(z, -0.135, -0.11) * smooth(-x, -0.075, -0.06) * smooth(-y, -0.06, -0.03)
        mous = np.exp(-((x / 0.026) ** 2 + ((z + 0.055) / 0.007) ** 2)) * front
        m = np.clip(jaw + mous, 0, 1)
        speck = smooth(noise(7, 2), 0.35, 0.6) * 0.6 + 0.4
        mix(m * 0.55 * speck, (0.62, 0.56, 0.54))
    # lips: an upper and a lower cushion, a darker line between them
    lz = -0.067 if female else -0.069
    lips = smooth(1 - (((x / 0.024) ** 2) + ((z - lz) / 0.0115) ** 2), 0.0, 0.25) * front
    mix(lips * (0.85 if female else 0.6), (0.86, 0.5, 0.52) if female else (0.8, 0.6, 0.58))
    line = np.exp(-((x / 0.02) ** 2) * 2) * np.exp(-(((z - lz - 0.0005) / 0.0018) ** 2)) * front
    mix(line * 0.5, (0.45, 0.28, 0.28))
    # brows: an arc above each eye, made of short hairs
    bc = 0.022 + 0.011 * (1 - ((x - 0.032) / 0.03) ** 2) - (0.0 if female else 0.002)
    th = 0.0042 if female else 0.0058
    brow = smooth(1 - np.abs(z - bc) / th, 0, 0.5) * smooth(x, 0.008, 0.014) * smooth(-x, -0.06, -0.052) * front
    hairs = smooth(noise(11, 2), 0.3, 0.7) * 0.5 + 0.5
    mix(brow * hairs * (0.82 if female else 0.88), (0.22, 0.16, 0.13))
    # baked occlusion deepens the creases
    col *= (1 - 0.32 * (1 - ao[..., :1]))
    col *= 1.04
    out = np.concatenate([np.clip(col, 0, 1), np.ones((S, S, 1), np.float32)], -1)
    out[~valid] = (0.9, 0.9, 0.9, 1)
    return out


def save_png(arr, path):
    im = bpy.data.images.new(os.path.basename(path), S, S, alpha=True)
    im.pixels[:] = arr.ravel()
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def export(ob, E, path):
    """Mesh in Unity space: face along +Z, Y up, X to the character's right; origin between the eyes."""
    me = ob.data
    me.calc_loop_triangles()
    uvl = me.uv_layers.active.data
    mw = ob.matrix_world
    verts, index, tris = [], {}, []
    for t in me.loop_triangles:
        ids = []
        for li in t.loops:
            l = me.loops[li]
            v = me.vertices[l.vertex_index]
            u = tuple(round(c, 5) for c in uvl[li].uv)
            key = (l.vertex_index, u)
            if key not in index:
                p = mw @ v.co - E
                n = (mw.to_3x3() @ v.normal).normalized()
                index[key] = len(verts)
                verts.append(((-p.x, p.z, -p.y), (-n.x, n.z, -n.y), u))
            ids.append(index[key])
        tris.append((ids[0], ids[2], ids[1]))
    with open(path, "wb") as f:
        f.write(struct.pack("<ii", len(verts), len(tris)))
        for p, n, u in verts: f.write(struct.pack("<8f", *p, *n, *u))
        for t in tris: f.write(struct.pack("<3i", *t))
    return len(verts), len(tris)


for g in ("male", "female"):
    src = bpy.data.objects[f"GEO-body_{g}_realistic"]
    eL = bpy.data.objects[f"GEO-body_{g}_realistic.eye.L"]
    eR = bpy.data.objects[f"GEO-body_{g}_realistic.eye.R"]
    E = (eL.matrix_world.translation + eR.matrix_world.translation) / 2
    low = mesh_at(src, 0, "low_" + g); cut(low, E.z - 0.155, E)
    high = mesh_at(src, 3, "high_" + g); cut(high, E.z - 0.165, E)
    fill_uv(low)
    tex = material(low, E)
    pos = bake(low, None, 'EMIT', image("pos_" + g, True), tex)
    nrm = bake(low, high, 'NORMAL', image("nrm_" + g, False), tex)
    ao = bake(low, high, 'AO', image("ao_" + g, False), tex)
    k = g[0]
    save_png(nrm, f"{OUT}/head_{k}_n.png")
    save_png(paint(pos, ao, g == "female", False), f"{OUT}/head_{k}_a.png")
    if g == "male": save_png(paint(pos, ao, False, True), f"{OUT}/head_m_s.png")
    nv, nt = export(low, E, f"{OUT}/head_{k}.bytes")
    # a lighter copy for people in the crowd (same UVs, so the same textures fit)
    lo = low.copy(); lo.data = low.data.copy(); sc.collection.objects.link(lo)
    dec = lo.modifiers.new("dec", 'DECIMATE'); dec.ratio = 0.22
    bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bpy.ops.object.modifier_apply(modifier="dec")
    lv, lt = export(lo, E, f"{OUT}/head_{k}_lo.bytes")
    print("LOD", g, lv, lt)
    zs = [(low.matrix_world @ v.co).z - E.z for v in low.data.vertices]
    xs = [abs((low.matrix_world @ v.co).x - E.x) for v in low.data.vertices]
    eyes = []
    for e in (eL, eR):
        c = e.matrix_world.translation - E
        eyes.append({"x": -c.x, "y": c.z, "z": -c.y, "r": max(e.dimensions) / 2})
    meta[k] = {"top": max(zs), "bottom": min(zs), "halfWidth": max(xs), "eyes": eyes, "verts": nv, "tris": nt}
    print("HEAD", g, nv, nt, meta[k])
    bpy.data.objects.remove(high, do_unlink=True)

with open(f"{OUT}/heads.json", "w") as f: json.dump(meta, f, indent=1)
print("DONE")
