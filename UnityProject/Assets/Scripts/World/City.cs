using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>One building in the world. Every building can be entered (see Interiors).</summary>
    public class Bld
    {
        public int id;
        public BType type;
        public District district;
        public Rect rect;            // footprint (x, z)
        public float height;
        public int floors;
        public int style;            // facade style (Tex.Facade)
        public int front;            // 0 south(-z) 1 north(+z) 2 west(-x) 3 east(+x)
        public Vector3 door;         // door position at ground (outside)
        public string name, address;
        public int cx, cz;           // chunk (block); -1 for outskirts
        public bool roofHelipad;

        public BInfo Info => BInfo.All[type];
        public string Title => Info.Title;
        public Vector3 FrontDir => front switch { 0 => Vector3.back, 1 => Vector3.forward, 2 => Vector3.left, _ => Vector3.right };
        /// <summary>Where a player stands after leaving the building.</summary>
        public Vector3 ExitPoint => door + FrontDir * 2.2f + Vector3.up * 0.2f;
        public bool IsHome => Info.HomePrice > 0;
        public int Units => type == BType.Apartment ? Mathf.Max(4, floors * 4) : type == BType.Tower ? Mathf.Max(4, floors / 4) : 1;

        public int UnitPrice(int unit)
        {
            int p = Info.HomePrice;
            if (type == BType.Apartment) p += floors * 1200 + (int)(rect.width * rect.height * 4);
            if (type == BType.Villa || type == BType.House) p += (int)(rect.width * rect.height * 30);
            if (district == District.Downtown || district == District.Beach) p = p * 3 / 2;
            int floor = unit / 10;
            if (unit > 0 && type == BType.Tower) p = p / 3 + floor * 12000 + (unit % 10) * 1500;
            if (unit > 0 && type == BType.Apartment) p += floor * 1500 + (unit % 10) * 300;
            return p / 100 * 100;
        }

        public int BizPrice
        {
            get
            {
                int p = Info.BizPrice;
                if (p <= 0) return 0;
                p += (int)(rect.width * rect.height * 12);
                if (district == District.Downtown) p = p * 3 / 2;
                return p / 1000 * 1000;
            }
        }
    }

    /// <summary>
    /// The city layout, generated deterministically (same on every device): a grid of N×N blocks
    /// of P metres with roads on the grid lines, districts, lots and buildings, plus outskirts
    /// (beach and sea in the south, mountains with a tunnel in the north, the airport in the east).
    /// </summary>
    public static class City
    {
        public const float P = 80f;        // block pitch
        public const float Road = 6f;      // half width of the asphalt
        public const float Walk = 10f;     // block interior starts this far from the grid line
        public const float Curb = 0.15f;   // sidewalk height
        public const int N = 14;           // blocks per side
        public const float Size = N * P;
        public const float SeaZ = -150f;   // beach from 0 down to here, then sea
        public const float AirportX0 = Size + 60f, AirportX1 = Size + 760f;
        public const float AirportZ0 = 300f, AirportZ1 = 820f;
        public const float NorthRoadX = 7 * P;
        public const float MountainZ = Size + 220f;
        public const float TunnelZ0 = Size + 300f, TunnelZ1 = Size + 470f;

        public static readonly List<Bld> All = new List<Bld>();
        public static readonly List<Bld>[,] ByChunk = new List<Bld>[N, N];
        public static readonly List<Bld> Outskirts = new List<Bld>();
        public static District[,] Districts = new District[N, N];
        public static Vector3 Spawn;

        static readonly string[] StreetsX = { "Западная", "Заводская", "Литейная", "Садовая", "Ленина", "Абая", "Центральная", "Мира", "Победы", "Гагарина", "Солнечная", "Весенняя", "Кольцевая", "Восточная", "Аэропортовская" };
        static readonly string[] StreetsZ = { "Набережная", "Морская", "Пляжная", "Парковая", "Школьная", "Тенистая", "Деловая", "Главный проспект", "Неоновая", "Звёздная", "Университетская", "Лесная", "Северная", "Горная", "Дальняя" };

        static bool built;

        public static void Build()
        {
            if (built) return;
            built = true;
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    ByChunk[x, z] = new List<Bld>();
                    Districts[x, z] = DistrictOf(x, z);
                }
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    BuildBlock(x, z);
            BuildOutskirts();
            Spawn = new Vector3(6 * P + 8f, Curb + 0.1f, 7 * P + 40f);
            Debug.Log($"[City] {All.Count} buildings");
        }

        static readonly HashSet<(int, int)> Parks = new HashSet<(int, int)> { (5, 4), (9, 10), (3, 9), (11, 3), (7, 12), (2, 11) };

        static District DistrictOf(int x, int z)
        {
            if (Parks.Contains((x, z))) return District.Park;
            if (z == 0) return District.Beach;
            if (x <= 1) return District.Industrial;
            if (z >= N - 2) return District.Suburb;
            float d = new Vector2(x - 6.5f, z - 7f).magnitude;
            if (d < 2.2f) return District.Downtown;
            if (d < 3.7f) return District.Business;
            return District.Residential;
        }

        /// <summary>Fixed places for the one-of-a-kind buildings (single = takes the whole block).</summary>
        static readonly Dictionary<(int, int), (BType type, bool single)> Fixed = new Dictionary<(int, int), (BType, bool)>
        {
            [(7, 6)] = (BType.Agency, true),
            [(7, 5)] = (BType.Mall, true), [(4, 11)] = (BType.Mall, true),
            [(3, 5)] = (BType.Hospital, true), [(10, 9)] = (BType.Hospital, true),
            [(2, 8)] = (BType.FireStation, false), [(11, 6)] = (BType.FireStation, false),
            [(4, 3)] = (BType.School, false), [(9, 12)] = (BType.School, false), [(12, 8)] = (BType.School, false),
            [(8, 10)] = (BType.University, true),
            [(6, 10)] = (BType.Library, false), [(8, 3)] = (BType.Library, false),
            [(6, 7)] = (BType.Bank, false), [(8, 8)] = (BType.Bank, false),
            [(5, 7)] = (BType.JobCenter, false),
            [(2, 4)] = (BType.CarDealer, true), [(12, 10)] = (BType.CarDealer, true), [(8, 1)] = (BType.CarDealer, true),
            [(1, 7)] = (BType.Station, true),
            [(0, 0)] = (BType.Port, true),
            [(6, 6)] = (BType.Hotel, false), [(10, 1)] = (BType.Hotel, false),
            [(7, 7)] = (BType.Parking, false), [(9, 5)] = (BType.Parking, false), [(4, 8)] = (BType.Parking, false),
            [(3, 2)] = (BType.GasStation, false), [(12, 5)] = (BType.GasStation, false), [(5, 12)] = (BType.GasStation, false),
            [(8, 7)] = (BType.Corp, false), [(5, 8)] = (BType.Corp, false),
            [(8, 4)] = (BType.GunShop, false), [(5, 9)] = (BType.GunShop, false),
            [(9, 7)] = (BType.Clothes, false), [(4, 6)] = (BType.Furniture, false), [(10, 4)] = (BType.Gym, false),
            [(3, 7)] = (BType.Garage, false), [(11, 11)] = (BType.Garage, false), [(5, 2)] = (BType.Garage, false),
            [(9, 9)] = (BType.Supermarket, false), [(4, 4)] = (BType.Supermarket, false), [(11, 8)] = (BType.Supermarket, false),
        };

        static BType Weighted(District d, Rng r)
        {
            (BType t, float w)[] table = d switch
            {
                District.Downtown => new[] { (BType.Tower, 6f), (BType.Office, 2f), (BType.Hotel, 0.6f), (BType.Restaurant, 0.6f) },
                District.Business => new[] { (BType.Office, 4f), (BType.Restaurant, 1.5f), (BType.Cafe, 1.5f), (BType.Apartment, 2f), (BType.Shop, 1f), (BType.Clothes, 0.6f), (BType.Supermarket, 0.5f), (BType.Gym, 0.4f), (BType.Hotel, 0.4f) },
                District.Residential => new[] { (BType.Apartment, 7f), (BType.Shop, 2f), (BType.Cafe, 1.2f), (BType.Restaurant, 0.8f), (BType.Garage, 0.4f), (BType.Supermarket, 0.4f), (BType.Gym, 0.3f), (BType.Furniture, 0.2f), (BType.Clothes, 0.3f) },
                District.Suburb => new[] { (BType.House, 9f), (BType.Shop, 0.8f), (BType.Cafe, 0.5f) },
                District.Beach => new[] { (BType.Villa, 5f), (BType.Cafe, 1f), (BType.Restaurant, 1f), (BType.Hotel, 0.7f) },
                District.Industrial => new[] { (BType.Factory, 3f), (BType.Warehouse, 3f), (BType.Garage, 0.8f) },
                _ => new[] { (BType.Cafe, 1f) },
            };
            float sum = 0; foreach (var e in table) sum += e.w;
            float x = r.F() * sum;
            foreach (var e in table) { x -= e.w; if (x <= 0) return e.t; }
            return table[0].t;
        }

        static void BuildBlock(int bx, int bz)
        {
            var d = Districts[bx, bz];
            var r = new Rng(H.U(bx, bz, 4242));
            float x0 = bx * P + Walk, z0 = bz * P + Walk, s = P - 2 * Walk; // 60×60
            Fixed.TryGetValue((bx, bz), out var fx);
            bool hasFixed = Fixed.ContainsKey((bx, bz));

            if (d == District.Park)
            {
                // one café pavilion in the park
                Add(bx, bz, BType.Cafe, d, new Rect(x0 + 2, z0 + 2, 12, 9), 0, 4.2f, 1, 6, r);
                return;
            }

            var lots = new List<(Rect rect, int front)>();
            if (hasFixed && fx.single)
                lots.Add((new Rect(x0, z0, s, s), 0));
            else if (d == District.Downtown)
            {
                lots.Add((new Rect(x0, z0, 28, s), 0));
                lots.Add((new Rect(x0 + 32, z0, 28, s), 1));
            }
            else if (d == District.Suburb)
            {
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                        lots.Add((new Rect(x0 + i * 20.66f, z0 + j * 31, 18.66f, 29), j == 0 ? 0 : 1));
            }
            else if (d == District.Beach)
            {
                for (int i = 0; i < 3; i++)
                    lots.Add((new Rect(x0 + i * 20.66f, z0, 18.66f, s), 0));
            }
            else if (d == District.Industrial)
            {
                lots.Add((new Rect(x0, z0, s, 28), 0));
                lots.Add((new Rect(x0, z0 + 32, s, 28), 1));
            }
            else
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                        lots.Add((new Rect(x0 + i * 32, z0 + j * 32, 28, 28), j == 0 ? 0 : 1));
            }

            for (int li = 0; li < lots.Count; li++)
            {
                var (lot, front) = lots[li];
                BType t = hasFixed && li == 0 ? fx.type : Weighted(d, r);
                // keep downtown/industrial fixed types sensible
                float set = 2f + r.F() * 2f;
                Rect fp;
                float h; int style;
                switch (t)
                {
                    case BType.Agency:
                        fp = Shrink(lot, front, 15, 3f, new Vector2(30, 30));
                        h = 20 * 3.6f; style = 0; break;
                    case BType.Corp:
                        fp = Shrink(lot, front, 2, 1f, new Vector2(24, 32));
                        h = 30 * 3.3f; style = 7; break;
                    case BType.Tower:
                        fp = Shrink(lot, front, r.F(2, 5), 1f, new Vector2(r.F(20, 26), r.F(24, 36)));
                        h = r.F(70, 180); style = r.Chance(0.5f) ? 0 : 7; break;
                    case BType.Office:
                        fp = Shrink(lot, front, set, 1f, new Vector2(r.F(20, 26), r.F(18, 26)));
                        h = r.F(24, 60); style = r.Chance(0.5f) ? 3 : 0; break;
                    case BType.Apartment:
                        fp = Shrink(lot, front, set, 1.5f, new Vector2(r.F(18, 24), r.F(14, 20)));
                        h = 3.3f * r.I(5) + 13.2f; style = r.I(3) switch { 0 => 1, 1 => 2, _ => 9 }; break;
                    case BType.House:
                        fp = Shrink(lot, front, 2f, 6f, new Vector2(r.F(9, 13), r.F(9, 12)));
                        h = r.Chance(0.5f) ? 3.4f : 6.6f; style = r.Chance(0.5f) ? 1 : 9; break;
                    case BType.Villa:
                        fp = Shrink(lot, front, 2f, 12f, new Vector2(r.F(14, 17), r.F(14, 20)));
                        h = 6.8f; style = 6; break;
                    case BType.Mall:
                        // 0.9: a wide low mall with a car park in front of it
                        fp = Shrink(lot, front, 2, 17f, new Vector2(54, 36)); h = 15f; style = 4; break;
                    case BType.Hospital:
                        fp = Shrink(lot, front, 4, 4f, new Vector2(46, 30)); h = 23.1f; style = 3; break;
                    case BType.University:
                        fp = Shrink(lot, front, 4, 4f, new Vector2(48, 30)); h = 19.8f; style = 8; break;
                    case BType.CarDealer:
                        fp = Shrink(lot, front, 4, 14f, new Vector2(40, 26)); h = 8f; style = 4; break;
                    case BType.Station:
                        fp = Shrink(lot, front, 4, 4f, new Vector2(50, 24)); h = 14f; style = 8; break;
                    case BType.Port:
                        fp = Shrink(lot, front, 4, 4f, new Vector2(30, 20)); h = 10f; style = 5; break;
                    case BType.Factory: case BType.Warehouse:
                        fp = Shrink(lot, front, set, 1.5f, new Vector2(r.F(36, 52), r.F(20, 24)));
                        h = r.F(9, 14); style = 5; break;
                    case BType.GasStation:
                        fp = Shrink(lot, front, 2, 14f, new Vector2(12, 9)); h = 4.2f; style = 4; break;
                    case BType.Garage:
                        fp = Shrink(lot, front, 2, 3f, new Vector2(r.F(16, 22), r.F(12, 16))); h = 6f; style = 5; break;
                    case BType.Parking:
                        fp = Shrink(lot, front, 2, 2f, new Vector2(22, 20)); h = 12.4f; style = 3; break;   // 0.9: an open multi-storey car park
                    case BType.Hotel:
                        fp = Shrink(lot, front, set, 1.5f, new Vector2(r.F(20, 26), r.F(16, 22))); h = 3.3f * (6 + r.I(10)); style = r.Chance(0.5f) ? 8 : 3; break;
                    case BType.Bank: case BType.JobCenter: case BType.Library: case BType.School: case BType.FireStation:
                        fp = Shrink(lot, front, 2, 2f, new Vector2(24, 18)); h = t == BType.FireStation ? 8f : 3.3f * (3 + r.I(3)); style = t == BType.Bank || t == BType.Library ? 8 : 3; break;
                    case BType.Restaurant: case BType.Cafe: case BType.Shop: case BType.Clothes: case BType.Furniture: case BType.Gym: case BType.Supermarket:
                    default:
                        bool small = t == BType.Cafe || t == BType.Shop;
                        fp = Shrink(lot, front, set, 1f, small ? new Vector2(r.F(14, 20), r.F(12, 16)) : new Vector2(r.F(20, 26), r.F(16, 22)));
                        // 0.9: the supermarket gets the whole lot so its hall fits inside the building
                        if (t == BType.Supermarket) fp = Shrink(lot, front, 0.3f, 1f, new Vector2(27.4f, 23.4f));
                        h = d == District.Beach ? 4.4f : 3.3f * (1 + r.I(4)) + 4.4f; style = 4; break;
                }
                int floors = Mathf.Max(1, Mathf.RoundToInt(h / 3.3f));
                // 0.7: building plots in the suburbs (18 of them) where players build their own houses
                if (d == District.Suburb && IsPlot(bx, bz, li) && !(hasFixed && li == 0))
                {
                    t = BType.Plot; h = 0.5f; floors = 0;
                    fp = new Rect(lot.xMin + 0.3f, lot.yMin + 0.3f, lot.width - 0.6f, lot.height - 0.6f);
                }
                Add(bx, bz, t, d, fp, front, h, floors, style, r);
            }
        }

        static bool IsPlot(int bx, int bz, int li) =>
            (bz == N - 2 && bx >= 3 && bx <= 11 && bx != 7 && (li == 1 || li == 4)) || (bz == N - 1 && (bx == 4 || bx == 10) && li == 2);

        /// <summary>Footprint of a given size inside the lot, pushed towards its front edge.</summary>
        static Rect Shrink(Rect lot, int front, float side, float frontGap, Vector2 size)
        {
            float w = Mathf.Min(size.x, lot.width - side * 2), dpt = Mathf.Min(size.y, lot.height - frontGap - 2);
            float cx = lot.center.x - w / 2f;
            float z = front == 0 ? lot.yMin + frontGap : lot.yMax - frontGap - dpt;
            return new Rect(cx, z, w, dpt);
        }

        static void Add(int bx, int bz, BType t, District d, Rect fp, int front, float h, int floors, int style, Rng r)
        {
            var b = new Bld
            {
                id = All.Count, type = t, district = d, rect = fp, height = h, floors = floors, style = style, front = front, cx = bx, cz = bz,
                roofHelipad = t == BType.Agency || t == BType.Tower && r.Chance(0.4f),
            };
            b.door = DoorOf(fp, front);
            NameIt(b);
            All.Add(b);
            if (bx >= 0) ByChunk[bx, bz].Add(b); else Outskirts.Add(b);
        }

        static Vector3 DoorOf(Rect fp, int front) => front switch
        {
            0 => new Vector3(fp.center.x, Curb + 0.01f, fp.yMin),
            1 => new Vector3(fp.center.x, Curb + 0.01f, fp.yMax),
            2 => new Vector3(fp.xMin, Curb + 0.01f, fp.center.y),
            _ => new Vector3(fp.xMax, Curb + 0.01f, fp.center.y),
        };

        static void NameIt(Bld b)
        {
            var info = b.Info;
            if (b.type == BType.House) b.name = "Дом " + (b.id % 97 + 1);
            else if (b.type == BType.Plot) b.name = "Участок №" + (b.id % 89 + 1);
            else if (b.type == BType.Corp) b.name = b.cx < 7 ? "Herbal Tower" : "Herbal Plaza";
            else if (b.type == BType.Agency) b.name = "Herbal City · Недвижимость";
            else b.name = H.Pick(info.Names, b.id, 77);
            if (b.cx >= 0)
            {
                // address: street along the nearest road in front of the door
                string street = b.front <= 1 ? StreetsZ[Mathf.Clamp(b.cz + (b.front == 1 ? 1 : 0), 0, StreetsZ.Length - 1)]
                                             : StreetsX[Mathf.Clamp(b.cx + (b.front == 3 ? 1 : 0), 0, StreetsX.Length - 1)];
                int num = Mathf.Abs((int)(b.front <= 1 ? b.door.x : b.door.z) / 8) + 1;
                b.address = (street.StartsWith("Главный") ? street : "ул. " + street) + ", " + num;
            }
            else b.address = "За городом";
        }

        static void BuildOutskirts()
        {
            var r = new Rng(999);
            // Airport terminal (door faces the access road on the west side of the terminal).
            Add(-1, -1, BType.Airport, District.Outskirts, new Rect(AirportX0 + 40, 7 * P + 20, 60, 34), 0, 16f, 3, 0, r);
            All[All.Count - 1].door = new Vector3(AirportX0 + 70, Curb + 0.01f, 7 * P + 20);
            // Mountain lodge past the tunnel.
            Add(-1, -1, BType.Restaurant, District.Outskirts, new Rect(NorthRoadX + 14, TunnelZ1 + 120, 18, 14), 2, 6.6f, 2, 6, r);
            var lodge = All[All.Count - 1];
            lodge.name = "Ресторан «Перевал»";
            lodge.door = new Vector3(lodge.rect.xMin, Curb + 0.01f, lodge.rect.center.y);
            // Beach hotel and lifeguard café on the sand.
            Add(-1, -1, BType.Cafe, District.Outskirts, new Rect(9 * P, -70, 12, 8), 1, 4.4f, 1, 6, r);
            All[All.Count - 1].name = "Пляжный бар «Волна»";
            // 0.9: cafés at the ends of the new roads: by the lake and by the lighthouse
            Add(-1, -1, BType.Cafe, District.Outskirts, new Rect(-640, 476, 12, 9), 2, 4.4f, 1, 6, r);
            All[All.Count - 1].name = "Кафе «Озеро»";
            Add(-1, -1, BType.Cafe, District.Outskirts, new Rect(Size + 645, -118, 12, 9), 2, 4.4f, 1, 6, r);
            All[All.Count - 1].name = "Кафе «Маяк»";
        }

        public static Bld Get(int id) => id >= 0 && id < All.Count ? All[id] : null;

        public static bool InCity(Vector3 p) => p.x >= 0 && p.z >= 0 && p.x < Size && p.z < Size;

        public static (int, int) ChunkOf(Vector3 p) => (Mathf.FloorToInt(p.x / P), Mathf.FloorToInt(p.z / P));

        /// <summary>Nearest building door within maxDist of p.</summary>
        public static Bld DoorNear(Vector3 p, float maxDist)
        {
            Bld best = null;
            float bd = maxDist * maxDist;
            var (cx, cz) = ChunkOf(p);
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int z = cz - 1; z <= cz + 1; z++)
                {
                    if (x < 0 || z < 0 || x >= N || z >= N) continue;
                    foreach (var b in ByChunk[x, z]) Check(b);
                }
            foreach (var b in Outskirts) Check(b);
            return best;

            void Check(Bld b)
            {
                var d = b.door - p; d.y = 0;
                float m = d.sqrMagnitude;
                if (m < bd) { bd = m; best = b; }
            }
        }

        public static List<Bld> OfType(BType t) => All.FindAll(b => b.type == t);

        public static Bld Nearest(BType t, Vector3 p)
        {
            Bld best = null; float bd = float.MaxValue;
            foreach (var b in All)
                if (b.type == t)
                {
                    float d = (b.door - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = b; }
                }
            return best;
        }

        /// <summary>Closest point on a road lane (for spawning cars).</summary>
        public static Vector3 NearestRoad(Vector3 p, out float yaw)
        {
            float gx = Mathf.Round(p.x / P) * P, gz = Mathf.Round(p.z / P) * P;
            gx = Mathf.Clamp(gx, 0, Size); gz = Mathf.Clamp(gz, 0, Size);
            float dx = Mathf.Abs(p.x - gx), dz = Mathf.Abs(p.z - gz);
            // keep clear of the crossroads
            float Mid(float a)
            {
                a = Mathf.Clamp(a, 18, Size - 18);
                float l = Mathf.Repeat(a, P);
                if (l < 18) a += 18 - l; else if (l > P - 18) a -= l - (P - 18);
                return a;
            }
            if (dx < dz)
            {
                yaw = 0;
                return new Vector3(gx + 3f, 0.05f, Mid(p.z));
            }
            yaw = 270;
            return new Vector3(Mid(p.x), 0.05f, gz + 3f);
        }

        public static string DistrictName(District d) => d switch
        {
            District.Downtown => "Центр", District.Business => "Деловой район", District.Residential => "Жилой район", District.Suburb => "Пригород",
            District.Beach => "Пляжный район", District.Industrial => "Промзона", District.Park => "Парк", _ => "Загород",
        };

        public static string DistrictAt(Vector3 p)
        {
            if (p.y < -100) return "В здании";
            if (p.z < 0 && p.x > 0 && p.x < Size) return "Пляж";
            if (p.x > Size + 40 && p.z > AirportZ0 - 100 && p.z < AirportZ1 + 100) return "Аэропорт";
            if (p.z > Size + 150) return "Горы";
            if (!InCity(p)) return "Загород";
            var (x, z) = ChunkOf(p);
            return DistrictName(Districts[x, z]);
        }
    }
}
