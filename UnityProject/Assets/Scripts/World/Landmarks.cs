using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 1.0: the city modules from the planning boards, under the same names: «Бизнес-парк Глобаль»,
    /// «Высотный микст-квартал», «Логистический центр», «Мультимодальный хаб», «Маршрутный узел и
    /// отели», «Озеро и пристань», «Окончательный жилой квартал». City.BuildBlock lays out their
    /// buildings; here is what goes round them (courtyards, solar roofs, buses, trams, trains, trucks,
    /// ramps, the lake and its pier) and a tall name stele for each, also shown on the map and offered
    /// by the taxi.
    /// </summary>
    public static class Landmarks
    {
        public class Mark { public string name; public Vector3 pos; public Color color; }
        public static readonly List<Mark> All = new List<Mark>();

        public const string Globe = "Бизнес-парк «Глобаль»", Mixed = "Высотный микст-квартал", Logistics = "Логистический центр",
            Hub = "Мультимодальный хаб", Hotels = "Маршрутный узел и отели", Lake = "Озеро и пристань", Homes = "Окончательный жилой квартал";

        /// <summary>The blocks the modules take (block x, z).</summary>
        public static readonly (int x, int z) GlobeBlock = (9, 6), MixedBlock = (10, 8), LogisticsBlock = (1, 3), HotelsBlock = (2, 7), LakeBlock = (3, 9), HubBlock = (1, 7);

        public static bool Is((int, int) block, int x, int z) => block.Item1 == x && block.Item2 == z;

        static Vector3 BlockCentre((int x, int z) b) => new Vector3((b.x + 0.5f) * City.P, City.Curb, (b.z + 0.5f) * City.P);

        static Material M(Color c, float s = 0.4f, float metal = 0f) => Mats.Lit(c, s, metal);

        // ------------------------------------------------------------------ vehicles made of parts

        /// <summary>A city bus (12 m), along +Z from c.</summary>
        static void Bus(MeshB m, Vector3 c, float yaw, Color paint)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            void B(Vector3 lo, Vector3 hi, Material mat) => m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + rot * ((lo + hi) / 2), rot, hi - lo), mat);
            var body = M(paint, 0.7f, 0.2f); var glass = M(new Color(0.08f, 0.12f, 0.16f), 0.95f); var dark = M(new Color(0.06f, 0.06f, 0.06f), 0.3f);
            B(new Vector3(-1.25f, 0.45f, -6f), new Vector3(1.25f, 3.1f, 6f), body);
            B(new Vector3(-1.27f, 1.35f, -5.6f), new Vector3(1.27f, 2.6f, 5.4f), glass);
            B(new Vector3(-1.2f, 0.9f, 5.98f), new Vector3(1.2f, 2.8f, 6.02f), glass);
            B(new Vector3(-1.2f, 3.1f, -4f), new Vector3(1.2f, 3.45f, 2f), M(new Color(0.85f, 0.85f, 0.87f)));
            foreach (float z in new[] { -4f, 3.8f })
                foreach (float x in new[] { -1.15f, 1.15f })
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + rot * new Vector3(x, 0.5f, z), rot * Quaternion.Euler(0, 0, 90), new Vector3(1f, 0.15f, 1f)), dark);
            B(new Vector3(-0.9f, 2.75f, 6.02f), new Vector3(0.9f, 3.0f, 6.05f), Mats.Glow(new Color(1f, 0.6f, 0.1f)));
        }

        /// <summary>A tram (two linked cars) or a suburban train car on rails.</summary>
        static void Railcar(MeshB m, Vector3 c, float yaw, Color paint, float len, float height)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            void B(Vector3 lo, Vector3 hi, Material mat) => m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + rot * ((lo + hi) / 2), rot, hi - lo), mat);
            var body = M(paint, 0.75f, 0.25f); var glass = M(new Color(0.07f, 0.1f, 0.14f), 0.95f);
            B(new Vector3(-1.35f, 0.5f, -len / 2), new Vector3(1.35f, height, len / 2), body);
            B(new Vector3(-1.37f, height * 0.42f, -len / 2 + 0.6f), new Vector3(1.37f, height * 0.8f, len / 2 - 0.6f), glass);
            B(new Vector3(-1.3f, 0.5f, -len / 2), new Vector3(1.3f, 0.75f, len / 2), M(new Color(0.15f, 0.15f, 0.16f)));
            B(new Vector3(-0.2f, height, -1f), new Vector3(0.2f, height + 0.9f, 1f), M(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.8f));
        }

        /// <summary>A lorry: cab and a long trailer box.</summary>
        static void Truck(MeshB m, Vector3 c, float yaw, Color cab, Color box)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            void B(Vector3 lo, Vector3 hi, Material mat) => m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + rot * ((lo + hi) / 2), rot, hi - lo), mat);
            B(new Vector3(-1.25f, 0.6f, 5.2f), new Vector3(1.25f, 3.3f, 7.6f), M(cab, 0.6f, 0.3f));
            B(new Vector3(-1.2f, 2.1f, 7.58f), new Vector3(1.2f, 3.0f, 7.62f), M(new Color(0.08f, 0.1f, 0.13f), 0.95f));
            B(new Vector3(-1.3f, 0.9f, -6.6f), new Vector3(1.3f, 4f, 5f), M(box, 0.3f));
            var dark = M(new Color(0.05f, 0.05f, 0.05f), 0.3f);
            foreach (float z in new[] { -5.4f, -4.2f, 6.6f })
                foreach (float x in new[] { -1.15f, 1.15f })
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + rot * new Vector3(x, 0.55f, z), rot * Quaternion.Euler(0, 0, 90), new Vector3(1.1f, 0.18f, 1.1f)), dark);
        }

        // ------------------------------------------------------------------ the modules

        public static void Build(MeshB m)
        {
            All.Clear();
            float y = City.Curb + 0.02f;
            var plaza = WorldBuilder.Plaza; var grass = WorldBuilder.Grass; var asphalt = WorldBuilder.Asphalt; var paint = WorldBuilder.Paint;

            // Бизнес-парк «Глобаль»: a paved courtyard with a fountain and trees between the four glass blocks, buses at the stop
            {
                var c = BlockCentre(GlobeBlock);
                m.Flat(plaza, c.x - 6, c.z - 6, c.x + 6, c.z + 6, y + 0.01f, 3f);
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 0.3f, Quaternion.identity, new Vector3(5f, 0.3f, 5f)), M(new Color(0.8f, 0.8f, 0.82f)));
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 0.62f, Quaternion.identity, new Vector3(4.4f, 0.02f, 4.4f)), M(new Color(0.2f, 0.5f, 0.75f), 0.95f));
                for (int i = 0; i < 4; i++) Tree(m, c + Quaternion.Euler(0, 45 + i * 90, 0) * Vector3.forward * 4.6f);
                Bus(m, new Vector3(GlobeBlock.x * City.P + 3.2f, 0.02f, c.z - 10f), 0, new Color(0.15f, 0.45f, 0.85f));
                Bus(m, new Vector3(GlobeBlock.x * City.P + 3.2f, 0.02f, c.z + 8f), 0, new Color(0.15f, 0.45f, 0.85f));
                AddMark(Globe, c + new Vector3(-8, 0, -8), new Color(0.2f, 0.6f, 1f));
            }
            // Высотный микст-квартал: a courtyard with a playground and trees
            {
                var c = BlockCentre(MixedBlock);
                m.Flat(grass, c.x - 7, c.z - 7, c.x + 7, c.z + 7, y + 0.01f, 3f);
                m.Flat(M(new Color(0.75f, 0.35f, 0.2f), 0.1f), c.x - 3, c.z - 3, c.x + 3, c.z + 3, y + 0.02f, 2f);
                var red = M(new Color(0.85f, 0.2f, 0.15f)); var yel = M(new Color(0.95f, 0.8f, 0.15f));
                m.Box(red, red, c + new Vector3(-1.5f, 0, -0.6f), c + new Vector3(-1.3f, 2.2f, 0.6f), new Vector2(1, 1));
                m.Box(red, red, c + new Vector3(1.3f, 0, -0.6f), c + new Vector3(1.5f, 2.2f, 0.6f), new Vector2(1, 1));
                m.Box(yel, yel, c + new Vector3(-1.5f, 2.2f, -0.1f), c + new Vector3(1.5f, 2.35f, 0.1f), new Vector2(1, 1));
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + new Vector3(2.2f, 0.8f, 1.8f), Quaternion.Euler(0, 0, 30), new Vector3(2.4f, 0.1f, 0.6f)), yel);
                for (int i = 0; i < 6; i++) Tree(m, c + Quaternion.Euler(0, i * 60, 0) * Vector3.forward * 5.8f);
                AddMark(Mixed, c + new Vector3(-8, 0, -8), new Color(0.95f, 0.55f, 0.2f));
            }
            // Логистический центр: loading docks with lorries, a curved ramp road up to the upper deck, a yard full of trailers
            {
                var c = BlockCentre(LogisticsBlock);
                float x0 = LogisticsBlock.x * City.P + City.Walk, z1 = LogisticsBlock.z * City.P + City.P - City.Walk;
                m.Flat(asphalt, x0, c.z - 2, x0 + 60, c.z + 6, y + 0.01f, 6f);
                for (int i = 0; i < 6; i++)
                    Truck(m, new Vector3(x0 + 6 + i * 8.5f, 0.02f, c.z + 1.8f), 180, i % 2 == 0 ? new Color(0.85f, 0.2f, 0.15f) : new Color(0.15f, 0.35f, 0.7f), new Color(0.92f, 0.92f, 0.9f));
                // the ramp: a curved concrete road rising to the roof deck of the main warehouse
                var deck = WorldBuilder.Concrete;
                for (int k = 0; k < 24; k++)
                {
                    float a0 = k / 24f * Mathf.PI, a1 = (k + 1) / 24f * Mathf.PI;
                    var cc = new Vector3(x0 + 56, 0, c.z - 16);
                    float h0 = k / 24f * 11f, h1 = (k + 1) / 24f * 11f;
                    Vector3 P(float a, float r, float h) => cc + new Vector3(Mathf.Cos(a) * r, h + 0.2f, Mathf.Sin(a) * r);
                    m.Quad(deck, P(a0, 6f, h0), P(a1, 6f, h1), P(a1, 12f, h1), P(a0, 12f, h0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    m.Quad(deck, P(a0, 12f, h0 - 0.4f), P(a1, 12f, h1 - 0.4f), P(a1, 6f, h1 - 0.4f), P(a0, 6f, h0 - 0.4f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    if (k % 4 == 0) m.Box(deck, deck, P(a0, 9f, 0) - new Vector3(0.3f, 0.2f, 0.3f), P(a0, 9f, h0) + new Vector3(0.3f, 0, 0.3f), new Vector2(2, 2));
                }
                AddMark(Logistics, new Vector3(x0 + 2, y, c.z + 8), new Color(0.95f, 0.75f, 0.2f));
            }
            // Мультимодальный хаб: the station with suburban trains at the platform, a bus station and a tram stop behind it
            {
                var c = BlockCentre(HubBlock);
                float x0 = HubBlock.x * City.P + City.Walk;
                float zb = HubBlock.z * City.P + City.P - City.Walk - 12f;
                m.Flat(asphalt, x0, zb - 4, x0 + 60, zb + 12, y + 0.01f, 6f);
                for (int i = 0; i < 4; i++)
                {
                    m.Box(plaza, plaza, new Vector3(x0 + 4 + i * 14, y, zb + 1), new Vector3(x0 + 7 + i * 14, y + 0.15f, zb + 9), new Vector2(2, 2));
                    var roof = M(new Color(0.9f, 0.92f, 0.95f), 0.6f, 0.4f);
                    m.Box(roof, roof, new Vector3(x0 + 3.5f + i * 14, y + 3.2f, zb + 0.5f), new Vector3(x0 + 7.5f + i * 14, y + 3.4f, zb + 9.5f), new Vector2(2, 2));
                    Bus(m, new Vector3(x0 + 10.5f + i * 14, 0.02f, zb + 5), 0, i % 2 == 0 ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.9f, 0.5f, 0.1f));
                }
                // a tram on rails along the street in front
                float tz = HubBlock.z * City.P + 2.5f;
                m.Box(WorldBuilder.Metal, WorldBuilder.Metal, new Vector3(HubBlock.x * City.P - 40, 0.01f, tz - 0.8f), new Vector3(HubBlock.x * City.P + 120, 0.05f, tz - 0.7f), new Vector2(1, 1));
                m.Box(WorldBuilder.Metal, WorldBuilder.Metal, new Vector3(HubBlock.x * City.P - 40, 0.01f, tz + 0.7f), new Vector3(HubBlock.x * City.P + 120, 0.05f, tz + 0.8f), new Vector2(1, 1));
                Railcar(m, new Vector3(c.x - 8, 0.02f, tz), 90, new Color(0.85f, 0.15f, 0.2f), 14f, 3.4f);
                Railcar(m, new Vector3(c.x + 7, 0.02f, tz), 90, new Color(0.85f, 0.15f, 0.2f), 14f, 3.4f);
                // a second train (an electric suburban one) on the railway beside the platform
                for (int i = 0; i < 4; i++) Railcar(m, new Vector3(-45f, 0.35f, 7 * City.P + 86 + i * 21), 0, new Color(0.15f, 0.5f, 0.3f), 20f, 4.2f);
                AddMark(Hub, c + new Vector3(0, 0, -22), new Color(0.3f, 0.95f, 0.6f));
            }
            // Маршрутный узел и отели: the two hotels by the hub get a taxi rank in front
            {
                var c = BlockCentre(HotelsBlock);
                float zf = HotelsBlock.z * City.P + City.Walk;
                for (int i = 0; i < 3; i++)
                {
                    var def = CarDef.Get("taxi");
                    m.AddMulti(CarMesh.Static(def, 1), Matrix4x4.TRS(new Vector3(c.x - 8 + i * 5.5f, City.Curb, zf - 3.2f), Quaternion.Euler(0, 90, 0), Vector3.one), CarMesh.Materials(Car.ColorOf(def, 5), def.Spec));
                }
                AddMark(Hotels, c + new Vector3(0, 0, -26), new Color(0.95f, 0.8f, 0.35f));
            }
            // Озеро и пристань: the park block becomes a square lake with a stone embankment, a pier and boats
            {
                var c = BlockCentre(LakeBlock);
                float h = 27f;
                var stone = Photo.Mat("granite_tile", new Color(0.8f, 0.8f, 0.78f), 0.3f);
                m.Box(stone, stone, new Vector3(c.x - h - 1, 0, c.z - h - 1), new Vector3(c.x + h + 1, City.Curb + 0.25f, c.z + h + 1), new Vector2(2, 2));
                m.Flat(M(new Color(0.12f, 0.38f, 0.55f), 0.96f), c.x - h, c.z - h, c.x + h, c.z + h, City.Curb + 0.27f, 6f);
                var deck = Photo.Mat("wood_floor_deck", Color.white, 0.2f);
                m.Box(deck, deck, new Vector3(c.x - 1.5f, City.Curb + 0.25f, c.z - h - 1), new Vector3(c.x + 1.5f, City.Curb + 0.55f, c.z - 4), new Vector2(2, 2));
                m.Box(deck, deck, new Vector3(c.x - 6, City.Curb + 0.25f, c.z - 7), new Vector3(c.x + 6, City.Curb + 0.55f, c.z - 4), new Vector2(2, 2));
                var hull = M(new Color(0.95f, 0.95f, 0.95f), 0.7f);
                foreach (var (bx, bz, by) in new[] { (-4.5f, -9.5f, 20f), (4.5f, -9.5f, -15f), (12f, 6f, 70f) })
                {
                    var bp = c + new Vector3(bx, 0.2f, bz);
                    m.Add(Prim.MeshOf(PrimitiveType.Capsule), Matrix4x4.TRS(bp, Quaternion.Euler(90, by, 0), new Vector3(1.4f, 2.4f, 0.6f)), hull);
                    m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(bp + Vector3.up * 0.5f, Quaternion.Euler(0, by, 0), new Vector3(0.9f, 0.6f, 1.2f)), M(new Color(0.15f, 0.35f, 0.6f)));
                }
                AddMark(Lake, c + new Vector3(-h + 2, 0, -h - 3), new Color(0.35f, 0.75f, 1f));
            }
            // Окончательный жилой квартал: the suburb of small houses
            AddMark(Homes, new Vector3(3 * City.P + 3, City.Curb, (City.N - 2) * City.P + 3), new Color(0.5f, 0.9f, 0.4f));
        }

        static void Tree(MeshB m, Vector3 p)
        {
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + Vector3.up * 1.4f, Quaternion.identity, new Vector3(0.25f, 1.4f, 0.25f)), M(new Color(0.35f, 0.24f, 0.16f)));
            m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(p + Vector3.up * 3.6f, Quaternion.identity, new Vector3(2.8f, 2.6f, 2.8f)), M(new Color(0.2f, 0.45f, 0.2f)));
        }

        static void AddMark(string name, Vector3 at, Color c) => All.Add(new Mark { name = name, pos = at, color = c });

        /// <summary>A tall stele with the module's name for each mark (world text, readable from both sides).</summary>
        public static void Signs(Transform parent)
        {
            foreach (var mk in All)
            {
                var go = new GameObject("Stele " + mk.name);
                go.transform.SetParent(parent, false);
                go.transform.position = mk.pos;
                Prim.Box(go.transform, new Vector3(0, 2.6f, 0), new Vector3(4.6f, 5.2f, 0.5f), Mats.Lit(new Color(0.12f, 0.13f, 0.16f), 0.6f));
                Prim.Box(go.transform, new Vector3(0, 5.25f, 0), new Vector3(4.8f, 0.1f, 0.6f), Mats.Glow(mk.color));
                foreach (float side in new[] { 1f, -1f })
                {
                    var t = new GameObject("Name").AddComponent<TextMeshPro>();
                    t.transform.SetParent(go.transform, false);
                    t.transform.localPosition = new Vector3(0, 2.9f, side * -0.27f);
                    t.transform.localRotation = Quaternion.Euler(0, side > 0 ? 0 : 180, 0);
                    t.font = UIKit.Font; t.text = mk.name.ToUpper(); t.fontSize = 3.2f; t.color = mk.color;
                    t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.Normal;
                    t.rectTransform.sizeDelta = new Vector2(4.2f, 4.6f);
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
        }
    }
}
