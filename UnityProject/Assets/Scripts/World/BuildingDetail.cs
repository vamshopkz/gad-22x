using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Hundreds of small parts per building, streamed with the street detail around the player:
    /// cornices, plinths, 3D window sills, drainpipes, air-conditioner units, satellite dishes,
    /// shutters and flower boxes on houses, shop awnings, glass fins on towers and roof clutter
    /// (vents, AC units, antennas, water tanks, roof exits, railings, solar panels).
    /// Windows sit on the facade texture grid: bays of 3.5 m, floors of 3.3 m.
    /// </summary>
    public static class BuildingDetail
    {
        static Material sill, cornice, pipe, acBody, acGrille, dish, metal, awningMat0, awningMat1, awningMat2, shutter, flowers, soil, solar, rail, tank;

        static void Init()
        {
            if (sill != null) return;
            sill = Photo.Mat("concrete_wall_003", new Color(0.95f, 0.95f, 0.93f), 0.2f);
            cornice = Photo.Mat("white_plaster_02", new Color(1.0f, 0.98f, 0.95f), 0.15f);
            pipe = Mats.Lit(new Color(0.42f, 0.44f, 0.46f), 0.5f, 0.6f);
            acBody = Mats.Lit(new Color(0.88f, 0.88f, 0.86f), 0.35f, 0.1f);
            acGrille = Mats.Lit(new Color(0.18f, 0.19f, 0.2f), 0.4f, 0.5f);
            dish = Mats.Lit(new Color(0.85f, 0.86f, 0.87f), 0.4f, 0.2f);
            metal = Mats.Lit(new Color(0.55f, 0.57f, 0.6f), 0.55f, 0.8f);
            awningMat0 = Mats.Lit(new Color(0.75f, 0.12f, 0.12f), 0.2f); awningMat1 = Mats.Lit(new Color(0.12f, 0.4f, 0.25f), 0.2f); awningMat2 = Mats.Lit(new Color(0.15f, 0.2f, 0.4f), 0.2f);
            shutter = Mats.Lit(new Color(0.18f, 0.32f, 0.22f), 0.25f);
            flowers = Mats.Lit(new Color(0.85f, 0.25f, 0.4f), 0.1f);
            soil = Mats.Lit(new Color(0.25f, 0.45f, 0.18f), 0.1f);
            solar = Mats.Lit(new Color(0.06f, 0.1f, 0.22f), 0.9f, 0.4f);
            rail = Mats.Lit(new Color(0.3f, 0.31f, 0.33f), 0.5f, 0.7f);
            tank = Mats.Lit(new Color(0.7f, 0.72f, 0.74f), 0.5f, 0.6f);
        }

        static void Bx(MeshB m, Material mat, Vector3 a, Vector3 b) => m.Box(mat, mat, Vector3.Min(a, b), Vector3.Max(a, b), new Vector2(2, 2));

        /// <summary>Facade point: along = metres from where the facade texture starts, out = metres outside the wall.</summary>
        static Vector3 F(int face, float x0, float z0, float x1, float z1, float along, float y, float outw) => face switch
        {
            0 => new Vector3(x0 + along, y, z0 - outw),
            1 => new Vector3(x1 - along, y, z1 + outw),
            2 => new Vector3(x0 - outw, y, z1 - along),
            _ => new Vector3(x1 + outw, y, z0 + along),
        };

        static float FaceLen(int face, float x0, float z0, float x1, float z1) => face <= 1 ? x1 - x0 : z1 - z0;

        public static void Add(MeshB m, Bld b, Rng r)
        {
            Init();
            float x0 = b.rect.xMin, z0 = b.rect.yMin, x1 = b.rect.xMax, z1 = b.rect.yMax, y0 = City.Curb, h = b.height;
            float density = Gfx.Level == 0 ? 0.4f : Gfx.Level == 1 ? 0.75f : 1f;
            switch (b.type)
            {
                case BType.House: House(m, b, r, x0, z0, x1, z1, y0, h); Roof(m, b, r, x0, z0, x1, z1, y0 + h, density, true); return;
                case BType.Villa: Roof(m, b, r, x0 + 1, z0 + 1, x1 - 1, z1 - 1, y0 + 7.4f, density, true); return;
                case BType.Tower: case BType.Agency: Tower(m, b, r, x0, z0, x1, z1, y0, h); return;
            }
            bool commercial = b.Info.BizPrice > 0 && b.type != BType.Office && b.type != BType.Factory && b.type != BType.Warehouse && b.type != BType.Hotel && b.type != BType.Parking;
            bool residential = b.type == BType.Apartment || b.type == BType.Hotel;
            float fStart = commercial ? 4.4f : 0f;
            int floors = Mathf.Max(1, Mathf.FloorToInt((h - fStart) / 3.3f));
            for (int face = 0; face < 4; face++)
            {
                float len = FaceLen(face, x0, z0, x1, z1);
                int bays = Mathf.FloorToInt(len / 3.5f);
                // plinth and the top cornice
                Bx(m, sill, F(face, x0, z0, x1, z1, -0.1f, y0, 0f), F(face, x0, z0, x1, z1, len + 0.1f, y0 + 0.55f, 0.08f));
                Bx(m, cornice, F(face, x0, z0, x1, z1, -0.2f, y0 + h - 0.35f, 0f), F(face, x0, z0, x1, z1, len + 0.2f, y0 + h, 0.22f));
                // floor bands
                if (b.style == 1 || b.style == 2 || b.style == 8 || b.style == 9)
                    for (int f = 1; f < floors; f++)
                    {
                        float y = y0 + fStart + f * 3.3f;
                        Bx(m, cornice, F(face, x0, z0, x1, z1, 0, y - 0.06f, 0f), F(face, x0, z0, x1, z1, len, y + 0.06f, 0.06f));
                    }
                if (b.style == 5 || b.style == 3 || b.style == 4 && !commercial) continue;
                // window sills (3D) and things hanging under windows
                for (int f = 0; f < floors; f++)
                {
                    float fy = y0 + fStart + f * 3.3f;
                    if (commercial && f == 0 && fStart == 0) continue;
                    for (int k = 0; k < bays; k++)
                    {
                        float u = (k + 0.5f) * 3.5f;
                        float sy = fy + 0.72f;
                        Bx(m, sill, F(face, x0, z0, x1, z1, u - 0.95f, sy - 0.05f, 0f), F(face, x0, z0, x1, z1, u + 0.95f, sy + 0.02f, 0.14f));
                        if (r.F() > density) continue;
                        float roll = r.F();
                        if (residential && face != b.front && roll < 0.22f) AC(m, face, x0, z0, x1, z1, u + 1.25f, fy + 0.35f);
                        else if (residential && roll < 0.27f) Dish(m, face, x0, z0, x1, z1, u - 1.3f, fy + 1.9f);
                        else if (b.type == BType.Hotel && face == b.front && roll < 0.5f && f > 0)
                            Bx(m, metal, F(face, x0, z0, x1, z1, u - 1.0f, fy + 0.0f, 0f), F(face, x0, z0, x1, z1, u + 1.0f, fy + 0.08f, 0.6f));
                    }
                }
                // drainpipes at the corners of the face
                if (residential || b.style == 2 || b.style == 8)
                    foreach (float u in new[] { 0.25f, len - 0.25f })
                    {
                        var p0 = F(face, x0, z0, x1, z1, u, y0, 0.14f);
                        m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p0 + Vector3.up * h / 2, Quaternion.identity, new Vector3(0.12f, h / 2, 0.12f)), pipe);
                        for (float yy = y0 + 1.5f; yy < y0 + h; yy += 3.3f)
                            Bx(m, pipe, F(face, x0, z0, x1, z1, u - 0.09f, yy, 0f), F(face, x0, z0, x1, z1, u + 0.09f, yy + 0.06f, 0.2f));
                    }
                // shop awnings over each storefront bay on the street side
                if (commercial && face == b.front && h > 3)
                {
                    var am = (b.id % 3) switch { 0 => awningMat0, 1 => awningMat1, _ => awningMat2 };
                    for (int k = 0; k < bays; k++)
                    {
                        float u = (k + 0.5f) * 3.5f;
                        if (Mathf.Abs(u - len / 2) < 2f) continue;   // the door has its own canopy
                        var a = F(face, x0, z0, x1, z1, u - 1.6f, y0 + 3.0f, 0f);
                        var c = F(face, x0, z0, x1, z1, u + 1.6f, y0 + 3.0f, 0f);
                        var outv = F(face, x0, z0, x1, z1, 0, 0, 1) - F(face, x0, z0, x1, z1, 0, 0, 0);
                        var rot = Quaternion.LookRotation(outv) * Quaternion.Euler(-28, 0, 0);
                        m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS((a + c) / 2 + outv * 0.55f - Vector3.up * 0.25f, rot, new Vector3(3.2f, 0.05f, 1.25f)), am);
                        Bx(m, am, a + outv * 1.08f + Vector3.down * 0.8f, c + outv * 1.12f + Vector3.down * 0.55f);
                    }
                }
            }
            Roof(m, b, r, x0, z0, x1, z1, y0 + h, density, false);
        }

        static void AC(MeshB m, int face, float x0, float z0, float x1, float z1, float u, float y)
        {
            Bx(m, acBody, F(face, x0, z0, x1, z1, u - 0.42f, y, 0.02f), F(face, x0, z0, x1, z1, u + 0.42f, y + 0.55f, 0.32f));
            var c = F(face, x0, z0, x1, z1, u - 0.1f, y + 0.275f, 0.33f);
            var outv = (F(face, x0, z0, x1, z1, 0, 0, 1) - F(face, x0, z0, x1, z1, 0, 0, 0)).normalized;
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c, Quaternion.FromToRotation(Vector3.up, outv), new Vector3(0.4f, 0.01f, 0.4f)), acGrille);
            for (int i = -1; i <= 1; i += 2)
                Bx(m, metal, F(face, x0, z0, x1, z1, u + i * 0.36f - 0.02f, y - 0.06f, 0f), F(face, x0, z0, x1, z1, u + i * 0.36f + 0.02f, y, 0.34f));
        }

        static void Dish(MeshB m, int face, float x0, float z0, float x1, float z1, float u, float y)
        {
            var outv = (F(face, x0, z0, x1, z1, 0, 0, 1) - F(face, x0, z0, x1, z1, 0, 0, 0)).normalized;
            var c = F(face, x0, z0, x1, z1, u, y, 0.35f);
            m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(c, Quaternion.LookRotation(outv + Vector3.up * 0.4f), new Vector3(0.6f, 0.6f, 0.12f)), dish);
            Bx(m, metal, F(face, x0, z0, x1, z1, u - 0.03f, y - 0.03f, 0f), F(face, x0, z0, x1, z1, u + 0.03f, y + 0.03f, 0.35f));
        }

        static void House(MeshB m, Bld b, Rng r, float x0, float z0, float x1, float z1, float y0, float h)
        {
            int floors = Mathf.Max(1, Mathf.RoundToInt(h / 3.3f));
            for (int face = 0; face < 4; face++)
            {
                float len = FaceLen(face, x0, z0, x1, z1);
                int bays = Mathf.FloorToInt(len / 3.5f);
                Bx(m, sill, F(face, x0, z0, x1, z1, -0.1f, y0, 0f), F(face, x0, z0, x1, z1, len + 0.1f, y0 + 0.45f, 0.06f));
                for (int f = 0; f < floors; f++)
                    for (int k = 0; k < bays; k++)
                    {
                        float u = (k + 0.5f) * 3.5f, fy = y0 + f * 3.3f;
                        if (face == b.front && f == 0 && Mathf.Abs(u - len / 2) < 1.8f) continue;
                        // shutters either side of the window
                        foreach (float s in new[] { -1f, 1f })
                            Bx(m, shutter, F(face, x0, z0, x1, z1, u + s * 0.88f - 0.28f, fy + 0.75f, 0.01f), F(face, x0, z0, x1, z1, u + s * 0.88f + 0.28f, fy + 2.55f, 0.06f));
                        Bx(m, sill, F(face, x0, z0, x1, z1, u - 0.9f, fy + 0.67f, 0f), F(face, x0, z0, x1, z1, u + 0.9f, fy + 0.74f, 0.14f));
                        if (face == b.front)
                        {
                            Bx(m, WorldBuilder.Wood, F(face, x0, z0, x1, z1, u - 0.75f, fy + 0.45f, 0.14f), F(face, x0, z0, x1, z1, u + 0.75f, fy + 0.66f, 0.36f));
                            Bx(m, soil, F(face, x0, z0, x1, z1, u - 0.72f, fy + 0.66f, 0.16f), F(face, x0, z0, x1, z1, u + 0.72f, fy + 0.78f, 0.34f));
                            for (int fl = 0; fl < 5; fl++)
                                m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(F(face, x0, z0, x1, z1, u - 0.6f + fl * 0.3f, fy + 0.82f, 0.25f), Quaternion.identity, Vector3.one * 0.14f), flowers);
                        }
                    }
                // gutter along the eaves on the long sides
                if (face <= 1)
                {
                    var a = F(face, x0, z0, x1, z1, -0.5f, y0 + h - 0.4f, 0.55f);
                    var c = F(face, x0, z0, x1, z1, len + 0.5f, y0 + h - 0.4f, 0.55f);
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS((a + c) / 2, Quaternion.Euler(0, 0, 90), new Vector3(0.14f, (len + 1) / 2, 0.14f)), pipe);
                    var dp = F(face, x0, z0, x1, z1, len - 0.2f, y0 + (h - 0.4f) / 2, 0.45f);
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(dp, Quaternion.identity, new Vector3(0.1f, (h - 0.4f) / 2, 0.1f)), pipe);
                }
            }
            // porch lamp and mailbox
            var d = b.door; var fd = b.FrontDir; var sd = Vector3.Cross(Vector3.up, fd);
            Bx(m, WorldBuilder.LampGlow, d + fd * 0.08f + sd * 1.2f + Vector3.up * 2.0f, d + fd * 0.25f + sd * 1.35f + Vector3.up * 2.3f);
            var mb = d + fd * 5.5f - sd * 1.8f;
            Bx(m, metal, mb + new Vector3(-0.04f, 0, -0.04f), mb + new Vector3(0.04f, 1.0f, 0.04f));
            Bx(m, awningMat2, mb + new Vector3(-0.2f, 1.0f, -0.25f), mb + new Vector3(0.2f, 1.3f, 0.25f));
        }

        static void Tower(MeshB m, Bld b, Rng r, float x0, float z0, float x1, float z1, float y0, float h)
        {
            float hl = h * (b.type == BType.Agency ? 0.8f : 0.72f);
            var fin = Mats.Lit(new Color(0.75f, 0.78f, 0.82f), 0.8f, 0.9f);
            for (int face = 0; face < 4; face++)
            {
                float len = FaceLen(face, x0, z0, x1, z1);
                int bays = Mathf.FloorToInt(len / 3.5f);
                for (int k = 0; k <= bays; k++)
                {
                    float u = k * 3.5f;
                    if (u > len) break;
                    Bx(m, fin, F(face, x0, z0, x1, z1, u - 0.06f, y0 + 4f, 0f), F(face, x0, z0, x1, z1, u + 0.06f, y0 + hl, 0.35f));
                }
                // lobby canopy band
                Bx(m, fin, F(face, x0, z0, x1, z1, -0.3f, y0 + 4.2f, 0f), F(face, x0, z0, x1, z1, len + 0.3f, y0 + 4.5f, 1.6f));
            }
            float ins = Mathf.Min(3f, (x1 - x0) * 0.15f);
            Roof(m, b, r, x0 + ins, z0 + ins, x1 - ins, z1 - ins, y0 + h, 1f, false);
            // window-cleaning crane arm on the roof
            var cpos = new Vector3(x0 + ins + 2, y0 + h, z0 + ins + 2);
            Bx(m, metal, cpos, cpos + new Vector3(0.4f, 3.5f, 0.4f));
            Bx(m, metal, cpos + new Vector3(0, 3.3f, 0), cpos + new Vector3(0.3f, 3.5f, -5f));
        }

        /// <summary>Roof clutter: vents, AC units, antennas, a water tank, a roof exit, a railing, solar panels.</summary>
        static void Roof(MeshB m, Bld b, Rng r, float x0, float z0, float x1, float z1, float y, float density, bool pitched)
        {
            float w = x1 - x0, d = z1 - z0;
            if (pitched)
            {
                if (b.type == BType.Villa)
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3(x0 + 1.5f + (i % 3) * 1.8f, y + 0.4f, z0 + 1.5f + (i / 3) * 1.3f);
                        m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p, Quaternion.Euler(-22, 0, 0), new Vector3(1.6f, 0.05f, 1.1f)), solar);
                    }
                // TV antenna
                var ap = new Vector3(x0 + w * 0.3f, y + 1.8f, (z0 + z1) / 2);
                Bx(m, metal, ap - new Vector3(0.03f, 1.6f, 0.03f), ap + new Vector3(0.03f, 0.8f, 0.03f));
                for (int i = 0; i < 4; i++) Bx(m, metal, ap + new Vector3(-0.5f, i * 0.18f, -0.02f), ap + new Vector3(0.5f, i * 0.18f + 0.03f, 0.02f));
                return;
            }
            int n = Mathf.Clamp(Mathf.RoundToInt(w * d / 40f * density), 3, 28);
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(r.F(x0 + 1.2f, x1 - 1.2f), y, r.F(z0 + 1.2f, z1 - 1.2f));
                switch (r.I(5))
                {
                    case 0: // AC unit with a fan on top
                        Bx(m, acBody, p + new Vector3(-0.7f, 0, -0.5f), p + new Vector3(0.7f, 1.0f, 0.5f));
                        m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + Vector3.up * 1.01f, Quaternion.identity, new Vector3(0.8f, 0.01f, 0.8f)), acGrille);
                        break;
                    case 1: // vent pipe with a cap
                        m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + Vector3.up * 0.6f, Quaternion.identity, new Vector3(0.3f, 0.6f, 0.3f)), metal);
                        m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + Vector3.up * 1.25f, Quaternion.identity, new Vector3(0.5f, 0.05f, 0.5f)), metal);
                        break;
                    case 2: // box vent
                        Bx(m, metal, p + new Vector3(-0.4f, 0, -0.4f), p + new Vector3(0.4f, 0.5f, 0.4f));
                        Bx(m, acGrille, p + new Vector3(-0.42f, 0.2f, -0.42f), p + new Vector3(0.42f, 0.35f, 0.42f));
                        break;
                    case 3: // satellite dish on a stand
                        Bx(m, metal, p + new Vector3(-0.04f, 0, -0.04f), p + new Vector3(0.04f, 0.9f, 0.04f));
                        m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(p + Vector3.up * 1.0f, Quaternion.Euler(-30, r.F(0, 360), 0), new Vector3(0.9f, 0.9f, 0.15f)), dish);
                        break;
                    default: // antenna mast
                        Bx(m, metal, p + new Vector3(-0.03f, 0, -0.03f), p + new Vector3(0.03f, 3.5f, 0.03f));
                        Bx(m, metal, p + new Vector3(-0.4f, 2.8f, -0.02f), p + new Vector3(0.4f, 2.84f, 0.02f));
                        break;
                }
            }
            if (w * d > 120)
            {
                // water tank on legs
                var t = new Vector3(x1 - 3f, y, z1 - 3f);
                for (int i = 0; i < 4; i++) Bx(m, metal, t + new Vector3((i % 2) * 1.4f - 0.75f, 0, (i / 2) * 1.4f - 0.75f), t + new Vector3((i % 2) * 1.4f - 0.65f, 1.6f, (i / 2) * 1.4f - 0.65f));
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(t + Vector3.up * 2.6f, Quaternion.identity, new Vector3(1.8f, 1.0f, 1.8f)), tank);
                // roof exit hut
                var e = new Vector3(x0 + 2f, y, z0 + 2f);
                Bx(m, cornice, e, e + new Vector3(2.4f, 2.4f, 2.0f));
                Bx(m, metal, e + new Vector3(0.7f, 0, -0.02f), e + new Vector3(1.6f, 2.0f, 0.02f));
            }
            // railing round the edge
            for (float x = x0 + 0.3f; x < x1; x += 1.5f) { Bx(m, rail, new Vector3(x, y, z0 + 0.3f), new Vector3(x + 0.04f, y + 1.0f, z0 + 0.34f)); Bx(m, rail, new Vector3(x, y, z1 - 0.34f), new Vector3(x + 0.04f, y + 1.0f, z1 - 0.3f)); }
            Bx(m, rail, new Vector3(x0 + 0.3f, y + 0.98f, z0 + 0.3f), new Vector3(x1 - 0.3f, y + 1.03f, z0 + 0.34f));
            Bx(m, rail, new Vector3(x0 + 0.3f, y + 0.98f, z1 - 0.34f), new Vector3(x1 - 0.3f, y + 1.03f, z1 - 0.3f));
        }
    }
}
