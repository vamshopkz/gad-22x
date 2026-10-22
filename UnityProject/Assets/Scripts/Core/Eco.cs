using UnityEngine;

namespace Gad
{
    /// <summary>Money, property, businesses, needs (food / energy / health).</summary>
    public static class Eco
    {
        public static void Earn(long amount, string why)
        {
            Save.D.money += amount;
            Hud.Toast("+" + Hud.Money(amount) + "  " + why, new Color(0.45f, 1f, 0.5f));
            AudioFx.Play("coin");
            Save.Write();
        }

        public static bool Spend(long price, string what = null)
        {
            if (Save.D.money < price)
            {
                Hud.Toast("Не хватает денег: нужно " + Hud.Money(price), new Color(1f, 0.5f, 0.45f));
                AudioFx.Play("error");
                return false;
            }
            Save.D.money -= price;
            if (what != null) Hud.Toast("−" + Hud.Money(price) + "  " + what, new Color(1f, 0.85f, 0.4f));
            AudioFx.Play("buy");
            Save.Write();
            return true;
        }

        public static bool OwnsHome(int bid, int unit) => Save.D.homes.Exists(h => h.building == bid && h.unit == unit);
        public static OwnedHome Home(int bid, int unit) => Save.D.homes.Find(h => h.building == bid && h.unit == unit);
        public static OwnedBiz Biz(int bid) => Save.D.biz.Find(z => z.building == bid);

        public static void BuyHome(Bld b, int unit)
        {
            if (OwnsHome(b.id, unit)) return;
            if (!Spend(b.UnitPrice(unit), "Покупка жилья")) return;
            Save.D.homes.Add(new OwnedHome { building = b.id, unit = unit });
            Save.Write();
            Hud.Toast("Поздравляем! Теперь это ваш дом: " + b.name + (unit > 0 ? ", кв. " + ((unit / 10 + 1) * 100 + unit % 10) : ""), new Color(0.5f, 1f, 0.6f));
            var pl = PlayerCtl.I;
            if (unit >= 10 && Interiors.Current == b && pl.Inside == b.id)
            {
                // stay where we are: redraw this floor so the flat becomes ours (door, sign, furniture)
                var pos = pl.transform.position; float yaw = pl.transform.eulerAngles.y;
                Interiors.BuildLevel(Interiors.Floor);
                pl.Teleport(pos, yaw);
            }
            else if (unit == 0) Interiors.Enter(b, 0);
        }

        public static long IncomePerHour(Bld b, OwnedBiz z) => (long)(b.BizPrice * 0.002f * (1f + (z.level - 1) * 0.6f));

        /// <summary>Uncollected income: accrues in real time (also while offline), capped at 12 hours.</summary>
        public static long Pending(OwnedBiz z)
        {
            var b = City.Get(z.building);
            if (b == null) return 0;
            // 1 real minute = 1 game hour; at most 12 real hours pile up
            float gameHours = Mathf.Min(720f, (float)((Save.Now - z.lastCollect) / 60.0));
            return (long)(IncomePerHour(b, z) * gameHours);
        }

        public static void BuyBiz(Bld b)
        {
            if (Biz(b.id) != null) return;
            if (!Spend(b.BizPrice, "Покупка бизнеса")) return;
            Save.D.biz.Add(new OwnedBiz { building = b.id, level = 1, lastCollect = Save.Now });
            Save.Write();
            Hud.Toast("Вы владелец: " + b.name + ". Доход капает каждый игровой час!", new Color(0.5f, 1f, 0.6f));
        }

        public static long UpgradeCost(Bld b, OwnedBiz z) => b.BizPrice / 2 * z.level;

        public static void Upgrade(Bld b)
        {
            var z = Biz(b.id);
            if (z == null || z.level >= 5) return;
            Collect(z);
            if (!Spend(UpgradeCost(b, z), "Улучшение бизнеса")) return;
            z.level++;
            Save.Write();
        }

        public static void Collect(OwnedBiz z)
        {
            long p = Pending(z);
            z.lastCollect = Save.Now;
            if (p > 0) Earn(p, "доход: " + City.Get(z.building)?.name);
        }

        public static long PendingAll()
        {
            long s = 0;
            foreach (var z in Save.D.biz) s += Pending(z);
            return s;
        }

        public static void CollectAll()
        {
            long s = PendingAll();
            foreach (var z in Save.D.biz) z.lastCollect = Save.Now;
            if (s > 0) Earn(s, "доход всех бизнесов"); else Hud.Toast("Пока нечего собирать");
        }

        public static void Eat(int price, int food, string msg)
        {
            if (Save.D.food >= 99) { Hud.Toast("Вы сыты"); return; }
            if (price > 0 && !Spend(price)) return;
            Save.D.food = Mathf.Min(100, Save.D.food + food);
            Hud.Toast(msg + "  +" + food + " сытости", new Color(0.6f, 1f, 0.6f));
            AudioFx.Play("eat");
        }

        static float bankTimer;

        /// <summary>Needs go down over time (1 real minute = 1 game hour).</summary>
        public static void Tick(float dt)
        {
            var d = Save.D;
            d.food = Mathf.Max(0, d.food - dt * 0.012f);
            d.energy = Mathf.Max(0, d.energy - dt * 0.008f);
            if (d.food <= 0 || d.energy <= 0) d.health = Mathf.Max(5, d.health - dt * 0.02f);
            else d.health = Mathf.Min(100, d.health + dt * 0.01f);
            bankTimer += dt;
            if (bankTimer > 60f)
            {
                bankTimer = 0;
                // deposit interest: 0.1% per game hour
                if (d.bank > 0) d.bank += Mathf.Max(1, (int)(d.bank * 0.001));
            }
        }
    }
}
