using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// The permanent part of the world: ground, roads and every building shell, merged into a few
    /// big meshes per 4×4-block region (so the skyline is always there and nothing pops in), static
    /// colliders, and the outskirts (beach, sea, port, railway, mountains with the tunnel, airport).
    /// Street furniture, signs, trees, people and traffic are streamed around the player (Streamer).
    /// </summary>
    public static partial class WorldBuilder
    {
        public static Material[] Facade = new Material[10];
        public static Material Store, Roof, Asphalt, Sidewalk, Grass, Plaza, Concrete, Sand, Water, Rock, Paint, PaintYellow,
            DoorGlass, Frame, RoofTiles, Curbside, LampGlow, WindowGlass, Wood, Metal;
        public static Transform Root;
        public static readonly List<Material> NightMats = new List<Material>();

        public static void Build()
        {
            Root = new GameObject("World").transform;
            MakeMaterials();
            const int R = 4;
            for (int rx = 0; rx < N4(); rx++)
                for (int rz = 0; rz < N4(); rz++)
                {
                    var ground = new MeshB();
                    var shells = new MeshB();
                    for (int bx = rx * R; bx < Mathf.Min(City.N, rx * R + R); bx++)
                        for (int bz = rz * R; bz < Mathf.Min(City.N, rz * R + R); bz++)
                        {
                            BlockGround(ground, bx, bz);
                            foreach (var b in City.ByChunk[bx, bz]) Shell(shells, b);
                        }
                    ground.Build($"Ground {rx},{rz}", Root, 13, false).AddComponent<MeshRendererShadowOff>();
                    shells.Build($"Buildings {rx},{rz}", Root, 10, true);
                }
            var outs = new MeshB();
            foreach (var b in City.Outskirts) Shell(outs, b);
            outs.Build("Outskirt buildings", Root, 10, true);
            Colliders();
            Outskirts();
        }

        static int N4() => (City.N + 3) / 4;

        static void MakeMaterials()
        {
            for (int s = 0; s < Facade.Length; s++)
            {
                Facade[s] = Photo.Facade(s);
                if (Facade[s] == null)
                {
                    var (main, emit) = Tex.Facade(Mathf.Min(s, 7));
                    Facade[s] = Mats.Emissive(main, emit, Color.white, s == 0 || s == 7 ? 0.75f : 0.25f);
                }
                NightMats.Add(Facade[s]);
            }
            Store = Facade[4];
            Roof = Photo.Mat("gravel_concrete", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            Asphalt = Photo.Mat("asphalt_02", new Color(0.82f, 0.82f, 0.84f), 0.25f);
            Sidewalk = Photo.Mat("concrete_tiles_02", new Color(1.05f, 1.05f, 1.03f), 0.18f);
            Plaza = Photo.Mat("granite_tile", Color.white, 0.3f);
            Concrete = Photo.Mat("painted_concrete", new Color(0.9f, 0.9f, 0.88f), 0.15f);
            Curbside = Photo.Mat("concrete_wall_003", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            Grass = Photo.Mat("aerial_grass_rock", new Color(0.62f, 0.85f, 0.45f), 0.05f);
            Sand = Photo.Mat("sand_01", new Color(1.05f, 1.0f, 0.92f), 0.05f);
            Rock = Mats.Tex(Tex.Rock(), Color.white, 0.1f);
            Water = Mats.Glass(new Color(0.12f, 0.42f, 0.55f, 0.85f));
            Water.mainTexture = Tex.Water();
            Paint = Mats.Lit(new Color(0.92f, 0.92f, 0.9f));
            PaintYellow = Mats.Lit(new Color(0.95f, 0.75f, 0.15f));
            DoorGlass = Mats.Lit(new Color(0.12f, 0.16f, 0.2f), 0.9f);
            Frame = Mats.Lit(new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.6f);
            RoofTiles = Photo.Mat("clay_roof_tiles_02", Color.white, 0.2f);
            LampGlow = new Material(Mats.Glow(Color.white)) { color = new Color(0.9f, 0.9f, 0.85f) };
            WindowGlass = Mats.Lit(new Color(0.35f, 0.5f, 0.6f), 0.95f);
            Wood = Photo.Mat("oak_veneer_01", Color.white, 0.35f);
            Metal = Mats.Lit(new Color(0.6f, 0.62f, 0.65f), 0.6f, 0.8f);
        }

        static Material Inner(District d) => d switch
        {
            District.Downtown => Plaza, District.Business => Plaza, District.Industrial => Concrete,
            _ => Grass,
        };

        static void BlockGround(MeshB m, int bx, int bz)
        {
            float X0 = bx * City.P, Z0 = bz * City.P, X1 = X0 + City.P, Z1 = Z0 + City.P;
            float ax0 = bx == 0 ? X0 - City.Road - 2 : X0, az0 = bz == 0 ? Z0 - City.Road - 2 : Z0;
            float ax1 = bx == City.N - 1 ? X1 + City.Road + 2 : X1, az1 = bz == City.N - 1 ? Z1 + City.Road + 2 : Z1;
            m.Flat(Asphalt, ax0, az0, ax1, az1, 0f, 8f);
            m.Box(Curbside, Sidewalk, new Vector3(X0 + City.Road, -0.05f, Z0 + City.Road), new Vector3(X1 - City.Road, City.Curb, Z1 - City.Road), new Vector2(4, 4));
            m.Flat(Inner(City.Districts[bx, bz]), X0 + City.Walk, Z0 + City.Walk, X1 - City.Walk, Z1 - City.Walk, City.Curb + 0.004f, City.Districts[bx, bz] == District.Downtown ? 4f : 8f);

            // markings: left (x = X0) and bottom (z = Z0) roads of this block, plus the outer ones on the edges
            Lanes(m, X0, Z0, Z1, true);
            Lanes(m, Z0, X0, X1, false);
            if (bx == City.N - 1) Lanes(m, X1, Z0, Z1, true);
            if (bz == City.N - 1) Lanes(m, Z1, X0, X1, false);
        }

        /// <summary>Centre dashes and zebra crossings of one road segment between two crossroads.</summary>
        static void Lanes(MeshB m, float line, float a0, float a1, bool alongZ)
        {
            const float y = 0.012f;
            float s0 = a0 + City.Road + 5f, s1 = a1 - City.Road - 5f;
            for (float s = s0; s < s1; s += 6f)
            {
                float e = Mathf.Min(s + 3f, s1);
                if (alongZ) m.Flat(Paint, line - 0.08f, s, line + 0.08f, e, y, 1f);
                else m.Flat(Paint, s, line - 0.08f, e, line + 0.08f, y, 1f);
            }
            // zebra at both ends
            foreach (float c in new[] { a0 + City.Road + 1f, a1 - City.Road - 4f })
                for (float k = -City.Road + 0.6f; k < City.Road - 0.5f; k += 1.1f)
                {
                    if (alongZ) m.Flat(Paint, line + k, c, line + k + 0.55f, c + 3f, y, 1f);
                    else m.Flat(Paint, c, line + k, c + 3f, line + k + 0.55f, y, 1f);
                }
            // stop lines
            if (alongZ)
            {
                m.Flat(Paint, line, a0 + City.Road + 4.3f, line + City.Road - 0.3f, a0 + City.Road + 4.6f, y, 1f);
                m.Flat(Paint, line - City.Road + 0.3f, a1 - City.Road - 4.6f, line, a1 - City.Road - 4.3f, y, 1f);
            }
            else
            {
                m.Flat(Paint, a0 + City.Road + 4.3f, line - City.Road + 0.3f, a0 + City.Road + 4.6f, line, y, 1f);
                m.Flat(Paint, a1 - City.Road - 4.6f, line, a1 - City.Road - 4.3f, line + City.Road - 0.3f, y, 1f);
            }
        }

        static bool Commercial(BType t) => t == BType.Shop || t == BType.Cafe || t == BType.Restaurant || t == BType.Supermarket || t == BType.Clothes
                                         || t == BType.Furniture || t == BType.Gym || t == BType.Mall || t == BType.CarDealer || t == BType.GasStation;

        static readonly Vector2 Tile = new Vector2(14f, 13.2f), StoreTile = new Vector2(14f, 17.6f);

        public static void Shell(MeshB m, Bld b)
        {
            float x0 = b.rect.xMin, z0 = b.rect.yMin, x1 = b.rect.xMax, z1 = b.rect.yMax, y0 = City.Curb, h = b.height;
            var off = new Vector2(H.R(4, b.id) * 0.25f, H.R(4, b.id, 1) * 0.25f);
            var face = Facade[b.style];
            switch (b.type)
            {
                case BType.House:
                {
                    m.Box(face, null, new Vector3(x0, y0, z0), new Vector3(x1, y0 + h, z1), Tile, off);
                    Gable(m, x0, z0, x1, z1, y0 + h, 2.6f, RoofTiles, Facade[b.style]);
                    // eaves boards and a chimney
                    m.Box(Paint, Paint, new Vector3(x0 - 0.5f, y0 + h - 0.32f, z0 - 0.5f), new Vector3(x1 + 0.5f, y0 + h - 0.12f, z0 - 0.3f), new Vector2(4, 4));
                    m.Box(Paint, Paint, new Vector3(x0 - 0.5f, y0 + h - 0.32f, z1 + 0.3f), new Vector3(x1 + 0.5f, y0 + h - 0.12f, z1 + 0.5f), new Vector2(4, 4));
                    m.Box(Curbside, Curbside, new Vector3(x1 - 2.2f, y0 + h, (z0 + z1) / 2 + 1), new Vector3(x1 - 1.4f, y0 + h + 3.2f, (z0 + z1) / 2 + 1.8f), new Vector2(2, 2));
                    break;
                }
                case BType.Villa:
                {
                    // modern villa: glass ground floor, a cantilevered upper volume clad in wood, white roof slabs
                    float dir = b.front == 0 ? -1 : 1;                     // towards the street
                    float zf = b.front == 0 ? z0 : z1, zb = b.front == 0 ? z1 : z0;
                    float w = x1 - x0, g = 3.4f, u = 3.3f;
                    var slab = Photo.Mat("white_plaster_02", new Color(1.08f, 1.08f, 1.06f), 0.25f);
                    var glassRail = Mats.Glass(new Color(0.6f, 0.75f, 0.8f, 0.3f));
                    Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
                    float Zmin(float a1, float a2) => Mathf.Min(a1, a2); float Zmax(float a1, float a2) => Mathf.Max(a1, a2);
                    // ground floor
                    m.Box(face, null, V(x0, y0, z0), V(x1, y0 + g, z1), Tile, off);
                    m.Box(slab, slab, V(x0 - 0.5f, y0 + g, Zmin(zf, zb) - 0.5f), V(x1 + 0.5f, y0 + g + 0.35f, Zmax(zf, zb) + 0.5f), new Vector2(4, 4), default, true);
                    // upper volume, pushed 2.6 m out over the terrace
                    float ux0 = x0 + w * 0.28f, ux1 = x1 + 1.2f;
                    float uzf = zf + dir * 2.6f, uzb = zb - dir * 2.5f;
                    m.uvYStart = 3.3f;
                    m.Box(Facade[6], null, V(ux0, y0 + g + 0.35f, Zmin(uzf, uzb)), V(ux1, y0 + g + 0.35f + u, Zmax(uzf, uzb)), Tile, off + new Vector2(0.25f, 0));
                    m.uvYStart = 0;
                    m.Box(slab, slab, V(ux0 - 0.4f, y0 + g + 0.35f + u, Zmin(uzf, uzb) - 0.4f), V(ux1 + 0.4f, y0 + g + 0.35f + u + 0.35f, Zmax(uzf, uzb) + 0.4f), new Vector2(4, 4), default, true);
                    // slim steel posts under the overhang
                    foreach (float px in new[] { ux0 + 0.3f, ux1 - 0.3f })
                        m.Box(Frame, Frame, V(px - 0.08f, y0, uzf - dir * 0.35f - 0.08f), V(px + 0.08f, y0 + g + 0.35f, uzf - dir * 0.35f + 0.08f), new Vector2(1, 1));
                    // glass railing round the free part of the ground-floor roof (a terrace)
                    float ry = y0 + g + 0.35f;
                    m.Box(glassRail, glassRail, V(x0 - 0.45f, ry, zf + dir * 0.45f - 0.03f), V(ux0, ry + 1.05f, zf + dir * 0.45f + 0.03f), new Vector2(4, 4));
                    m.Box(glassRail, glassRail, V(x0 - 0.48f, ry, Zmin(zf, zb)), V(x0 - 0.42f, ry + 1.05f, Zmax(zf, zb)), new Vector2(4, 4));
                    m.Box(Frame, Frame, V(x0 - 0.47f, ry + 1.03f, Zmin(zf, zb)), V(x0 - 0.43f, ry + 1.08f, Zmax(zf, zb)), new Vector2(1, 1));
                    // garage door and wall lights
                    float gx0 = x1 - 4.6f, gx1 = x1 - 0.6f;
                    m.Box(Frame, Frame, V(gx0, y0, zf + dir * 0.06f - 0.03f), V(gx1, y0 + 2.6f, zf + dir * 0.06f + 0.03f), new Vector2(1, 1));
                    for (float gy = y0 + 0.3f; gy < y0 + 2.6f; gy += 0.32f)
                        m.Box(Curbside, Curbside, V(gx0 + 0.05f, gy, zf + dir * 0.1f - 0.01f), V(gx1 - 0.05f, gy + 0.03f, zf + dir * 0.1f + 0.01f), new Vector2(1, 1));
                    foreach (float lx in new[] { b.door.x - 1.9f, b.door.x + 1.9f, gx0 - 0.4f })
                        m.Box(LampGlow, LampGlow, V(lx - 0.1f, y0 + 2.1f, zf + dir * 0.08f - 0.04f), V(lx + 0.1f, y0 + 2.4f, zf + dir * 0.08f + 0.04f), new Vector2(1, 1));
                    // terrace deck
                    var deck = Photo.Mat("wood_floor_deck", Color.white, 0.2f);
                    m.Box(Paint, deck, V(x0 - 1.5f, y0, Zmin(zf, zf + dir * 3.2f)), V(x1 + 1.5f, y0 + 0.22f, Zmax(zf, zf + dir * 3.2f)), new Vector2(4, 4));
                    break;
                }
                case BType.Tower:
                case BType.Agency:
                {
                    float hl = h * (b.type == BType.Agency ? 0.8f : 0.72f);
                    m.Box(face, Roof, new Vector3(x0, y0, z0), new Vector3(x1, y0 + hl, z1), Tile, off);
                    float ins = Mathf.Min(3f, (x1 - x0) * 0.15f);
                    m.uvYStart = hl;
                    m.Box(face, Roof, new Vector3(x0 + ins, y0 + hl, z0 + ins), new Vector3(x1 - ins, y0 + h, z1 - ins), Tile, off);
                    m.uvYStart = 0;
                    Parapet(m, x0 + ins, z0 + ins, x1 - ins, z1 - ins, y0 + h, Frame);
                    FutureCity.Outline(m, b, x0, z0, x1, z1, y0, y0 + hl);
                    FutureCity.Outline(m, b, x0 + ins, z0 + ins, x1 - ins, z1 - ins, y0 + hl, y0 + h);
                    float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                    if (!b.roofHelipad)
                        m.Box(Concrete, Roof, new Vector3(cx - 4, y0 + h, cz - 3), new Vector3(cx + 4, y0 + h + 4, cz + 3), new Vector2(4, 4));
                    else
                    {
                        m.Box(Concrete, Asphalt, new Vector3(cx - 7, y0 + h, cz - 7), new Vector3(cx + 7, y0 + h + 0.6f, cz + 7), new Vector2(4, 4));
                        m.Flat(PaintYellow, cx - 2.5f, cz - 3f, cx - 1.6f, cz + 3f, y0 + h + 0.62f, 1f);
                        m.Flat(PaintYellow, cx + 1.6f, cz - 3f, cx + 2.5f, cz + 3f, y0 + h + 0.62f, 1f);
                        m.Flat(PaintYellow, cx - 1.6f, cz - 0.45f, cx + 1.6f, cz + 0.45f, y0 + h + 0.62f, 1f);
                    }
                    // antenna
                    m.Box(Metal, Metal, new Vector3(cx - 0.2f, y0 + h, cz + 3.5f), new Vector3(cx + 0.2f, y0 + h + 14f, cz + 3.9f), new Vector2(1, 1));
                    break;
                }
                case BType.Factory: case BType.Warehouse:
                {
                    m.Box(face, Roof, new Vector3(x0, y0, z0), new Vector3(x1, y0 + h, z1), new Vector2(14f, h), off);
                    if (b.type == BType.Factory)
                    {
                        // chimneys and saw-tooth skylights
                        m.Box(Concrete, Concrete, new Vector3(x1 - 6, y0 + h, z1 - 6), new Vector3(x1 - 3, y0 + h + 16, z1 - 3), new Vector2(4, 4));
                        for (float x = x0 + 2; x < x1 - 8; x += 6)
                        {
                            m.Box(Metal, WindowGlass, new Vector3(x, y0 + h, z0 + 3), new Vector3(x + 4, y0 + h + 1.6f, z1 - 3), new Vector2(4, 4));
                        }
                    }
                    // loading gates on the front
                    break;
                }
                case BType.GasStation:
                {
                    m.Box(face, Roof, new Vector3(x0, y0, z0), new Vector3(x1, y0 + h, z1), StoreTile, off);
                    // canopy over the pumps in front
                    float fz = b.front == 0 ? z0 - 13 : z1 + 3;
                    m.Box(Paint, Roof, new Vector3(x0 - 3, y0 + 5f, fz), new Vector3(x1 + 3, y0 + 5.8f, fz + 10), new Vector2(4, 4), default, true, b.Info.Sign.r > 0 ? Paint : null);
                    m.Box(b.Info.Sign.g > 0.5f ? Mats.Lit(b.Info.Sign) : Paint, null, new Vector3(x0 - 3.05f, y0 + 5f, fz - 0.05f), new Vector3(x1 + 3.05f, y0 + 5.8f, fz + 10.05f), new Vector2(4, 4));
                    foreach (float px in new[] { x0 + 1, x1 - 1 })
                        foreach (float pz in new[] { fz + 2, fz + 8 })
                            m.Box(Metal, null, new Vector3(px - 0.2f, y0, pz - 0.2f), new Vector3(px + 0.2f, y0 + 5f, pz + 0.2f), new Vector2(1, 1));
                    break;
                }
                default:
                {
                    if (Commercial(b.type))
                    {
                        float g = Mathf.Min(h, 4.4f);
                        m.Box(Store, Roof, new Vector3(x0, y0, z0), new Vector3(x1, y0 + g, z1), StoreTile, new Vector2(off.x, 0));
                        if (h > g + 0.5f)
                        {
                            var up = Facade[H.R(3, b.id, 9) switch { 0 => 1, 1 => 2, _ => 3 }];
                            m.Box(up, Roof, new Vector3(x0, y0 + g, z0), new Vector3(x1, y0 + h, z1), Tile, off);
                        }
                        // shop sign band across the front
                        Band(m, b, y0 + g - 1.1f, y0 + g - 0.1f, Mats.Lit(b.Info.Sign, 0.4f));
                    }
                    else
                        m.Box(face, Roof, new Vector3(x0, y0, z0), new Vector3(x1, y0 + h, z1), Tile, off);
                    Parapet(m, x0, z0, x1, z1, y0 + h, b.style == 2 ? Concrete : Paint);
                    if (b.type == BType.Apartment) Balconies(m, b, x0, z0, x1, z1, y0, h);
                    if ((b.type == BType.Office || b.type == BType.Hotel || b.type == BType.Bank) && (b.district == District.Downtown || b.district == District.Business) && h > 18)
                        FutureCity.Outline(m, b, x0, z0, x1, z1, y0, y0 + h);
                    // rooftop units
                    if (b.type == BType.Apartment || b.type == BType.Office || b.type == BType.Hotel || b.type == BType.Hospital)
                    {
                        float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                        m.Box(Concrete, Roof, new Vector3(cx - 2.5f, y0 + h, cz - 2), new Vector3(cx + 2.5f, y0 + h + 3f, cz + 2), new Vector2(4, 4));
                    }
                    if (b.type == BType.Hospital)
                    {
                        // red cross on the facade
                        Band(m, b, y0 + h - 6f, y0 + h - 1.5f, Mats.Lit(new Color(0.95f, 0.95f, 0.95f)), 5f);
                    }
                    break;
                }
            }
            Door(m, b);
        }

        static Material balconyGlass, balconySlab;

        /// <summary>Balconies on the street side of a block of flats, lined up with the windows of the facade texture.</summary>
        static void Balconies(MeshB m, Bld b, float x0, float z0, float x1, float z1, float y0, float h)
        {
            balconyGlass ??= Mats.Glass(new Color(0.55f, 0.7f, 0.78f, 0.32f));
            balconySlab ??= Photo.Mat("white_plaster_02", new Color(0.95f, 0.95f, 0.93f), 0.2f);
            float len = b.front <= 1 ? x1 - x0 : z1 - z0;
            int bays = Mathf.FloorToInt(len / 3.5f);
            int floors = Mathf.FloorToInt(h / 3.3f);
            for (int f = 1; f < floors; f++)
                for (int k = 0; k < bays; k++)
                {
                    if ((k + f + b.id) % 2 == 1) continue;
                    float u = (k + 0.5f) * 3.5f;                       // along the facade, from where its texture starts
                    float y = y0 + f * 3.3f;
                    // facade local: x along, z out of the wall
                    Vector3 P(float along, float yy, float outw) => b.front switch
                    {
                        0 => new Vector3(x0 + along, yy, z0 - outw),
                        1 => new Vector3(x1 - along, yy, z1 + outw),
                        2 => new Vector3(x0 - outw, yy, z1 - along),
                        _ => new Vector3(x1 + outw, yy, z0 + along),
                    };
                    void Bx(Material mat, float a0, float a1, float yb, float yt, float o0, float o1)
                    {
                        var p = P(a0, yb, o0); var q = P(a1, yt, o1);
                        m.Box(mat, mat, Vector3.Min(p, q), Vector3.Max(p, q), new Vector2(2, 2), default, true);
                    }
                    Bx(balconySlab, u - 1.5f, u + 1.5f, y - 0.05f, y + 0.12f, 0f, 1.15f);
                    Bx(balconyGlass, u - 1.48f, u + 1.48f, y + 0.12f, y + 1.1f, 1.1f, 1.14f);
                    Bx(balconyGlass, u - 1.5f, u - 1.46f, y + 0.12f, y + 1.1f, 0f, 1.12f);
                    Bx(balconyGlass, u + 1.46f, u + 1.5f, y + 0.12f, y + 1.1f, 0f, 1.12f);
                    Bx(Frame, u - 1.5f, u + 1.5f, y + 1.08f, y + 1.13f, 1.08f, 1.16f);
                }
        }

        /// <summary>A coloured band on the front facade (shop signs).</summary>
        static void Band(MeshB m, Bld b, float ya, float yb, Material mat, float width = -1)
        {
            const float o = 0.12f;
            float x0 = b.rect.xMin, z0 = b.rect.yMin, x1 = b.rect.xMax, z1 = b.rect.yMax;
            if (width > 0)
            {
                float c = b.front <= 1 ? b.rect.center.x : b.rect.center.y;
                if (b.front <= 1) { x0 = c - width / 2; x1 = c + width / 2; } else { z0 = c - width / 2; z1 = c + width / 2; }
            }
            switch (b.front)
            {
                case 0: m.Box(mat, mat, new Vector3(x0 + 0.5f, ya, z0 - o), new Vector3(x1 - 0.5f, yb, z0), new Vector2(4, 4), default, true); break;
                case 1: m.Box(mat, mat, new Vector3(x0 + 0.5f, ya, z1), new Vector3(x1 - 0.5f, yb, z1 + o), new Vector2(4, 4), default, true); break;
                case 2: m.Box(mat, mat, new Vector3(x0 - o, ya, z0 + 0.5f), new Vector3(x0, yb, z1 - 0.5f), new Vector2(4, 4), default, true); break;
                default: m.Box(mat, mat, new Vector3(x1, ya, z0 + 0.5f), new Vector3(x1 + o, yb, z1 - 0.5f), new Vector2(4, 4), default, true); break;
            }
        }

        static void Door(MeshB m, Bld b)
        {
            var d = b.door;
            var dir = b.FrontDir;
            var side = new Vector3(dir.z, 0, -dir.x);
            float w = b.type == BType.Mall || b.type == BType.Airport || b.type == BType.Station ? 4.4f : 2.4f;
            float hh = b.type == BType.House ? 2.3f : 2.8f;
            // frame (protrudes 0.25)
            Vector3 c = d + dir * 0.12f;
            BoxAt(m, Frame, c + Vector3.up * (hh / 2 + 0.05f), side * (w + 0.5f), Vector3.up * (hh + 0.3f), dir * 0.24f);
            // dark glass
            BoxAt(m, DoorGlass, c + dir * 0.02f + Vector3.up * (hh / 2), side * w, Vector3.up * hh, dir * 0.24f);
            // handle bar
            BoxAt(m, Metal, c + dir * 0.18f + Vector3.up * 1.1f, side * (w * 0.6f), Vector3.up * 0.06f, dir * 0.06f);
            // canopy
            if (b.type != BType.House)
                BoxAt(m, Mats.Lit(b.Info.Sign, 0.3f), d + dir * 0.9f + Vector3.up * (hh + 0.55f), side * (w + 1.4f), Vector3.up * 0.18f, dir * 1.8f);
            else
            {
                // porch step
                BoxAt(m, Concrete, d + dir * 0.8f + Vector3.up * 0.08f, side * (w + 1f), Vector3.up * 0.16f, dir * 1.4f);
            }
        }

        /// <summary>Oriented box from centre and three axis vectors (lengths = sizes).</summary>
        public static void BoxAt(MeshB m, Material mat, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az)
        {
            Vector3 hx = ax / 2, hy = ay / 2, hz = az / 2;
            Vector3 P(int sx, int sy, int sz) => c + hx * sx + hy * sy + hz * sz;
            var uv0 = Vector2.zero; var uv1 = Vector2.right; var uv2 = Vector2.one; var uv3 = Vector2.up;
            // faces: +z, -z, +x, -x, +y, -y in local axis terms
            m.Quad(mat, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), uv0, uv3, uv2, uv1);
            m.Quad(mat, P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1), uv0, uv3, uv2, uv1);
            m.Quad(mat, P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), uv0, uv3, uv2, uv1);
            m.Quad(mat, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), uv0, uv3, uv2, uv1);
            m.Quad(mat, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), uv0, uv1, uv2, uv3);
            m.Quad(mat, P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), uv0, uv1, uv2, uv3);
        }

        static void Parapet(MeshB m, float x0, float z0, float x1, float z1, float y, Material mat)
        {
            const float t = 0.3f, hh = 0.7f;
            var tile = new Vector2(4, 4);
            m.Box(mat, mat, new Vector3(x0, y, z0), new Vector3(x1, y + hh, z0 + t), tile);
            m.Box(mat, mat, new Vector3(x0, y, z1 - t), new Vector3(x1, y + hh, z1), tile);
            m.Box(mat, mat, new Vector3(x0, y, z0 + t), new Vector3(x0 + t, y + hh, z1 - t), tile);
            m.Box(mat, mat, new Vector3(x1 - t, y, z0 + t), new Vector3(x1, y + hh, z1 - t), tile);
        }

        /// <summary>Pitched roof: ridge along the longer side, gables filled with the wall material.</summary>
        static void Gable(MeshB m, float x0, float z0, float x1, float z1, float y, float rise, Material roof, Material wall)
        {
            const float ov = 0.5f;
            bool alongX = x1 - x0 >= z1 - z0;
            if (alongX)
            {
                float cz = (z0 + z1) / 2;
                Vector3 a = new Vector3(x0 - ov, y - 0.3f, z0 - ov), b2 = new Vector3(x1 + ov, y - 0.3f, z0 - ov);
                Vector3 r0 = new Vector3(x0 - ov, y + rise, cz), r1 = new Vector3(x1 + ov, y + rise, cz);
                Vector3 c = new Vector3(x0 - ov, y - 0.3f, z1 + ov), d = new Vector3(x1 + ov, y - 0.3f, z1 + ov);
                m.Quad(roof, a, r0, r1, b2, new Vector2(0, 0), new Vector2(0, 2), new Vector2(4, 2), new Vector2(4, 0));
                m.Quad(roof, d, r1, r0, c, new Vector2(0, 0), new Vector2(0, 2), new Vector2(4, 2), new Vector2(4, 0));
                m.Tri(wall, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x0, y + rise - 0.3f, cz), Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.25f, 0.2f));
                m.Tri(wall, new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x1, y + rise - 0.3f, cz), Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.25f, 0.2f));
            }
            else
            {
                float cx = (x0 + x1) / 2;
                Vector3 a = new Vector3(x0 - ov, y - 0.3f, z1 + ov), b2 = new Vector3(x0 - ov, y - 0.3f, z0 - ov);
                Vector3 r0 = new Vector3(cx, y + rise, z1 + ov), r1 = new Vector3(cx, y + rise, z0 - ov);
                Vector3 c = new Vector3(x1 + ov, y - 0.3f, z1 + ov), d = new Vector3(x1 + ov, y - 0.3f, z0 - ov);
                m.Quad(roof, a, r0, r1, b2, new Vector2(0, 0), new Vector2(0, 2), new Vector2(4, 2), new Vector2(4, 0));
                m.Quad(roof, d, r1, r0, c, new Vector2(0, 0), new Vector2(0, 2), new Vector2(4, 2), new Vector2(4, 0));
                m.Tri(wall, new Vector3(x1, y, z0), new Vector3(x0, y, z0), new Vector3(cx, y + rise - 0.3f, z0), Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.25f, 0.2f));
                m.Tri(wall, new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(cx, y + rise - 0.3f, z1), Vector2.zero, new Vector2(0.5f, 0), new Vector2(0.25f, 0.2f));
            }
        }

        // ------------------------------------------------------------------ colliders

        static void Colliders()
        {
            var go = new GameObject("Colliders");
            go.transform.SetParent(Root, false);
            var bgo = new GameObject("Building colliders") { layer = 10 };
            bgo.transform.SetParent(go.transform, false);
            var ggo = new GameObject("Ground colliders") { layer = 13 };
            ggo.transform.SetParent(go.transform, false);

            void Bx(GameObject g, Vector3 min, Vector3 max)
            {
                var c = g.AddComponent<BoxCollider>();
                c.center = (min + max) / 2;
                c.size = max - min;
            }

            // world floor
            Bx(ggo, new Vector3(-3000, -10, City.SeaZ - 400), new Vector3(4000, 0, 4000));
            for (int bx = 0; bx < City.N; bx++)
                for (int bz = 0; bz < City.N; bz++)
                {
                    float X0 = bx * City.P, Z0 = bz * City.P;
                    Bx(ggo, new Vector3(X0 + City.Road, -0.5f, Z0 + City.Road), new Vector3(X0 + City.P - City.Road, City.Curb, Z0 + City.P - City.Road));
                }
            foreach (var b in City.All)
            {
                float h = b.height + (b.type == BType.House ? 2.3f : 0.7f);
                Bx(bgo, new Vector3(b.rect.xMin, 0, b.rect.yMin), new Vector3(b.rect.xMax, City.Curb + h, b.rect.yMax));
                if (b.type == BType.GasStation)
                {
                    float fz = b.front == 0 ? b.rect.yMin - 13 : b.rect.yMax + 3;
                    foreach (float px in new[] { b.rect.xMin + 1, b.rect.xMax - 1 })
                        foreach (float pz in new[] { fz + 2, fz + 8 })
                            Bx(bgo, new Vector3(px - 0.25f, 0, pz - 0.25f), new Vector3(px + 0.25f, 5, pz + 0.25f));
                }
            }
            // sea limit
            Bx(bgo, new Vector3(-3000, -10, City.SeaZ - 60), new Vector3(4000, 50, City.SeaZ - 50));
        }

        // ------------------------------------------------------------------ outskirts

        static void Outskirts()
        {
            var m = new MeshB();
            float S = City.Size;
            // grass all around
            m.Flat(Grass, -2500, 0 - City.Road - 2, 4000, 3600, -0.03f, 16f);
            m.Flat(Grass, -2500, -10, -City.Road - 2, 0, -0.03f, 16f);
            m.Flat(Grass, S + City.Road + 2, -10, 4000, 0, -0.03f, 16f);
            // beach and promenade
            m.Flat(Sand, -2500, City.SeaZ - 300, 4000, -City.Road - 2, -0.02f, 10f);
            m.Box(Curbside, Plaza, new Vector3(-City.Road - 2, -0.05f, -City.Road - 8), new Vector3(S + City.Road + 2, City.Curb, -City.Road - 2), new Vector2(4, 4));
            var water = new MeshB();
            water.Flat(Water, -3000, -3000, 4500, City.SeaZ, 0.25f, 30f);
            var wgo = water.Build("Sea", Root, 4, false);
            wgo.AddComponent<WaterScroll>();

            Port(m);
            Railway(m);
            NorthRoad(m);
            Airport(m);
            Palms(m);
            m.Build("Outskirts", Root, 13, true);
            Mountains();
        }

        static void Port(MeshB m)
        {
            // concrete quay into the sea south of block (0,0)
            float x0 = -40, x1 = 150, z1 = -City.Road - 8, z0 = City.SeaZ - 40;
            m.Box(Concrete, Concrete, new Vector3(x0, -2f, z0), new Vector3(x1, 0.6f, z1), new Vector2(8, 8));
            var rng = new Rng(77);
            Material[] cont = { Mats.Lit(new Color(0.75f, 0.2f, 0.15f)), Mats.Lit(new Color(0.15f, 0.35f, 0.7f)), Mats.Lit(new Color(0.2f, 0.55f, 0.3f)), Mats.Lit(new Color(0.85f, 0.6f, 0.15f)), Mats.Lit(new Color(0.55f, 0.55f, 0.58f)) };
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 3; j++)
                {
                    int stack = 1 + rng.I(3);
                    for (int k = 0; k < stack; k++)
                    {
                        float cx = x0 + 15 + i * 7, cz = z0 + 40 + j * 16;
                        m.Box(cont[rng.I(cont.Length)], cont[rng.I(cont.Length)], new Vector3(cx, 0.6f + k * 2.6f, cz), new Vector3(cx + 2.5f, 0.6f + k * 2.6f + 2.55f, cz + 12f), new Vector2(2, 2.6f));
                    }
                }
            // cranes
            var crane = Mats.Lit(new Color(0.9f, 0.5f, 0.1f));
            for (int c = 0; c < 2; c++)
            {
                float cx = x0 + 100 + c * 30, cz = z0 + 6;
                foreach (float dx in new[] { 0f, 10f })
                    foreach (float dz in new[] { 0f, 10f })
                        m.Box(crane, crane, new Vector3(cx + dx, 0.6f, cz + dz), new Vector3(cx + dx + 1, 30, cz + dz + 1), new Vector2(2, 2));
                m.Box(crane, crane, new Vector3(cx - 1, 30, cz - 25), new Vector3(cx + 12, 32, cz + 30), new Vector2(2, 2));
            }
            // cargo ship
            var hull = Mats.Lit(new Color(0.15f, 0.18f, 0.25f));
            m.Box(hull, Mats.Lit(new Color(0.55f, 0.15f, 0.12f)), new Vector3(x0 + 80, -1f, z0 - 30), new Vector3(x0 + 180, 7f, z0 - 12), new Vector2(4, 4));
            m.Box(Paint, Paint, new Vector3(x0 + 160, 7f, z0 - 28), new Vector3(x0 + 176, 18f, z0 - 14), new Vector2(4, 4));
        }

        static void Railway(MeshB m)
        {
            const float x = -45f;
            m.Box(Concrete, Gravel(), new Vector3(x - 4, -0.03f, -City.Road - 2), new Vector3(x + 4, 0.25f, City.Size + 200), new Vector2(4, 4));
            for (float z = -City.Road; z < City.Size + 200; z += 1.2f)
                m.Box(Wood, Wood, new Vector3(x - 1.4f, 0.25f, z), new Vector3(x + 1.4f, 0.35f, z + 0.3f), new Vector2(1, 1));
            m.Box(Metal, Metal, new Vector3(x - 0.8f, 0.35f, -City.Road), new Vector3(x - 0.7f, 0.5f, City.Size + 200), new Vector2(1, 1));
            m.Box(Metal, Metal, new Vector3(x + 0.7f, 0.35f, -City.Road), new Vector3(x + 0.8f, 0.5f, City.Size + 200), new Vector2(1, 1));
            // platform by the station (block 1,7)
            m.Box(Concrete, Sidewalk, new Vector3(x + 4, -0.05f, 7 * City.P), new Vector3(-City.Road - 1, 1.0f, 8 * City.P), new Vector2(4, 4));
            // a train waiting at the platform
            var body = Mats.Lit(new Color(0.85f, 0.2f, 0.2f), 0.5f);
            for (int i = 0; i < 4; i++)
            {
                float z0 = 7 * City.P + 4 + i * 19;
                m.Box(body, Mats.Lit(new Color(0.3f, 0.3f, 0.32f)), new Vector3(x - 1.6f, 0.9f, z0), new Vector3(x + 1.6f, 4.6f, z0 + 18), new Vector2(18, 3.7f));
                m.Box(WindowGlass, WindowGlass, new Vector3(x - 1.62f, 2.6f, z0 + 1), new Vector3(x + 1.62f, 3.6f, z0 + 17), new Vector2(4, 4));
            }
        }

        static Material gravel;
        static Material Gravel() => gravel ??= Mats.Tex(Tex.Gravel(), new Color(0.6f, 0.58f, 0.55f));

        static void NorthRoad(MeshB m)
        {
            float x = City.NorthRoadX, z0 = City.Size + City.Road, z1 = City.TunnelZ1 + 160;
            m.Flat(Asphalt, x - City.Road, z0, x + City.Road, z1, 0.0f, 8f);
            for (float z = z0 + 4; z < z1; z += 6f) m.Flat(Paint, x - 0.08f, z, x + 0.08f, z + 3f, 0.012f, 1f);
            m.Flat(Paint, x - City.Road + 0.4f, z0, x - City.Road + 0.6f, z1, 0.012f, 1f);
            m.Flat(Paint, x + City.Road - 0.6f, z0, x + City.Road - 0.4f, z1, 0.012f, 1f);
            // turnaround plaza by the lodge
            m.Flat(Asphalt, x - 20, City.TunnelZ1 + 100, x + 14, City.TunnelZ1 + 160, 0.005f, 8f);
            // tunnel: walls, roof, portals, lights
            float t0 = City.TunnelZ0, t1 = City.TunnelZ1;
            m.Box(Concrete, Concrete, new Vector3(x - City.Road - 1.5f, 0, t0), new Vector3(x - City.Road, 7.5f, t1), new Vector2(4, 4));
            m.Box(Concrete, Concrete, new Vector3(x + City.Road, 0, t0), new Vector3(x + City.Road + 1.5f, 7.5f, t1), new Vector2(4, 4));
            m.Box(Concrete, Rock, new Vector3(x - City.Road - 1.5f, 7.5f, t0), new Vector3(x + City.Road + 1.5f, 9f, t1), new Vector2(4, 4), default, true, Concrete);
            foreach (float pz in new[] { t0, t1 })
            {
                m.Box(Concrete, Concrete, new Vector3(x - City.Road - 4, 7.5f, pz - 1), new Vector3(x + City.Road + 4, 10.5f, pz + 1), new Vector2(4, 4));
                m.Box(Concrete, Concrete, new Vector3(x - City.Road - 4, 0, pz - 1), new Vector3(x - City.Road, 7.5f, pz + 1), new Vector2(4, 4));
                m.Box(Concrete, Concrete, new Vector3(x + City.Road, 0, pz - 1), new Vector3(x + City.Road + 4, 7.5f, pz + 1), new Vector2(4, 4));
            }
            for (float z = t0 + 6; z < t1; z += 12)
            {
                m.Box(LampGlow, LampGlow, new Vector3(x - City.Road + 0.01f, 6.4f, z), new Vector3(x - City.Road + 0.3f, 6.6f, z + 2.5f), new Vector2(1, 1));
                m.Box(LampGlow, LampGlow, new Vector3(x + City.Road - 0.3f, 6.4f, z), new Vector3(x + City.Road - 0.01f, 6.6f, z + 2.5f), new Vector2(1, 1));
            }
        }

        static void Airport(MeshB m)
        {
            float x0 = City.AirportX0, x1 = City.AirportX1, z0 = City.AirportZ0, z1 = City.AirportZ1, S = City.Size;
            // access road from the east ring road
            m.Flat(Asphalt, S + City.Road, 7 * City.P - City.Road, x0 + 74, 7 * City.P + City.Road, 0f, 8f);
            for (float x = S + 10; x < x0 + 70; x += 6f) m.Flat(Paint, x, 7 * City.P - 0.08f, x + 3f, 7 * City.P + 0.08f, 0.012f, 1f);
            // apron and runway
            m.Flat(Concrete, x0, 7 * City.P + City.Road, x1, z1, 0.004f, 8f);
            m.Flat(Asphalt, x0 + 20, z1 - 90, x1 - 20, z1 - 40, 0.008f, 8f);
            for (float x = x0 + 40; x < x1 - 40; x += 30f) m.Flat(Paint, x, z1 - 65.5f, x + 15, z1 - 64.5f, 0.014f, 1f);
            for (int i = 0; i < 8; i++)
            {
                m.Flat(Paint, x0 + 30, z1 - 86 + i * 5.5f, x0 + 55, z1 - 84 + i * 5.5f, 0.014f, 1f);
                m.Flat(Paint, x1 - 55, z1 - 86 + i * 5.5f, x1 - 30, z1 - 84 + i * 5.5f, 0.014f, 1f);
            }
            // control tower
            float tx = x0 + 150, tz = 7 * City.P + 30;
            m.Box(Concrete, Concrete, new Vector3(tx, 0, tz), new Vector3(tx + 6, 34, tz + 6), new Vector2(4, 4));
            m.Box(WindowGlass, Roof, new Vector3(tx - 2, 34, tz - 2), new Vector3(tx + 8, 39, tz + 8), new Vector2(4, 4));
            // planes
            for (int i = 0; i < 3; i++) Plane(m, new Vector3(x0 + 230 + i * 120, 0, 7 * City.P + 80 + (i % 2) * 40), i);
            Plane(m, new Vector3(x0 + 420, 0, z1 - 65), 3);
        }

        static void Plane(MeshB m, Vector3 p, int i)
        {
            var white = Mats.Lit(new Color(0.95f, 0.95f, 0.96f), 0.6f);
            var tail = Mats.Lit(i % 2 == 0 ? new Color(0.15f, 0.35f, 0.85f) : new Color(0.85f, 0.2f, 0.2f), 0.5f);
            m.Add(Prim.MeshOf(PrimitiveType.Capsule), Matrix4x4.TRS(p + new Vector3(0, 4.2f, 0), Quaternion.Euler(0, 0, 90), new Vector3(4.2f, 19f, 4.2f)), white);
            m.Box(white, white, p + new Vector3(-4, 3.2f, -17), p + new Vector3(4, 3.8f, 17), new Vector2(4, 4));
            m.Box(white, white, p + new Vector3(-17, 4.2f, -6), p + new Vector3(-14, 4.6f, 6), new Vector2(4, 4));
            m.Box(tail, tail, p + new Vector3(-18, 4.5f, -0.3f), p + new Vector3(-14, 11f, 0.3f), new Vector2(4, 4));
            m.Box(WindowGlass, WindowGlass, p + new Vector3(15, 5.2f, -1.2f), p + new Vector3(17.2f, 6f, 1.2f), new Vector2(4, 4));
            foreach (float dz in new[] { -6f, 6f })
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + new Vector3(1, 2.6f, dz), Quaternion.Euler(0, 0, 90), new Vector3(1.6f, 2.2f, 1.6f)), Metal);
        }

        static void Palms(MeshB m)
        {
            var trunk = Mats.Lit(new Color(0.5f, 0.38f, 0.25f));
            var leaf = Mats.Lit(new Color(0.18f, 0.5f, 0.2f));
            var rng = new Rng(55);
            for (float x = 200; x < City.Size + 100; x += 22)
            {
                float z = -20 - rng.F() * 30;
                var p = new Vector3(x + rng.F(-5, 5), 0, z);
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + Vector3.up * 3.5f, Quaternion.Euler(rng.F(-6, 6), 0, rng.F(-6, 6)), new Vector3(0.45f, 3.5f, 0.45f)), trunk);
                for (int k = 0; k < 6; k++)
                    m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 7f, Quaternion.Euler(-25, k * 60, 0) , new Vector3(0.9f, 0.12f, 4.2f)) * Matrix4x4.Translate(new Vector3(0, 0, 0.45f)), leaf);
                // umbrellas and loungers
                if (rng.Chance(0.6f))
                {
                    var u = p + new Vector3(8, 0, -25);
                    var col = Mats.Lit(Palette.Get(Palette.Cloth, rng.I(12)));
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(u + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.08f, 1.2f, 0.08f)), Metal);
                    m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(u + Vector3.up * 2.4f, Quaternion.identity, new Vector3(3.2f, 0.7f, 3.2f)), col);
                    m.Box(Paint, Paint, u + new Vector3(1, 0, 0.5f), u + new Vector3(1.7f, 0.4f, 2.5f), new Vector2(1, 1));
                }
            }
        }

        static void Mountains()
        {
            // 20 m grid heightmap, rising north of the city; a canyon for the north road, flats around the lodge.
            const float step = 20f;
            float xa = -1600, xb = 2800, za = City.MountainZ - 60, zb = City.MountainZ + 1400;
            int nx = (int)((xb - xa) / step) + 1, nz = (int)((zb - za) / step) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = xa + i * step, z = za + j * step;
                    float ramp = Mathf.Clamp01((z - City.MountainZ) / 260f);
                    float n = Mathf.PerlinNoise(x * 0.0022f + 5, z * 0.0022f + 9) * 0.7f + Mathf.PerlinNoise(x * 0.008f, z * 0.008f) * 0.3f;
                    float hgt = ramp * (40 + n * 260f);
                    float dxr = Mathf.Abs(x - City.NorthRoadX);
                    if (z < City.TunnelZ1 + 200 && dxr < 30) hgt *= Mathf.Clamp01((dxr - 10) / 20f);
                    // lodge flat
                    if (z > City.TunnelZ1 + 60 && z < City.TunnelZ1 + 220 && x > City.NorthRoadX - 60 && x < City.NorthRoadX + 80) hgt = 0;
                    // snow-capped edge far away gets taller
                    hgt += Mathf.Clamp01((z - City.MountainZ - 600) / 600f) * 200f * n;
                    verts[j * nx + i] = new Vector3(x, hgt - 0.05f, z);
                    uvs[j * nx + i] = new Vector2(x / 30f, z / 30f);
                }
            var tris = new List<int>();
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "Mountains" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Mountains") { layer = 13 };
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Rock;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // rock over the tunnel so the canyon looks closed there
            var cap = new MeshB();
            cap.Box(Rock, Rock, new Vector3(City.NorthRoadX - 30, 9f, City.TunnelZ0 + 2), new Vector3(City.NorthRoadX + 30, 70f, City.TunnelZ1 - 2), new Vector2(30, 30));
            cap.Build("Tunnel rock", Root, 13, true);
            var cgo = new GameObject("Tunnel rock collider") { layer = 13 };
            cgo.transform.SetParent(Root, false);
            var bc = cgo.AddComponent<BoxCollider>();
            bc.center = new Vector3(City.NorthRoadX, 39.5f, (City.TunnelZ0 + City.TunnelZ1) / 2);
            bc.size = new Vector3(60, 61, City.TunnelZ1 - City.TunnelZ0 - 4);
            // tunnel side walls as colliders
            foreach (float sx in new[] { -1f, 1f })
            {
                var w = cgo.AddComponent<BoxCollider>();
                w.center = new Vector3(City.NorthRoadX + sx * (City.Road + 0.75f), 4.5f, (City.TunnelZ0 + City.TunnelZ1) / 2);
                w.size = new Vector3(1.5f, 9f, City.TunnelZ1 - City.TunnelZ0);
            }
        }
    }

    /// <summary>Marker: the ground meshes don't cast shadows (set in MeshB.Build).</summary>
    public class MeshRendererShadowOff : MonoBehaviour { }

    public class WaterScroll : MonoBehaviour
    {
        Material m;
        void Start() { m = GetComponent<MeshRenderer>().sharedMaterial; }
        void Update() { if (m != null) m.mainTextureOffset = new Vector2(Time.time * 0.004f, Time.time * 0.006f); }
    }
}
