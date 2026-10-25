using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.7: one house project of the catalogue (100 of them, six families) or a city house fitted to its
    /// footprint. Everything about the look is decided here from the seed; HouseBuilder turns it into
    /// meshes. Sizes in metres; the house stands with its front wall on local z = 0, depth along +z.
    /// </summary>
    public class HouseDesign
    {
        public int id;
        public string name, family;
        public int fam;                 // 0 classic, 1 modern, 2 cottage, 3 mediterranean, 4 scandinavian, 5 townhouse
        public int price;
        public float W, D, FH = 3.1f;
        public int floors;
        public int roofType;            // 0 gable (ridge along the front), 1 gable (ridge to the back), 2 hip, 3 flat, 4 shed
        public float pitch;             // roof slope, degrees
        public int win;                 // 0 classic, 1 tall, 2 panoramic
        public string wallTex, roofTex;
        public Color wallTint, roofTint, trim, door;
        public bool garage, balcony, porch, terrace, chimney, pool, solar, shutters;
        public float doorX;             // front door position along the front wall

        public float Height => floors * FH + (roofType == 3 ? 0.6f : RoofRise);
        public float RoofRise => roofType == 3 ? 0f : roofType == 4 ? 1.4f : Mathf.Tan(pitch * Mathf.Deg2Rad) * (roofType == 1 ? W : roofType == 2 ? Mathf.Min(W, D) : D) * 0.5f;
        public int Area => Mathf.RoundToInt(W * D * floors);

        public const int Count = 100;
        static readonly HouseDesign[] all = new HouseDesign[Count];
        public static readonly string[] Families = { "Классика", "Модерн", "Коттедж", "Средиземноморье", "Скандинавия", "Таунхаус" };
        static readonly string[][] Names =
        {
            new[] { "Оксфорд", "Виндзор", "Кембридж", "Гринвич", "Честер", "Йорк", "Бристоль", "Бат", "Дарем", "Ричмонд", "Кент", "Суррей", "Эссекс", "Норфолк", "Девон", "Корнуолл", "Сомерсет" },
            new[] { "Кристалл", "Горизонт", "Куб", "Призма", "Линия", "Атриум", "Панорама", "Терра", "Аура", "Вектор", "Модуль", "Плаза", "Скай", "Нова", "Эфир", "Квант", "Ориджин" },
            new[] { "Сосна", "Берёзка", "Ромашка", "Василёк", "Рябина", "Калина", "Ивушка", "Липа", "Вишня", "Яблонька", "Клевер", "Мята", "Лаванда", "Жасмин", "Сирень", "Ольха", "Черёмуха" },
            new[] { "Тоскана", "Сицилия", "Капри", "Амальфи", "Санторини", "Майорка", "Ибица", "Марбелья", "Ницца", "Монако", "Палермо", "Верона", "Сиена", "Флоренция", "Кордова", "Севилья", "Гранада" },
            new[] { "Фьорд", "Осло", "Берген", "Мальмё", "Упсала", "Тромсё", "Хельсинки", "Турку", "Рейкьявик", "Нордик", "Лагом", "Хюгге", "Сканди", "Аврора", "Ледник", "Сага", "Викинг" },
            new[] { "Сити", "Лофт", "Бруклин", "Сохо", "Челси", "Манхэттен", "Ноттинг", "Бульвар", "Квартал", "Арбат", "Невский", "Тверская", "Камден", "Монмартр", "Трибека", "Гарлем", "Бронкс" },
        };

        public static HouseDesign Get(int id)
        {
            id = Mathf.Clamp(id, 0, Count - 1);
            return all[id] ??= Make(id, 99f, 99f, true);
        }

        /// <summary>A design for a city house: fits the footprint, no pool or garage outside it.</summary>
        public static HouseDesign ForCity(Bld b)
        {
            bool villa = b.type == BType.Villa;
            var d = Make(1000 + b.id, b.front <= 1 ? b.rect.width : b.rect.height, b.front <= 1 ? b.rect.height : b.rect.width, false, villa ? (H.R(2, b.id, 5) == 0 ? 1 : 3) : -1);
            if (villa || b.district != District.Suburb) d.garage = false;
            if (villa) { d.floors = 2; d.terrace = false; }   // the garden already has a pool deck behind villas
            d.name = b.name;
            return d;
        }

        static HouseDesign Make(int seed, float maxW, float maxD, bool catalogue, int forceFam = -1)
        {
            var r = new Rng((uint)(seed * 7919 + 13));
            var d = new HouseDesign { id = seed };
            d.fam = forceFam >= 0 ? forceFam : catalogue ? seed % 6 : (r.Chance(0.5f) ? 0 : 2 + r.I(2) * 2);
            int tier = catalogue ? seed / 6 % 3 : r.I(2);     // 0 small, 1 medium, 2 large
            float sizeK = 0.75f + tier * 0.18f + r.F(-0.05f, 0.08f);
            d.W = Mathf.Round(Mathf.Clamp(r.F(10f, 15f) * sizeK, 8f, Mathf.Min(14.5f, maxW - 0.2f)) * 2f) / 2f;
            d.D = Mathf.Round(Mathf.Clamp(r.F(9f, 13f) * sizeK, 8f, Mathf.Min(13.5f, maxD - 0.2f)) * 2f) / 2f;
            d.floors = 1 + (tier >= 1 ? 1 : 0) + (tier == 2 && r.Chance(0.4f) ? 1 : 0);
            d.FH = 3.1f;
            d.doorX = 0f;
            Color C(float rr, float g, float b) => new Color(rr, g, b);
            switch (d.fam)
            {
                case 0: // classic: brick, gable or hip, small framed windows, chimney
                    d.wallTex = r.Pick(new[] { "red_brick", "yellow_bricks", "white_bricks" });
                    d.wallTint = Color.white;
                    d.roofType = r.Chance(0.6f) ? 0 : 2; d.pitch = r.F(32f, 40f);
                    d.roofTex = r.Chance(0.5f) ? "grey_roof_tiles_02" : "roof_slates_02"; d.roofTint = Color.white;
                    d.win = 0; d.shutters = r.Chance(0.5f); d.chimney = true; d.porch = r.Chance(0.6f);
                    d.trim = C(0.95f, 0.95f, 0.93f); d.door = r.Pick(new[] { C(0.15f, 0.25f, 0.45f), C(0.45f, 0.08f, 0.1f), C(0.1f, 0.25f, 0.15f) });
                    break;
                case 1: // modern: plaster or concrete, flat roof, panoramic glass, pool
                    d.wallTex = r.Pick(new[] { "white_plaster_02", "concrete_wall_003", "white_stucco" });
                    d.wallTint = r.Chance(0.3f) ? C(0.85f, 0.86f, 0.88f) : Color.white;
                    d.roofType = r.Chance(0.75f) ? 3 : 4; d.pitch = 8f;
                    d.roofTex = "gravel_concrete"; d.roofTint = C(0.6f, 0.6f, 0.62f);
                    d.win = 2; d.pool = catalogue && r.Chance(0.6f); d.terrace = true; d.garage = catalogue && r.Chance(0.7f); d.solar = r.Chance(0.4f);
                    d.balcony = d.floors > 1 && r.Chance(0.6f);
                    d.trim = C(0.12f, 0.12f, 0.13f); d.door = C(0.15f, 0.13f, 0.12f);
                    break;
                case 2: // cottage: painted wooden siding, steep gable, porch, shutters
                    d.wallTex = r.Pick(new[] { "weathered_plank_siding", "white_planks_clean" });
                    d.wallTint = r.Pick(new[] { C(0.75f, 0.85f, 0.95f), C(0.82f, 0.9f, 0.78f), C(1f, 0.93f, 0.7f), C(1f, 0.85f, 0.8f), Color.white });
                    d.roofType = r.Chance(0.5f) ? 0 : 1; d.pitch = r.F(38f, 45f);
                    d.roofTex = r.Chance(0.5f) ? "red_slate_roof_tiles_01" : "grey_roof_tiles_02"; d.roofTint = Color.white;
                    d.win = 0; d.shutters = true; d.porch = r.Chance(0.85f); d.chimney = r.Chance(0.5f);
                    d.trim = Color.white; d.door = r.Pick(new[] { C(0.85f, 0.2f, 0.2f), C(0.2f, 0.35f, 0.6f), C(0.95f, 0.8f, 0.3f) });
                    break;
                case 3: // mediterranean: white or cream stucco, terracotta hip roof, tall windows, balcony
                    d.wallTex = r.Pick(new[] { "white_stucco", "beige_wall_001" });
                    d.wallTint = r.Pick(new[] { Color.white, C(1f, 0.95f, 0.85f), C(1f, 0.9f, 0.8f) });
                    d.roofType = 2; d.pitch = r.F(22f, 28f);
                    d.roofTex = "clay_roof_tiles_02"; d.roofTint = Color.white;
                    d.win = 1; d.balcony = d.floors > 1; d.terrace = true; d.pool = catalogue && r.Chance(0.5f); d.shutters = r.Chance(0.6f);
                    d.trim = C(0.95f, 0.92f, 0.86f); d.door = C(0.4f, 0.25f, 0.12f);
                    break;
                case 4: // scandinavian: black or white boards, steep gable, big windows, deck
                    d.wallTex = r.Chance(0.6f) ? "black_painted_planks" : "white_planks_clean";
                    d.wallTint = Color.white;
                    d.roofType = r.Chance(0.7f) ? 1 : 0; d.pitch = r.F(42f, 50f);
                    d.roofTex = "roof_slates_02"; d.roofTint = C(0.45f, 0.45f, 0.48f);
                    d.win = 2; d.terrace = true; d.chimney = r.Chance(0.4f); d.solar = r.Chance(0.3f);
                    d.trim = d.wallTex == "white_planks_clean" ? C(0.15f, 0.15f, 0.15f) : C(0.85f, 0.75f, 0.55f); d.door = C(0.75f, 0.6f, 0.4f);
                    break;
                default: // townhouse: narrow and tall, stone and cladding, flat or shed roof
                    d.W = Mathf.Clamp(Mathf.Round(r.F(8f, 10f) * 2f) / 2f, 8f, Mathf.Min(10f, maxW - 0.2f));
                    d.floors = Mathf.Max(2, d.floors + (catalogue ? 1 : 0)); if (d.floors > 3) d.floors = 3;
                    d.wallTex = r.Pick(new[] { "stacked_stone_wall", "exterior_wall_cladding", "red_brick" });
                    d.wallTint = Color.white;
                    d.roofType = r.Chance(0.5f) ? 3 : 4; d.pitch = 10f;
                    d.roofTex = "gravel_concrete"; d.roofTint = C(0.55f, 0.55f, 0.57f);
                    d.win = r.Chance(0.5f) ? 1 : 2; d.balcony = true; d.garage = catalogue && r.Chance(0.5f);
                    d.trim = C(0.18f, 0.18f, 0.2f); d.door = C(0.2f, 0.2f, 0.22f);
                    break;
            }
            if (!catalogue) { d.garage = false; d.pool = false; }
            // 0.9: suburban houses with room beside them get their own garage
            if (!catalogue && maxW <= 10.8f && seed % 10 < 7) d.garage = true;
            if (d.porch && d.balcony) d.porch = false;
            d.family = Families[d.fam];
            var list = Names[d.fam];
            d.name = d.family + " «" + list[(seed / 6) % list.Length] + "»";
            float rate = new[] { 900f, 1400f, 800f, 1200f, 1100f, 1000f }[d.fam];
            float p = d.W * d.D * d.floors * rate + (d.pool ? 60000 : 0) + (d.garage ? 25000 : 0) + (d.solar ? 15000 : 0) + (d.balcony ? 8000 : 0) + (d.terrace ? 10000 : 0) + (d.porch ? 6000 : 0);
            d.price = Mathf.RoundToInt(p / 1000f) * 1000;
            return d;
        }

        public string Features()
        {
            var f = new List<string> { floors + (floors == 1 ? " этаж" : " этажа"), Area + " м²" };
            if (garage) f.Add("гараж"); if (pool) f.Add("бассейн"); if (balcony) f.Add("балкон"); if (terrace) f.Add("терраса");
            if (porch) f.Add("крыльцо"); if (chimney) f.Add("камин"); if (solar) f.Add("солнечные панели");
            return string.Join(" · ", f);
        }
    }

    /// <summary>
    /// Builds a house for real: walls with true openings, glass you can see through, a front door that
    /// swings open, floors and stairs, rooms with furniture, a roof, and the yard (porch, terrace,
    /// pool, garage). The exterior goes into a MeshB in any space (a whole block merged, or a single
    /// house); the inside (furniture, colliders, the door, action points) is a separate object built
    /// only near the player.
    /// </summary>
    public static class HouseBuilder
    {
        const float T = 0.22f;   // wall thickness

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material M(string key, Func<Material> make) { if (!mats.TryGetValue(key, out var m) || m == null) mats[key] = m = make(); return m; }
        static Material Wall(HouseDesign d) => Photo.Mat(d.wallTex, d.wallTint, 0.15f);
        static Material RoofM(HouseDesign d) => Photo.Mat(d.roofTex, d.roofTint, 0.2f);
        static Material Trim(Color c) => M("trim" + ColorUtility.ToHtmlStringRGB(c), () => Mats.Lit(c, 0.35f));
        public static Material GlassM => M("glass", () => { var g = Mats.Glass(new Color(0.55f, 0.7f, 0.78f, 0.28f)); g.SetFloat("_Smoothness", 0.96f); return g; });
        static Material Plaster => Photo.Mat("plastered_wall", new Color(0.97f, 0.95f, 0.9f), 0.1f);
        static Material Ceiling => M("ceiling", () => Mats.Lit(new Color(0.96f, 0.96f, 0.95f), 0.1f));
        static Material FloorWood => Photo.Mat("wood_floor", Color.white, 0.35f);
        static Material FloorTile => Photo.Mat("interior_tiles", Color.white, 0.5f);
        static Material Plinth => Photo.Mat("granite_tile", new Color(0.7f, 0.7f, 0.7f), 0.3f);
        static Material Deck => Photo.Mat("wood_floor_deck", Color.white, 0.25f);
        static Material Stone => Photo.Mat("concrete_tiles_02", Color.white, 0.2f);
        static Material Water => M("pool", () => Mats.Lit(new Color(0.15f, 0.55f, 0.8f), 0.97f));
        static Material Oak => Photo.Mat("oak_veneer_01", Color.white, 0.4f);
        static Material Marble => Photo.Mat("marble_01", Color.white, 0.75f);
        static Material White => M("white", () => Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.6f));
        static Material Dark => M("dark", () => Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.7f, 0.3f));
        static Material Steel => M("steel", () => Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.8f, 0.9f));
        static Material Solar => M("solar", () => Mats.Lit(new Color(0.05f, 0.08f, 0.18f), 0.92f, 0.4f));

        // ------------------------------------------------------------------ geometry helpers in house space

        class Ctx
        {
            public MeshB m;
            public Matrix4x4 xf;
            public Vector3 P(Vector3 p) => xf.MultiplyPoint3x4(p);
            public void Quad(Material mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud) =>
                m.Quad(mat, P(a), P(b), P(c), P(d), ua, ub, uc, ud);
            public void Tri(Material mat, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc) => m.Tri(mat, P(a), P(b), P(c), ua, ub, uc);
            /// <summary>Axis-aligned box in house space, UVs in metres / tile; top optional, bottom optional.</summary>
            public void Box(Material side, Vector3 a, Vector3 b, float tile = 2f, Material top = null, bool bottom = false, Material bot = null)
            {
                var mn = Vector3.Min(a, b); var mx = Vector3.Max(a, b);
                float x0 = mn.x, y0 = mn.y, z0 = mn.z, x1 = mx.x, y1 = mx.y, z1 = mx.z;
                Vector2 U(float u, float v) => new Vector2(u / tile, v / tile);
                Quad(side, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), U(x0, y0), U(x0, y1), U(x1, y1), U(x1, y0));
                Quad(side, new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), U(-x1, y0), U(-x1, y1), U(-x0, y1), U(-x0, y0));
                Quad(side, new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), U(-z1, y0), U(-z1, y1), U(-z0, y1), U(-z0, y0));
                Quad(side, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), U(z0, y0), U(z0, y1), U(z1, y1), U(z1, y0));
                var tm = top ?? side;
                Quad(tm, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), U(x0, z0), U(x0, z1), U(x1, z1), U(x1, z0));
                if (bottom) Quad(bot ?? side, new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), U(x0, z1), U(x0, z0), U(x1, z0), U(x1, z1));
            }
            public void Mesh(Mesh mesh, Matrix4x4 local, Material mat) => m.Add(mesh, xf * local, mat);
        }

        struct Opening { public float a, b, y0, y1; public bool door; }

        /// <summary>Openings of one wall on one floor (positions along the wall from its left end, as seen from outside).</summary>
        static List<Opening> Openings(HouseDesign d, int side, int floor, float len)
        {
            var list = new List<Opening>();
            float w, h, sill, gap;
            switch (d.win)
            {
                case 1: w = 1.0f; h = 2.0f; sill = 0.5f; gap = 2.2f; break;
                case 2: w = 2.0f; h = 2.45f; sill = 0.15f; gap = 3.2f; break;
                default: w = 1.15f; h = 1.4f; sill = 0.9f; gap = 2.5f; break;
            }
            if (side == 1 && floor == 0) { sill = Mathf.Max(sill, 1.05f); h = Mathf.Min(h, 1.2f); }   // above the kitchen worktop
            bool front = side == 0;
            float doorC = len / 2f + d.doorX;
            int n = Mathf.Max(1, Mathf.FloorToInt((len - 1f) / gap));
            if (side >= 2 && d.win == 2) n = Mathf.Max(1, n - 1);
            for (int i = 0; i < n; i++)
            {
                float c = len * (i + 0.5f) / n;
                if (front && floor == 0 && Mathf.Abs(c - doorC) < w / 2f + 1.0f) continue;
                // the stairs run along the left wall (side 2): keep that wall blank on upper floors over the stairwell
                if (side == 2 && floor > 0 && len - c > 0.6f && len - c < 6.2f) continue;
                list.Add(new Opening { a = c - w / 2f, b = c + w / 2f, y0 = sill, y1 = sill + h });
            }
            if (front && floor == 0) list.Add(new Opening { a = doorC - 0.6f, b = doorC + 0.6f, y0 = 0f, y1 = 2.25f, door = true });
            if (front && floor == 1 && d.balcony)
            {
                list.RemoveAll(o => Mathf.Abs((o.a + o.b) / 2f - len / 2f) < 1.6f);
                list.Add(new Opening { a = len / 2f - 0.9f, b = len / 2f + 0.9f, y0 = 0.05f, y1 = 2.3f, door = true });
            }
            list.Sort((p, q) => p.a.CompareTo(q.a));
            return list;
        }

        /// <summary>
        /// One wall of one floor from (x0,z0) to (x1,z1) in house space (outside on the right-hand side
        /// when walking from start to end... i.e. normal = out). Solid pieces around the openings; each
        /// opening gets a frame, a sill, glass (or nothing for doors).
        /// </summary>
        static void WallRun(Ctx c, HouseDesign d, Vector3 s, Vector3 e, Vector3 outN, float y0, float hgt, List<Opening> ops, bool exterior, List<(Vector3, Vector3)> cols)
        {
            var dir = (e - s).normalized; float len = Vector3.Distance(s, e);
            var wall = exterior ? Wall(d) : Plaster;
            var trim = Trim(d.trim);
            // the wall as a strip of pieces between openings; boxes are built along the wall's own axes
            void Piece(float a, float b, float ya, float yb)
            {
                if (b - a < 0.01f || yb - ya < 0.01f) return;
                var p0 = s + dir * a - outN * T; var p1 = s + dir * b;
                var lo = Vector3.Min(p0, p1); var hi = Vector3.Max(p0, p1);
                lo.y = y0 + ya; hi.y = y0 + yb;
                c.Box(wall, lo, hi, exterior ? 2.2f : 2f);
                // a plaster lining on the inside face
                var q0 = s + dir * a - outN * T; var q1 = s + dir * b - outN * (T - 0.005f);
                var l2 = Vector3.Min(q0, q1) - outN * 0.004f; var h2 = Vector3.Max(q0, q1) - outN * 0.004f;
                l2.y = y0 + ya; h2.y = y0 + yb;
                if (exterior) c.Box(Plaster, l2, h2, 2f);
                cols?.Add((lo, hi));
            }
            float at = 0f;
            foreach (var o in ops)
            {
                Piece(at, o.a, 0f, hgt);
                Piece(o.a, o.b, 0f, o.y0);
                Piece(o.a, o.b, o.y1, hgt);
                at = o.b;
                if (!exterior) continue;
                // frame: two jambs and a head, a sill outside; glass in windows
                float fw = 0.07f;
                Vector3 A(float u, float y, float out_) => s + dir * u + outN * out_ + Vector3.up * (y0 + y);
                void Bx(Vector3 p, Vector3 q, Material mm) => c.Box(mm, Vector3.Min(p, q), Vector3.Max(p, q), 1f);
                Bx(A(o.a - fw, o.y0, 0.03f) - outN * 0.12f, A(o.a, o.y1 + fw, 0.03f), trim);
                Bx(A(o.b, o.y0, 0.03f) - outN * 0.12f, A(o.b + fw, o.y1 + fw, 0.03f), trim);
                Bx(A(o.a - fw, o.y1, 0.03f) - outN * 0.12f, A(o.b + fw, o.y1 + fw, 0.03f), trim);
                if (!o.door)
                {
                    Bx(A(o.a - 0.1f, o.y0 - 0.06f, 0.08f) - outN * 0.18f, A(o.b + 0.1f, o.y0, 0.08f), trim);
                    // glass in the middle of the wall, with a mullion for wide panes
                    var g0 = A(o.a, o.y0, -T * 0.5f); var g1 = A(o.b, o.y1, -T * 0.5f);
                    c.Quad(GlassM, A(o.a, o.y0, -T * 0.5f), A(o.a, o.y1, -T * 0.5f), A(o.b, o.y1, -T * 0.5f), A(o.b, o.y0, -T * 0.5f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    c.Quad(GlassM, A(o.b, o.y0, -T * 0.5f), A(o.b, o.y1, -T * 0.5f), A(o.a, o.y1, -T * 0.5f), A(o.a, o.y0, -T * 0.5f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    float mid = (o.a + o.b) / 2f;
                    Bx(A(mid - 0.03f, o.y0, -T * 0.5f + 0.03f), A(mid + 0.03f, o.y1, -T * 0.5f - 0.03f), trim);
                    if (d.win == 0) Bx(A(o.a, (o.y0 + o.y1) / 2f - 0.025f, -T * 0.5f + 0.03f), A(o.b, (o.y0 + o.y1) / 2f + 0.025f, -T * 0.5f - 0.03f), trim);
                    cols?.Add((Vector3.Min(g0, g1) - outN * 0.05f, Vector3.Max(g0, g1) + outN * 0.05f));
                    if (d.shutters && d.win != 2)
                    {
                        var sh = Trim(Color.Lerp(d.door, Color.black, 0.15f));
                        float sw = (o.b - o.a) * 0.5f;
                        Bx(A(o.a - fw - sw, o.y0, 0.04f), A(o.a - fw, o.y1, 0.08f), sh);
                        Bx(A(o.b + fw, o.y0, 0.04f), A(o.b + fw + sw, o.y1, 0.08f), sh);
                    }
                }
            }
            Piece(at, len, 0f, hgt);
        }

        // ------------------------------------------------------------------ the house

        /// <summary>Exterior and fixed interior shell (walls, floors, roof, stairs, yard) into c.m.</summary>
        static void Shell(Ctx c, HouseDesign d, List<(Vector3, Vector3)> cols)
        {
            float W = d.W, D = d.D, FH = d.FH, hw = W / 2f;
            float top = d.floors * FH;
            var trim = Trim(d.trim);
            // plinth and ground floor slab
            c.Box(Plinth, new Vector3(-hw - 0.05f, -0.05f, -0.05f), new Vector3(hw + 0.05f, 0.25f, D + 0.05f), 1.5f, FloorWood);
            cols?.Add((new Vector3(-hw, -0.3f, 0f), new Vector3(hw, 0.25f, D)));
            for (int f = 0; f < d.floors; f++)
            {
                float y = 0.25f + f * FH, h = FH - (f == 0 ? 0.25f : 0f);
                if (f > 0) y = f * FH;
                float wh = f == 0 ? FH - 0.25f : FH;
                // four walls: front (z=0, out = -z), back (z=D, out = +z), left (x=-hw, out=-x), right (x=+hw)
                WallRun(c, d, new Vector3(-hw, 0, 0), new Vector3(hw, 0, 0), Vector3.back, y, wh, Openings(d, 0, f, W), true, cols);
                WallRun(c, d, new Vector3(hw, 0, D), new Vector3(-hw, 0, D), Vector3.forward, y, wh, Openings(d, 1, f, W), true, cols);
                WallRun(c, d, new Vector3(-hw, 0, D), new Vector3(-hw, 0, 0), Vector3.left, y, wh, Openings(d, 2, f, D), true, cols);
                WallRun(c, d, new Vector3(hw, 0, 0), new Vector3(hw, 0, D), Vector3.right, y, wh, Openings(d, 3, f, D), true, cols);
                if (f > 0)
                {
                    // floor slab with the stairwell cut out along the left wall
                    float sx0 = -hw + T, sx1 = -hw + T + 1.15f, sz0 = 1.0f, sz1 = 1.0f + StairRun(d) + 0.1f;
                    void Slab(float x0, float z0, float x1, float z1)
                    {
                        if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
                        c.Box(Ceiling, new Vector3(x0, y - 0.2f, z0), new Vector3(x1, y, z1), 2f, FloorWood, true, Ceiling);
                        cols?.Add((new Vector3(x0, y - 0.2f, z0), new Vector3(x1, y, z1)));
                    }
                    Slab(sx1, T, hw - T, D - T);
                    Slab(-hw + T, T, sx1, sz0);
                    Slab(-hw + T, sz1, sx1, D - T);
                    // a railing along the stairwell
                    c.Box(Steel, new Vector3(sx1 - 0.03f, y, sz0), new Vector3(sx1 + 0.03f, y + 0.95f, sz1), 1f);
                    c.Box(Steel, new Vector3(sx1 - 0.04f, y + 0.95f, sz0), new Vector3(sx1 + 0.04f, y + 1.0f, sz1), 1f);
                    for (float z = sz0; z <= sz1; z += 0.6f) c.Box(Steel, new Vector3(sx1 - 0.015f, y, z), new Vector3(sx1 + 0.015f, y + 0.95f, z + 0.03f), 1f);
                    // a band on the facade between floors
                    c.Box(trim, new Vector3(-hw - 0.06f, y - 0.12f, -0.06f), new Vector3(hw + 0.06f, y + 0.06f, 0.0f), 1f);
                    c.Box(trim, new Vector3(-hw - 0.06f, y - 0.12f, D), new Vector3(hw + 0.06f, y + 0.06f, D + 0.06f), 1f);
                }
                // stairs from this floor to the next
                if (f < d.floors - 1) Stairs(c, d, f, cols);
            }
            // ceiling of the top floor
            c.Box(Ceiling, new Vector3(-hw + T, top - 0.12f, T), new Vector3(hw - T, top, D - T), 2f, Ceiling, true, Ceiling);
            Roof(c, d);
            Yard(c, d, cols);
        }

        static float StairRun(HouseDesign d) => Mathf.Ceil(d.FH / 0.19f) * 0.27f;

        static void Stairs(Ctx c, HouseDesign d, int f, List<(Vector3, Vector3)> cols)
        {
            float hw = d.W / 2f, y = f == 0 ? 0.25f : f * d.FH, rise = (f + 1) * d.FH - y;
            int n = Mathf.CeilToInt(rise / 0.19f);
            float run = StairRun(d), x0 = -hw + T + 0.02f, x1 = x0 + 1.1f, z0 = 1.0f;
            for (int i = 0; i < n; i++)
            {
                float zz = z0 + run * i / n, yy = y + rise * (i + 1) / n;
                c.Box(Oak, new Vector3(x0, yy - 0.05f, zz), new Vector3(x1, yy, zz + run / n + 0.02f), 1f);
                c.Box(White, new Vector3(x0, yy - rise / n, zz + 0.02f), new Vector3(x1, yy - 0.05f, zz + 0.04f), 1f);
            }
            if (cols != null) colsRamp.Add((new Vector3((x0 + x1) / 2f, y, z0), new Vector3((x0 + x1) / 2f, y + rise, z0 + run), x1 - x0));
        }
        static readonly List<(Vector3 a, Vector3 b, float w)> colsRamp = new List<(Vector3, Vector3, float)>();

        static void Roof(Ctx c, HouseDesign d)
        {
            float W = d.W, D = d.D, hw = W / 2f, y = d.floors * d.FH, o = 0.45f;
            var rm = RoofM(d); var wall = Wall(d); var trim = Trim(d.trim);
            Vector2 U(float a, float b) => new Vector2(a / 2.5f, b / 2.5f);
            switch (d.roofType)
            {
                case 3: // flat with a parapet
                    c.Box(trim, new Vector3(-hw - 0.08f, y, -0.08f), new Vector3(hw + 0.08f, y + 0.12f, D + 0.08f), 1f, Photo.Mat(d.roofTex, d.roofTint, 0.1f));
                    c.Box(trim, new Vector3(-hw - 0.08f, y, -0.08f), new Vector3(hw + 0.08f, y + 0.6f, 0.12f), 1f);
                    c.Box(trim, new Vector3(-hw - 0.08f, y, D - 0.12f), new Vector3(hw + 0.08f, y + 0.6f, D + 0.08f), 1f);
                    c.Box(trim, new Vector3(-hw - 0.08f, y, -0.08f), new Vector3(-hw + 0.12f, y + 0.6f, D + 0.08f), 1f);
                    c.Box(trim, new Vector3(hw - 0.12f, y, -0.08f), new Vector3(hw + 0.08f, y + 0.6f, D + 0.08f), 1f);
                    if (d.solar) for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++)
                        c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(-hw + 1.5f + i * 1.9f, y + 0.5f, D * 0.35f + j * 1.3f), Quaternion.Euler(-20, 0, 0), new Vector3(1.7f, 0.05f, 1.1f)), Solar);
                    break;
                case 4: // shed: high at the front, low at the back
                {
                    float hi = y + d.RoofRise, lo = y + 0.2f;
                    c.Quad(rm, new Vector3(-hw - o, hi, -o), new Vector3(-hw - o, lo, D + o), new Vector3(hw + o, lo, D + o), new Vector3(hw + o, hi, -o), U(0, 0), U(0, D + 2 * o), U(W + 2 * o, D + 2 * o), U(W + 2 * o, 0));
                    c.Quad(trim, new Vector3(hw + o, hi - 0.15f, -o), new Vector3(hw + o, lo - 0.15f, D + o), new Vector3(-hw - o, lo - 0.15f, D + o), new Vector3(-hw - o, hi - 0.15f, -o), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    // the walls rise to meet the slope on the sides and the front
                    foreach (float sx in new[] { -hw, hw })
                    {
                        var a = new Vector3(sx, y, 0); var b = new Vector3(sx, hi, 0); var e = new Vector3(sx, lo, D); var f = new Vector3(sx, y, D);
                        if (sx < 0) c.Quad(wall, f, e, b, a, U(D, 0), U(D, lo - y), U(0, hi - y), U(0, 0));
                        else c.Quad(wall, a, b, e, f, U(0, 0), U(0, hi - y), U(D, lo - y), U(D, 0));
                    }
                    c.Quad(wall, new Vector3(-hw, y, 0), new Vector3(-hw, hi, 0), new Vector3(hw, hi, 0), new Vector3(hw, y, 0), U(0, 0), U(0, hi - y), U(W, hi - y), U(W, 0));
                    break;
                }
                case 2: // hip: four slopes, ridge along the longer side
                {
                    float rise = d.RoofRise, alongX = W >= D ? 1 : 0;
                    float inset = alongX > 0 ? D / 2f : hw;
                    var e0 = new Vector3(-hw - o, y, -o); var e1 = new Vector3(hw + o, y, -o); var e2 = new Vector3(hw + o, y, D + o); var e3 = new Vector3(-hw - o, y, D + o);
                    Vector3 r0, r1;
                    if (alongX > 0) { r0 = new Vector3(-hw + inset, y + rise, D / 2f); r1 = new Vector3(hw - inset, y + rise, D / 2f); }
                    else { r0 = new Vector3(0, y + rise, inset); r1 = new Vector3(0, y + rise, D - inset); }
                    if (alongX > 0)
                    {
                        c.Quad(rm, e0, r0, r1, e1, U(0, 0), U(inset, 4), U(W - inset, 4), U(W + 2 * o, 0));
                        c.Quad(rm, e2, r1, r0, e3, U(0, 0), U(inset, 4), U(W - inset, 4), U(W + 2 * o, 0));
                        c.Tri(rm, e3, r0, e0, U(0, 0), U(D / 2, 4), U(D + 2 * o, 0));
                        c.Tri(rm, e1, r1, e2, U(0, 0), U(D / 2, 4), U(D + 2 * o, 0));
                    }
                    else
                    {
                        c.Quad(rm, e3, r1, r0, e0, U(0, 0), U(inset, 4), U(D - inset, 4), U(D + 2 * o, 0));
                        c.Quad(rm, e1, r0, r1, e2, U(0, 0), U(inset, 4), U(D - inset, 4), U(D + 2 * o, 0));
                        c.Tri(rm, e0, r0, e1, U(0, 0), U(W / 2, 4), U(W + 2 * o, 0));
                        c.Tri(rm, e2, r1, e3, U(0, 0), U(W / 2, 4), U(W + 2 * o, 0));
                    }
                    Soffit(c, d, y, o);
                    break;
                }
                default: // gable: ridge along x (0) or along z (1)
                {
                    float rise = d.RoofRise;
                    if (d.roofType == 0)
                    {
                        var ra = new Vector3(-hw - o, y + rise, D / 2f); var rb = new Vector3(hw + o, y + rise, D / 2f);
                        float sl = Mathf.Sqrt(rise * rise + (D / 2f + o) * (D / 2f + o));
                        c.Quad(rm, new Vector3(-hw - o, y - o * rise / (D / 2f), -o), ra, rb, new Vector3(hw + o, y - o * rise / (D / 2f), -o), U(0, 0), U(0, sl), U(W + 2 * o, sl), U(W + 2 * o, 0));
                        c.Quad(rm, new Vector3(hw + o, y - o * rise / (D / 2f), D + o), rb, ra, new Vector3(-hw - o, y - o * rise / (D / 2f), D + o), U(0, 0), U(0, sl), U(W + 2 * o, sl), U(W + 2 * o, 0));
                        // gable ends in the wall material
                        c.Tri(wall, new Vector3(-hw, y, D), new Vector3(-hw, y + rise, D / 2f), new Vector3(-hw, y, 0), U(D, 0), U(D / 2, rise), U(0, 0));
                        c.Tri(wall, new Vector3(hw, y, 0), new Vector3(hw, y + rise, D / 2f), new Vector3(hw, y, D), U(0, 0), U(D / 2, rise), U(D, 0));
                        c.Box(trim, new Vector3(-hw - o, y + rise - 0.02f, D / 2f - 0.08f), new Vector3(hw + o, y + rise + 0.12f, D / 2f + 0.08f), 1f);
                    }
                    else
                    {
                        var ra = new Vector3(0, y + rise, -o); var rb = new Vector3(0, y + rise, D + o);
                        float sl = Mathf.Sqrt(rise * rise + (hw + o) * (hw + o));
                        c.Quad(rm, new Vector3(-hw - o, y - o * rise / hw, D + o), rb, ra, new Vector3(-hw - o, y - o * rise / hw, -o), U(0, 0), U(0, sl), U(D + 2 * o, sl), U(D + 2 * o, 0));
                        c.Quad(rm, new Vector3(hw + o, y - o * rise / hw, -o), ra, rb, new Vector3(hw + o, y - o * rise / hw, D + o), U(0, 0), U(0, sl), U(D + 2 * o, sl), U(D + 2 * o, 0));
                        c.Tri(wall, new Vector3(-hw, y, 0), new Vector3(0, y + rise, 0), new Vector3(hw, y, 0), U(0, 0), U(hw, rise), U(W, 0));
                        c.Tri(wall, new Vector3(hw, y, D), new Vector3(0, y + rise, D), new Vector3(-hw, y, D), U(0, 0), U(hw, rise), U(W, 0));
                        c.Box(trim, new Vector3(-0.08f, y + rise - 0.02f, -o), new Vector3(0.08f, y + rise + 0.12f, D + o), 1f);
                        // a round window in the front gable
                        c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(new Vector3(0, y + rise * 0.45f, -0.02f), Quaternion.Euler(90, 0, 0), new Vector3(0.7f, 0.03f, 0.7f)), trim);
                        c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(new Vector3(0, y + rise * 0.45f, -0.05f), Quaternion.Euler(90, 0, 0), new Vector3(0.55f, 0.03f, 0.55f)), Dark);
                    }
                    Soffit(c, d, y, o);
                    if (d.solar)
                        for (int i = 0; i < 4; i++)
                            c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(d.roofType == 0 ? new Vector3(-hw + 1.4f + i * 1.8f, y + rise * 0.5f + 0.08f, D * 0.25f) : new Vector3(hw * 0.5f, y + rise * 0.5f + 0.08f, 1.5f + i * 1.8f),
                                d.roofType == 0 ? Quaternion.Euler(-Mathf.Atan2(rise, D / 2f) * Mathf.Rad2Deg, 0, 0) : Quaternion.Euler(0, 0, -Mathf.Atan2(rise, hw) * Mathf.Rad2Deg), new Vector3(1.6f, 0.05f, 1.0f)), Solar);
                    break;
                }
            }
            if (d.chimney)
            {
                float cx = d.roofType == 1 ? hw * 0.5f : hw * 0.55f, cz = d.roofType == 1 ? D * 0.6f : D * 0.3f;
                c.Box(Photo.Mat("red_brick", Color.white, 0.15f), new Vector3(cx - 0.35f, y, cz - 0.35f), new Vector3(cx + 0.35f, y + d.RoofRise + 0.9f, cz + 0.35f), 1.2f);
                c.Box(Dark, new Vector3(cx - 0.42f, y + d.RoofRise + 0.9f, cz - 0.42f), new Vector3(cx + 0.42f, y + d.RoofRise + 1.0f, cz + 0.42f), 1f);
            }
            // gutters along the eaves of pitched roofs
            if (d.roofType != 3)
            {
                c.Box(Steel, new Vector3(-hw - 0.45f, y - 0.25f, -0.55f), new Vector3(hw + 0.45f, y - 0.12f, -0.45f), 1f);
                foreach (float sx in new[] { -hw - 0.3f, hw + 0.3f })
                    c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(new Vector3(sx, y / 2f, -0.25f), Quaternion.identity, new Vector3(0.09f, y / 2f, 0.09f)), Steel);
            }
        }

        static void Soffit(Ctx c, HouseDesign d, float y, float o)
        {
            float hw = d.W / 2f, D = d.D;
            var trim = Trim(d.trim);
            c.Box(trim, new Vector3(-hw - o, y - 0.08f, -o), new Vector3(hw + o, y, 0f), 1f, null, true);
            c.Box(trim, new Vector3(-hw - o, y - 0.08f, D), new Vector3(hw + o, y, D + o), 1f, null, true);
            c.Box(trim, new Vector3(-hw - o, y - 0.08f, 0f), new Vector3(-hw, y, D), 1f, null, true);
            c.Box(trim, new Vector3(hw, y - 0.08f, 0f), new Vector3(hw + o, y, D), 1f, null, true);
        }

        static void Yard(Ctx c, HouseDesign d, List<(Vector3, Vector3)> cols)
        {
            float W = d.W, D = d.D, hw = W / 2f, FH = d.FH;
            var trim = Trim(d.trim);
            // front steps and path to the street
            float dx = d.doorX;
            c.Box(Stone, new Vector3(dx - 1.0f, 0f, -1.0f), new Vector3(dx + 1.0f, 0.12f, 0f), 1.5f);
            c.Box(Stone, new Vector3(dx - 0.8f, 0f, -4.5f), new Vector3(dx + 0.8f, 0.03f, -1.0f), 1.5f);
            if (d.porch)
            {
                c.Box(Deck, new Vector3(-hw + 0.5f, 0f, -2.2f), new Vector3(hw - 0.5f, 0.24f, 0f), 2f);
                cols?.Add((new Vector3(-hw + 0.5f, -0.2f, -2.2f), new Vector3(hw - 0.5f, 0.24f, 0f)));
                c.Box(trim, new Vector3(-hw + 0.3f, FH - 0.35f, -2.4f), new Vector3(hw - 0.3f, FH - 0.2f, 0f), 1f, null, true);
                c.Box(RoofM(d), new Vector3(-hw + 0.25f, FH - 0.2f, -2.45f), new Vector3(hw - 0.25f, FH - 0.12f, 0f), 2f);
                foreach (float px in new[] { -hw + 0.7f, hw - 0.7f, dx - 1.2f, dx + 1.2f })
                    c.Mesh(Gad.Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(new Vector3(px, (FH - 0.35f + 0.24f) / 2f, -2.1f), Quaternion.identity, new Vector3(0.18f, (FH - 0.59f) / 2f, 0.18f)), trim);
            }
            else
            {
                // a canopy over the door
                c.Box(trim, new Vector3(dx - 1.1f, 2.5f, -1.1f), new Vector3(dx + 1.1f, 2.62f, 0f), 1f, null, true);
            }
            // a lamp beside the door
            c.Box(Dark, new Vector3(dx + 0.85f, 1.7f, -0.12f), new Vector3(dx + 1.0f, 2.0f, 0f), 1f);
            c.Box(M("doorlamp", () => Mats.Glow(new Color(1f, 0.85f, 0.6f))), new Vector3(dx + 0.87f, 1.74f, -0.13f), new Vector3(dx + 0.98f, 1.94f, -0.12f), 1f);
            if (d.balcony && d.floors > 1)
            {
                float y = FH;
                c.Box(Stone, new Vector3(-1.6f, y - 0.18f, -1.3f), new Vector3(1.6f, y + 0.02f, 0f), 1.5f, null, true);
                cols?.Add((new Vector3(-1.6f, y - 0.18f, -1.3f), new Vector3(1.6f, y + 0.02f, 0f)));
                foreach (var (a, b) in new[] { (new Vector3(-1.6f, y, -1.3f), new Vector3(1.6f, y + 1.0f, -1.26f)), (new Vector3(-1.6f, y, -1.3f), new Vector3(-1.56f, y + 1.0f, 0f)), (new Vector3(1.56f, y, -1.3f), new Vector3(1.6f, y + 1.0f, 0f)) })
                {
                    c.Quad(GlassM, new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    c.Quad(GlassM, new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    c.Box(Steel, new Vector3(Mathf.Min(a.x, b.x), b.y, Mathf.Min(a.z, b.z)), new Vector3(Mathf.Max(a.x, b.x), b.y + 0.05f, Mathf.Max(a.z, b.z)), 1f);
                    cols?.Add((Vector3.Min(a, b), Vector3.Max(a, b)));
                }
            }
            if (d.terrace)
            {
                c.Box(Deck, new Vector3(-hw, 0f, D), new Vector3(hw, 0.22f, D + 3.2f), 2f);
                cols?.Add((new Vector3(-hw, -0.2f, D), new Vector3(hw, 0.22f, D + 3.2f)));
            }
            if (d.pool)
            {
                float z0 = D + (d.terrace ? 3.6f : 1.2f);
                c.Box(Photo.Mat("granite_tile", Color.white, 0.4f), new Vector3(-3.4f, 0f, z0), new Vector3(3.4f, 0.55f, z0 + 4.4f), 1.2f);
                c.Box(Water, new Vector3(-3.1f, 0.3f, z0 + 0.3f), new Vector3(3.1f, 0.5f, z0 + 4.1f), 2f);
                cols?.Add((new Vector3(-3.4f, 0f, z0), new Vector3(3.4f, 0.55f, z0 + 4.4f)));
                for (int i = 0; i < 2; i++)
                    c.Box(White, new Vector3(-2.8f + i * 1.2f, 0.55f, z0 + 4.6f), new Vector3(-2.2f + i * 1.2f, 0.75f, z0 + 6.2f), 1f);
            }
            if (d.garage)
            {
                float gx0 = hw, gx1 = hw + 3.8f, gd = Mathf.Min(D, 6.5f), gh = 2.9f;
                c.Box(Wall(d), new Vector3(gx0, 0f, 0f), new Vector3(gx1, gh, gd), 2.2f, Photo.Mat("gravel_concrete", new Color(0.6f, 0.6f, 0.6f), 0.1f));
                c.Box(trim, new Vector3(gx0 + 0.3f, 0.05f, -0.03f), new Vector3(gx1 - 0.3f, 2.3f, 0.01f), 1f);
                for (float yy = 0.4f; yy < 2.3f; yy += 0.45f)
                    c.Box(Trim(Color.Lerp(d.trim, Color.black, 0.2f)), new Vector3(gx0 + 0.3f, yy, -0.05f), new Vector3(gx1 - 0.3f, yy + 0.03f, -0.02f), 1f);
                c.Box(Stone, new Vector3(gx0 + 0.2f, 0f, -6f), new Vector3(gx1 - 0.2f, 0.03f, 0f), 2f);
                cols?.Add((new Vector3(gx0, 0f, 0f), new Vector3(gx1, gh, gd)));
            }
        }

        // ------------------------------------------------------------------ furniture and life inside

        static void Furnish(Transform root, HouseDesign d, List<WorldActions.Point> points, bool owned, Action onSleep, Action onWardrobe)
        {
            float hw = d.W / 2f, D = d.D, FH = d.FH;
            float inX0 = -hw + T + 1.3f, inX1 = hw - T - 0.3f;   // the strip right of the stairs
            GameObject Model(string n, Vector3 p, float yaw, float fit, Color? tint = null) => ModelLib.Place(root, n, p, yaw, fit, true, tint);
            float y0 = 0.25f;
            // living room at the front right: sofa facing a TV on the right wall, a coffee table, a rug, an armchair, plants
            float lx = (inX0 + inX1) / 2f + 0.6f;
            Model(d.fam == 1 || d.fam == 4 ? "sofa_03" : "sofa_02", new Vector3(lx - 0.6f, y0, 1.4f), 0, 2.2f, Color.Lerp(d.door, Color.white, 0.6f));
            Model("modern_coffee_table_01", new Vector3(lx - 0.6f, y0, 2.6f), 0, 1.1f);
            Model("Television_01", new Vector3(lx - 0.6f, y0 + 0.45f, Mathf.Min(D * 0.45f, 4.4f)), 180, 1.3f);
            var cab = new MeshB();
            var cx = new Ctx { m = cab, xf = Matrix4x4.identity };
            cx.Box(Oak, new Vector3(lx - 1.5f, y0, Mathf.Min(D * 0.45f, 4.4f) - 0.25f), new Vector3(lx + 0.3f, y0 + 0.45f, Mathf.Min(D * 0.45f, 4.4f) + 0.25f), 1f);
            cx.Box(Photo.Mat("velour_velvet", Color.Lerp(d.door, Color.white, 0.3f), 0.05f), new Vector3(lx - 2.0f, y0, 1.9f), new Vector3(lx + 0.8f, y0 + 0.015f, 3.4f), 1.5f);
            Model("ArmChair_01", new Vector3(inX1 - 0.6f, y0, 2.4f), -90, 0.9f);
            Model("potted_plant_04", new Vector3(inX1 - 0.4f, y0, 0.6f), 0, 0.7f);
            Model("modern_ceiling_lamp_01", new Vector3(lx - 0.6f, (d.floors > 1 ? FH - 0.2f : FH) - 0.6f, 2.4f), 0, 0.6f);
            // kitchen along the back wall: cabinets, worktop, a fridge, a hob, wall cupboards; a dining table
            float kz = D - T - 0.62f, kx0 = inX0 + 0.3f, kx1 = inX1;
            cx.Box(Oak, new Vector3(kx0, y0, kz), new Vector3(kx1 - 0.8f, y0 + 0.86f, D - T), 1f);
            cx.Box(Marble, new Vector3(kx0 - 0.02f, y0 + 0.86f, kz - 0.03f), new Vector3(kx1 - 0.78f, y0 + 0.9f, D - T), 1f);
            cx.Box(Oak, new Vector3(kx0, y0 + 1.5f, D - T - 0.35f), new Vector3(kx1 - 0.8f, y0 + 2.2f, D - T), 1f);
            cx.Box(Steel, new Vector3(kx1 - 0.78f, y0, D - T - 0.7f), new Vector3(kx1, y0 + 2.0f, D - T), 1f);
            cx.Box(Dark, new Vector3(kx0 + 0.8f, y0 + 0.9f, kz + 0.05f), new Vector3(kx0 + 1.4f, y0 + 0.905f, kz + 0.55f), 1f);
            cx.Box(Steel, new Vector3(kx0 + 2.0f, y0 + 0.9f, kz + 0.1f), new Vector3(kx0 + 2.5f, y0 + 0.92f, kz + 0.5f), 1f);
            for (float u = kx0 + 0.3f; u < kx1 - 1.0f; u += 0.6f)
                cx.Box(Steel, new Vector3(u, y0 + 0.75f, kz - 0.02f), new Vector3(u + 0.15f, y0 + 0.77f, kz), 1f);
            float tz = D * 0.62f;
            Model("wooden_table_02", new Vector3((inX0 + inX1) / 2f, y0, tz), 0, 1.6f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var pp = new Vector3((inX0 + inX1) / 2f, y0, tz) + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -1.0f);
                cx.Box(Oak, pp + new Vector3(-0.22f, 0.43f, -0.22f), pp + new Vector3(0.22f, 0.47f, 0.22f), 1f);
                var back = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -0.2f);
                cx.Box(Oak, pp + back + new Vector3(-0.2f, 0.47f, -0.02f), pp + back + new Vector3(0.2f, 0.95f, 0.02f), 1f);
                foreach (var lg in new[] { new Vector3(-0.18f, 0, -0.18f), new Vector3(0.18f, 0, -0.18f), new Vector3(-0.18f, 0, 0.18f), new Vector3(0.18f, 0, 0.18f) })
                    cx.Box(Dark, pp + lg + new Vector3(-0.02f, 0f, -0.02f), pp + lg + new Vector3(0.02f, 0.43f, 0.02f), 1f);
            }
            if (d.chimney)
            {   // a fireplace on the right wall
                cx.Box(Photo.Mat("red_brick", Color.white, 0.15f), new Vector3(hw - T - 0.5f, y0, D * 0.3f - 0.8f), new Vector3(hw - T, y0 + 1.3f, D * 0.3f + 0.8f), 1f);
                cx.Box(Dark, new Vector3(hw - T - 0.52f, y0 + 0.1f, D * 0.3f - 0.45f), new Vector3(hw - T - 0.49f, y0 + 0.8f, D * 0.3f + 0.45f), 1f);
                cx.Box(M("fire", () => Mats.Glow(new Color(1f, 0.55f, 0.15f))), new Vector3(hw - T - 0.45f, y0 + 0.12f, D * 0.3f - 0.3f), new Vector3(hw - T - 0.2f, y0 + 0.4f, D * 0.3f + 0.3f), 1f);
            }
            // upper floors: a bedroom at the front, a bathroom at the back behind a partition with a doorway
            for (int f = 1; f < d.floors; f++)
            {
                float y = f * d.FH;
                float pz = D * 0.62f;
                cx.Box(Plaster, new Vector3(inX0 - 0.05f, y, pz - 0.06f), new Vector3(inX0 + 0.05f, y + FH - 0.12f, pz + 0.06f), 2f);
                cx.Box(Plaster, new Vector3(inX0 + 0.95f, y, pz - 0.06f), new Vector3(inX1 + 0.3f, y + FH - 0.12f, pz + 0.06f), 2f);
                cx.Box(Plaster, new Vector3(inX0 + 0.05f, y + 2.15f, pz - 0.06f), new Vector3(inX0 + 0.95f, y + FH - 0.12f, pz + 0.06f), 2f);
                cx.Box(FloorTile, new Vector3(inX0 + 0.05f, y + 0.001f, pz + 0.06f), new Vector3(hw - T, y + 0.012f, D - T), 1f);
                var bed = Model("GothicBed_01", new Vector3((inX0 + inX1) / 2f + 0.4f, y, 1.4f), 0, 2.1f);
                Model("ClassicNightstand_01", new Vector3((inX0 + inX1) / 2f + 1.75f, y, 0.55f), 0, 0.5f);
                Model("ClassicNightstand_01", new Vector3((inX0 + inX1) / 2f - 0.95f, y, 0.55f), 0, 0.5f);
                Model("modern_ceiling_lamp_01", new Vector3((inX0 + inX1) / 2f, y + FH - 0.75f, 2.0f), 0, 0.5f);
                // a wardrobe on the right wall
                cx.Box(Oak, new Vector3(hw - T - 0.62f, y, pz - 2.2f), new Vector3(hw - T, y + 2.2f, pz - 0.15f), 1f);
                cx.Box(Steel, new Vector3(hw - T - 0.64f, y + 1.0f, pz - 1.22f), new Vector3(hw - T - 0.62f, y + 1.3f, pz - 1.16f), 1f);
                // bathroom: a bath, a basin with a mirror, a toilet
                cx.Box(White, new Vector3(hw - T - 1.7f, y, D - T - 0.8f), new Vector3(hw - T, y + 0.58f, D - T), 1f);
                cx.Box(Water, new Vector3(hw - T - 1.6f, y + 0.4f, D - T - 0.7f), new Vector3(hw - T - 0.1f, y + 0.5f, D - T - 0.1f), 1f);
                cx.Box(White, new Vector3(inX0 + 1.2f, y + 0.75f, D - T - 0.45f), new Vector3(inX0 + 1.8f, y + 0.9f, D - T), 1f);
                cx.Box(M("mirror", () => Mats.Lit(new Color(0.8f, 0.85f, 0.9f), 0.98f, 1f)), new Vector3(inX0 + 1.15f, y + 1.15f, D - T - 0.02f), new Vector3(inX0 + 1.85f, y + 1.9f, D - T), 1f);
                cx.Box(White, new Vector3(inX0 + 2.3f, y, D - T - 0.65f), new Vector3(inX0 + 2.7f, y + 0.42f, D - T - 0.05f), 1f);
                cx.Box(White, new Vector3(inX0 + 2.3f, y + 0.42f, D - T - 0.2f), new Vector3(inX0 + 2.7f, y + 0.85f, D - T - 0.05f), 1f);
                if (owned && bed != null)
                {
                    var bp = root.TransformPoint(new Vector3((inX0 + inX1) / 2f + 0.4f, y, 2.8f));
                    points.Add(new WorldActions.Point { Pos = bp, Label = "СПАТЬ", Act = onSleep });
                    points.Add(new WorldActions.Point { Pos = root.TransformPoint(new Vector3(hw - T - 1.1f, y, pz - 1.2f)), Label = "ГАРДЕРОБ", Act = onWardrobe });
                }
            }
            if (owned && d.floors == 1)
            {   // a single-storey house: the bed sits in the back left corner
                Model("GothicBed_01", new Vector3(inX0 + 1.0f, y0, D - T - 1.2f), 180, 2.0f);
                points.Add(new WorldActions.Point { Pos = root.TransformPoint(new Vector3(inX0 + 1.0f, y0, D - T - 2.6f)), Label = "СПАТЬ", Act = onSleep });
                points.Add(new WorldActions.Point { Pos = root.TransformPoint(new Vector3(inX1 - 0.4f, y0, D * 0.45f)), Label = "ГАРДЕРОБ", Act = onWardrobe });
            }
            var mesh = cab.Build("Furniture", root, 12, true);
            mesh.transform.localPosition = Vector3.zero;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>The exterior + shell of a house into a MeshB, placed by xf (house space → mesh space).</summary>
        public static void Exterior(MeshB m, Matrix4x4 xf, HouseDesign d) => Shell(new Ctx { m = m, xf = xf }, d, null);

        /// <summary>
        /// The close-up part of a house (furniture, colliders, the front door leaf, action points) under
        /// a new object at the house origin. The shell itself is drawn by Exterior (merged elsewhere).
        /// </summary>
        public static GameObject Inside(Transform parent, Vector3 origin, Quaternion rot, HouseDesign d, bool owned, Action onSleep, Action onWardrobe, string doorLabel = null, Action doorAct = null)
        {
            var go = new GameObject("House " + d.name) { layer = 10 };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(origin, rot);
            var cols = new List<(Vector3, Vector3)>();
            colsRamp.Clear();
            Shell(new Ctx { m = new MeshB(), xf = Matrix4x4.identity }, d, cols);   // geometry thrown away: only the colliders are wanted
            foreach (var (a, b) in cols)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = (a + b) / 2f; bc.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
            }
            foreach (var (a, b, w) in colsRamp)
            {
                var r = new GameObject("Stairs") { layer = 10 };
                r.transform.SetParent(go.transform, false);
                var mid = (a + b) / 2f; var dir = b - a;
                r.transform.localPosition = mid - Vector3.up * 0.06f;
                r.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                var bc = r.AddComponent<BoxCollider>();
                bc.size = new Vector3(w, 0.1f, dir.magnitude);
            }
            var hp = go.AddComponent<WorldActions.Owner>();
            Furnish(go.transform, d, hp.points, owned, onSleep, onWardrobe);
            HouseDoor.Make(go.transform, new Vector3(d.doorX, 0.25f, -0.01f), d.door, 1.2f, 2.0f);
            if (d.balcony && d.floors > 1) HouseDoor.Make(go.transform, new Vector3(0f, d.FH + 0.05f, -0.01f), GlassTint(d), 1.8f, 2.25f, true);
            if (doorAct != null) hp.points.Add(new WorldActions.Point { Pos = go.transform.TransformPoint(new Vector3(d.doorX + 1.4f, 0f, -1.6f)), Label = doorLabel, Act = doorAct });
            WorldActions.Register(hp);
            return go;
        }

        static Color GlassTint(HouseDesign d) => new Color(0.55f, 0.7f, 0.78f, 0.3f);

        /// <summary>A whole house as one object (catalogue previews and player plots).</summary>
        public static GameObject Whole(Transform parent, Vector3 origin, Quaternion rot, HouseDesign d, int layer = 10)
        {
            var m = new MeshB();
            Exterior(m, Matrix4x4.identity, d);
            var go = m.Build("HouseShell " + d.name, parent, layer, true);
            go.transform.SetPositionAndRotation(origin, rot);
            return go;
        }
    }

    static class RngPick
    {
        public static T Pick<T>(this Rng r, T[] a) => a[r.I(a.Length)];
    }

    /// <summary>A hinged front door that swings inwards when someone comes close and closes behind them.</summary>
    public class HouseDoor : MonoBehaviour
    {
        Transform leaf;
        float open, width;
        bool glass;

        public static HouseDoor Make(Transform parent, Vector3 local, Color c, float w, float h, bool glassDoor = false)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local + new Vector3(-w / 2f, 0, -HouseBuilderT / 2f);
            var d = go.AddComponent<HouseDoor>();
            d.width = w; d.glass = glassDoor;
            d.leaf = new GameObject("Leaf").transform;
            d.leaf.SetParent(go.transform, false);
            var mat = glassDoor ? HouseBuilder.GlassM : Mats.Lit(c, 0.45f);
            var p = Prim.Box(d.leaf, new Vector3(w / 2f, h / 2f, 0), new Vector3(w, h, 0.05f), mat);
            if (!glassDoor)
            {
                Prim.Box(d.leaf, new Vector3(w / 2f, h * 0.7f, -0.03f), new Vector3(w * 0.6f, h * 0.35f, 0.02f), Mats.Lit(Color.Lerp(c, Color.white, 0.12f), 0.5f));
                Prim.Box(d.leaf, new Vector3(w / 2f, h * 0.27f, -0.03f), new Vector3(w * 0.6f, h * 0.35f, 0.02f), Mats.Lit(Color.Lerp(c, Color.white, 0.12f), 0.5f));
                Prim.Box(d.leaf, new Vector3(w - 0.12f, h * 0.48f, -0.05f), new Vector3(0.04f, 0.2f, 0.04f), Mats.Lit(new Color(0.85f, 0.75f, 0.4f), 0.85f, 1f));
            }
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            return d;
        }
        const float HouseBuilderT = 0.22f;

        void Update()
        {
            var pl = PlayerCtl.I;
            float want = 0f;
            if (pl != null)
            {
                var hinge = transform.position + transform.right * width / 2f;
                var dd = pl.transform.position - hinge; dd.y *= 0.4f;
                if (dd.sqrMagnitude < 2.6f * 2.6f) want = 1f;
            }
            open = Mathf.MoveTowards(open, want, Time.deltaTime * 1.6f);
            float e = open * open * (3f - 2f * open);
            leaf.localRotation = Quaternion.Euler(0, -95f * e, 0);
        }
    }

    /// <summary>Action points out in the world (in real houses and on plots), found by the player like interior points.</summary>
    public static class WorldActions
    {
        public class Point { public Vector3 Pos; public string Label; public Action Act; }
        public class Owner : MonoBehaviour
        {
            public readonly List<Point> points = new List<Point>();
            void OnDestroy() => owners.Remove(this);
        }
        static readonly List<Owner> owners = new List<Owner>();
        public static void Register(Owner o) { if (!owners.Contains(o)) owners.Add(o); }

        public static Point Nearest(Vector3 p, float max)
        {
            Point best = null; float bd = max * max;
            foreach (var o in owners)
            {
                if (o == null) continue;
                foreach (var a in o.points)
                {
                    var d = a.Pos - p; d.y *= 0.3f;
                    if (Mathf.Abs(a.Pos.y - p.y) > 1.6f) continue;
                    if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = a; }
                }
            }
            return best;
        }
    }
}
