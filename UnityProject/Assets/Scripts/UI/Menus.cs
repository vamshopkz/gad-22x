using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gad
{
    /// <summary>All the windows of the game: phone, map, shops, dealer, garage, homes, jobs, business…</summary>
    public static class Menus
    {
        static readonly Color Green = new Color(0.15f, 0.7f, 0.35f), Blue = new Color(0.15f, 0.5f, 0.9f), Orange = new Color(0.95f, 0.55f, 0.1f), Grey = new Color(0.35f, 0.38f, 0.45f);

        // ------------------------------------------------------------------ phone

        public static void Phone()
        {
            Window.Show("ТЕЛЕФОН · " + Save.D.name, w =>
            {
                var d = Save.D;
                w.Text($"Деньги: {Hud.Money(d.money)}   В банке: {Hud.Money(d.bank)}   Образование: {EduName(d.edu)}   Смен отработано: {d.shifts}", 24, UIKit.Muted);
                var job = JobDef.Get(d.job);
                w.Row("Работа", job != null ? job.Title + (job.Kind > 0 ? " · задания по городу" : " · смены в здании") : "Нет работы — загляните в Центр занятости", "ОТКРЫТЬ", Blue, MyJob);
                w.Row("Мои машины", d.cars.Count == 0 ? "Пока нет машин — купите в автосалоне" : d.cars.Count + " шт. Вызвать к себе", "ГАРАЖ", Orange, () => Garage(null));
                w.Row("Моя недвижимость", d.homes.Count == 0 ? "Нет жилья — квартиры, дома, виллы продаются по всему городу" : d.homes.Count + " объект(ов)", "ОТКРЫТЬ", Green, Homes);
                w.Row("Мой бизнес", d.biz.Count == 0 ? "Купите магазин, кафе, завод… — доход каждый час" : $"{d.biz.Count} шт. · накопилось {Hud.Money(Eco.PendingAll())}", "ОТКРЫТЬ", Green, Business);
                w.Row("Карта города и GPS", "Найти автосалон, работу, дом…", "КАРТА", Blue, Map);
                w.Row("Чат и игроки онлайн", "Сейчас в мире: " + (WorldNet.OnlineCount + 1) + " (вы + другие игроки)", "ЧАТ", Blue, Chat);
                w.Row("Такси (быстро)", "Перенестись в любое место города за $60", "ВЫБРАТЬ", Orange, () => Travel(null));
                w.Row("Внешность", "Переодеться можно дома (гардероб) или купить одежду в магазине", "ДОМА", Grey, () => Hud.Toast("Гардероб — у вас дома или в магазине одежды"));
                w.Row("Настройки", "Графика, звук, имя", "ОТКРЫТЬ", Grey, Settings);
            });
        }

        static string EduName(int e) => e switch { 0 => "нет", 1 => "школа", _ => "университет" };

        public static void MyJob()
        {
            Window.Show("РАБОТА", w =>
            {
                var job = JobDef.Get(Save.D.job);
                if (job == null)
                {
                    w.Text("У вас нет работы. Устроиться можно в любом здании с вакансиями или в Центре занятости.", 26);
                    w.Row("Все вакансии города", "", "СМОТРЕТЬ", Blue, AllJobs);
                    return;
                }
                w.Text($"Должность: {job.Title}\nОплата: {Hud.Money(job.Pay)} за {(job.Kind == 0 ? "смену" : "заказ")}", 28);
                if (job.Kind == 0)
                {
                    var b = City.Get(Save.D.jobBuilding) ?? City.Nearest(job.Where, PlayerCtl.I.transform.position);
                    w.Row("Место работы: " + b.name, b.address, "GPS", Blue, () => { Hud.SetGps(b.door, b.name); Window.Close(); });
                    w.Text("Придите на работу и нажмите «НАЧАТЬ СМЕНУ» внутри здания.", 24, UIKit.Muted);
                }
                else if (Work.MissionKind == 0)
                    w.Row(job.Kind == 1 ? "Взять заказ на доставку" : "Выйти на линию такси", "Цель появится на карте", "НАЧАТЬ", Green, () => { Window.Close(); Work.StartMission(job); });
                else
                    w.Row("Закончить задания", Work.TargetLabel, "СТОП", Orange, () => { Work.StopMission(); Window.Refresh(); });
                w.Row("Уволиться", "", "УВОЛИТЬСЯ", new Color(0.8f, 0.2f, 0.2f), () => { Save.D.job = ""; Save.D.jobBuilding = -1; Work.StopMission(); Hud.Toast("Вы уволились"); Window.Refresh(); });
            });
        }

        public static void AllJobs()
        {
            Window.Show("ВАКАНСИИ ГОРОДА", w =>
            {
                foreach (var j in JobDef.All)
                {
                    var b = City.Nearest(j.Where, PlayerCtl.I.Inside >= 0 ? City.Get(PlayerCtl.I.Inside).door : PlayerCtl.I.transform.position);
                    bool ok = Save.D.edu >= j.Edu;
                    string sub = $"{Hud.Money(j.Pay)} за {(j.Kind == 0 ? "смену" : "заказ")} · {BInfo.All[j.Where].Title}" + (ok ? "" : " · нужно образование: " + EduName(j.Edu));
                    w.Row(j.Title + (Save.D.job == j.Id ? "  (ваша)" : ""), sub, ok ? "УСТРОИТЬСЯ" : "GPS", ok ? Green : Blue, () =>
                    {
                        if (ok) Hire(j, b);
                        else { Hud.SetGps(City.Nearest(j.Edu >= 2 ? BType.University : BType.School, PlayerCtl.I.transform.position).door, "Учёба"); Window.Close(); }
                    });
                }
            });
        }

        static void Hire(JobDef j, Bld b)
        {
            if (Save.D.edu < j.Edu) { Hud.Toast("Нужно образование: " + EduName(j.Edu)); return; }
            Save.D.job = j.Id;
            Save.D.jobBuilding = b != null ? b.id : -1;
            Save.Write();
            Hud.Toast("Вы приняты: " + j.Title + "!", new Color(0.5f, 1f, 0.6f));
            Window.Close();
            if (j.Kind == 0 && PlayerCtl.I.Inside >= 0 && b != null && b.id == PlayerCtl.I.Inside) Interiors.Enter(b, 0); // re-enter to show the shift point
            else if (j.Kind == 0 && b != null) Hud.SetGps(b.door, b.name);
            else Work.StartMission(j);
        }

        /// <summary>Jobs and ownership of one building.</summary>
        public static void Building(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Text(b.Title + " · " + b.address, 24, UIKit.Muted);
                foreach (var j in JobDef.All)
                {
                    if (j.Where != b.type) continue;
                    bool mine = Save.D.job == j.Id;
                    bool ok = Save.D.edu >= j.Edu;
                    string sub = $"{Hud.Money(j.Pay)} за {(j.Kind == 0 ? "смену" : "заказ")}" + (ok ? "" : " · нужно: " + EduName(j.Edu));
                    if (mine && j.Kind == 0) w.Row(j.Title + " (ваша работа)", sub, "НАЧАТЬ СМЕНУ", Green, () => { Window.Close(); Work.StartShift(j, b); });
                    else if (mine) w.Row(j.Title + " (ваша работа)", sub, Work.MissionKind == 0 ? "ВЗЯТЬ ЗАКАЗ" : "ИДЁТ", Green, () => { Window.Close(); if (Work.MissionKind == 0) Work.StartMission(j); });
                    else w.Row(j.Title, sub, "УСТРОИТЬСЯ", ok ? Blue : Grey, () => Hire(j, b), ok);
                }
                if (b.BizPrice > 0)
                {
                    var z = Eco.Biz(b.id);
                    if (z == null)
                        w.Row("Купить этот бизнес", $"Цена {Hud.Money(b.BizPrice)} · доход ≈{Hud.Money(Eco.IncomePerHour(b, new OwnedBiz { level = 1 }))} в игровой час", "КУПИТЬ", Orange, () => { Eco.BuyBiz(b); Window.Refresh(); }, Save.D.money >= b.BizPrice);
                    else
                    {
                        w.Row($"Ваш бизнес · уровень {z.level}", $"Доход {Hud.Money(Eco.IncomePerHour(b, z))}/ч · накопилось {Hud.Money(Eco.Pending(z))}", "СОБРАТЬ", Green, () => { Eco.Collect(z); Window.Refresh(); });
                        if (z.level < 5) w.Row("Улучшить бизнес", "+60% дохода · " + Hud.Money(Eco.UpgradeCost(b, z)), "УЛУЧШИТЬ", Orange, () => { Eco.Upgrade(b); Window.Refresh(); });
                    }
                }
                if (b.IsHome && b.Units > 1) w.Row("Квартиры в этом доме", "", "СМОТРЕТЬ", Blue, () => Flats(b));
            });
        }

        // ------------------------------------------------------------------ property

        public static void Flats(Bld b)
        {
            Window.Show(b.name.ToUpper() + " · КВАРТИРЫ", w =>
            {
                int n = Mathf.Min(b.Units, 16);
                w.Text(b.type == BType.Tower ? "Пентхаусы и апартаменты с видом на город" : "Выберите квартиру. Свою можно обустроить и спать в ней.", 24, UIKit.Muted);
                for (int u = 1; u <= n; u++)
                {
                    int unit = u;
                    bool own = Eco.OwnsHome(b.id, u);
                    int floor = b.type == BType.Tower ? Mathf.Max(2, b.floors - n + u) : 1 + (u - 1) / 4;
                    w.Row((b.type == BType.Tower ? "Апартаменты " : "Квартира ") + u + (own ? "  (МОЯ)" : ""), $"{floor} этаж · {Hud.Money(b.UnitPrice(u))}", own ? "ДОМОЙ" : "СМОТРЕТЬ", own ? Green : Blue, () => { Window.Close(); Interiors.Enter(b, unit); });
                }
            });
        }

        public static void BuyHome(Bld b, int unit)
        {
            int price = b.UnitPrice(unit);
            Window.Show("ПОКУПКА ЖИЛЬЯ", w =>
            {
                w.Text($"{b.name}{(unit > 0 ? ", кв. " + unit : "")}\n{b.address}\nЦена: {Hud.Money(price)}", 30);
                w.Text("В своём доме можно спать, переодеваться, хранить вещи и расставлять мебель.", 24, UIKit.Muted);
                w.Row("Купить", "У вас " + Hud.Money(Save.D.money), "КУПИТЬ", Green, () => { Window.Close(); Eco.BuyHome(b, unit); }, Save.D.money >= price);
            }, 900, 560);
        }

        public static void Homes()
        {
            Window.Show("МОЯ НЕДВИЖИМОСТЬ", w =>
            {
                if (Save.D.homes.Count == 0) w.Text("Пока ничего нет. Жилые дома, частные дома и виллы есть по всему городу — заходите и смотрите!", 26);
                foreach (var h in Save.D.homes)
                {
                    var b = City.Get(h.building);
                    if (b == null) continue;
                    w.Row(b.name + (h.unit > 0 ? ", кв. " + h.unit : ""), b.address + " · мебели: " + h.decor.Count, "GPS", Blue, () => { Hud.SetGps(b.door, b.name); Window.Close(); });
                }
            });
        }

        public static void Business()
        {
            Window.Show("МОЙ БИЗНЕС", w =>
            {
                w.Row("Собрать весь доход", "Накопилось: " + Hud.Money(Eco.PendingAll()), "СОБРАТЬ", Green, () => { Eco.CollectAll(); Window.Refresh(); });
                foreach (var z in Save.D.biz)
                {
                    var b = City.Get(z.building);
                    if (b == null) continue;
                    w.Row($"{b.name} · ур. {z.level}", $"{Hud.Money(Eco.IncomePerHour(b, z))}/ч · {b.address}", "GPS", Blue, () => { Hud.SetGps(b.door, b.name); Window.Close(); });
                }
                if (Save.D.biz.Count == 0) w.Text("Купить бизнес можно в магазинах, кафе, ресторанах, офисах, на заводах, складах, в отелях и др.", 24, UIKit.Muted);
            });
        }

        public static void Sleep()
        {
            Window.Close();
            Hud.FadeThrough(() =>
            {
                Save.D.energy = 100;
                Save.D.health = Mathf.Min(100, Save.D.health + 20);
                Save.Write();
                Hud.Toast("Вы выспались! Энергия 100%", new Color(0.5f, 0.9f, 1f));
            }, 1.2f);
        }

        // ------------------------------------------------------------------ shops

        public static void Food(Bld b, FoodDef[] items)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Text($"Сытость: {Mathf.RoundToInt(Save.D.food)}%", 26, UIKit.Muted);
                foreach (var f in items)
                    w.Row(f.Title, "+" + f.Food + " сытости", Hud.Money(f.Price), Green, () => { Eco.Eat(f.Price, f.Food, f.Title); Window.Refresh(); });
            }, 900, 660);
        }

        public static void CarDealer(Bld b)
        {
            int color = 0;
            Window.Show("АВТОСАЛОН · " + b.name.ToUpper(), w =>
            {
                var chips = new List<(string, Color, bool, System.Action)>();
                for (int i = 0; i < Palette.Car.Length; i++) { int ci = i; chips.Add((Palette.CarNames[i], Palette.Car[i], color == i, () => { color = ci; Window.Refresh(); })); }
                w.Chips("Цвет кузова", chips);
                foreach (var c in CarDef.All)
                {
                    var def = c;
                    w.Row(c.Title, $"до {Mathf.RoundToInt(c.MaxSpeed * 3.6f)} км/ч · разгон {c.Accel:0}", Hud.Money(c.Price), Green, () =>
                    {
                        if (!Eco.Spend(def.Price, "Покупка: " + def.Title)) return;
                        Save.D.cars.Add(new OwnedCar { model = def.Id, color = color });
                        Save.Write();
                        Window.Close();
                        Interiors.Exit();
                        var car = PlayerCtl.I.CallCar(Save.D.cars.Count - 1, PlayerCtl.I.transform.position);
                        Hud.Toast("Ваша новая машина ждёт у входа! Нажмите «СЕСТЬ»", new Color(0.5f, 1f, 0.6f));
                        _ = car;
                    }, Save.D.money >= c.Price, Palette.Car[color]);
                }
            });
        }

        /// <summary>My cars: call, repair, tune, paint. b = garage building (or null from the phone).</summary>
        public static void Garage(Bld b, bool parking = false)
        {
            Window.Show(b == null ? "МОИ МАШИНЫ" : b.name.ToUpper(), w =>
            {
                if (Save.D.cars.Count == 0) { w.Text("У вас нет машин. Автосалоны отмечены на карте жёлтым.", 26); w.Row("Ближайший автосалон", "", "GPS", Blue, () => { var cd = City.Nearest(BType.CarDealer, PlayerCtl.I.transform.position); Hud.SetGps(cd.door, cd.name); Window.Close(); }); return; }
                for (int i = 0; i < Save.D.cars.Count; i++)
                {
                    int idx = i;
                    var oc = Save.D.cars[i];
                    var def = CarDef.Get(oc.model);
                    string sub = $"Состояние {Mathf.RoundToInt(oc.health)}% · тюнинг {oc.tune}/3 · {Palette.CarNames[oc.color % Palette.CarNames.Length]}";
                    w.Row(def.Title, sub, PlayerCtl.I.Inside >= 0 ? "ЗАБРАТЬ" : "ВЫЗВАТЬ", Green, () =>
                    {
                        Window.Close();
                        if (oc.health <= 0 && b == null) { Hud.Toast("Машина сломана — отремонтируйте в автомастерской"); return; }
                        if (PlayerCtl.I.Inside >= 0) Interiors.Exit();
                        PlayerCtl.I.CallCar(idx, PlayerCtl.I.transform.position);
                        Hud.Toast("Машина подана на ближайшую дорогу");
                    }, true, Palette.Get(Palette.Car, oc.color));
                    if (b != null && b.type == BType.Garage)
                    {
                        int repair = Mathf.RoundToInt((100 - oc.health) * def.Price / 1500f) + 20;
                        if (oc.health < 100) w.Row("   Ремонт", Hud.Money(repair), "ПОЧИНИТЬ", Blue, () => { if (Eco.Spend(repair, "Ремонт")) { oc.health = 100; Window.Refresh(); } });
                        int tune = def.Price / 5 * (oc.tune + 1);
                        if (oc.tune < 3) w.Row("   Тюнинг двигателя +" + (oc.tune + 1), "Скорость и разгон выше · " + Hud.Money(tune), "УЛУЧШИТЬ", Orange, () => { if (Eco.Spend(tune, "Тюнинг")) { oc.tune++; Window.Refresh(); } });
                        var chips = new List<(string, Color, bool, System.Action)>();
                        for (int c = 0; c < Palette.Car.Length; c++) { int cc = c; chips.Add((Palette.CarNames[c], Palette.Car[c], oc.color == c, () => { if (oc.color != cc && Eco.Spend(500, "Покраска")) { oc.color = cc; Save.Write(); Window.Refresh(); } })); }
                        w.Chips("   Покраска $500", chips);
                    }
                }
                if (b == null) w.Text("Ремонт, тюнинг и покраска — в автомастерской (СТО).", 22, UIKit.Muted);
            });
        }

        public static void Hospital(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Text($"Здоровье: {Mathf.RoundToInt(Save.D.health)}%", 28);
                w.Row("Лечение", "Полностью восстанавливает здоровье", Hud.Money(150), Green, () => { if (Eco.Spend(150, "Лечение")) { Save.D.health = 100; Window.Refresh(); } });
                w.Row("Работа в больнице", "Медсестра / медбрат, врач", "ВАКАНСИИ", Blue, () => Building(b));
            }, 900, 560);
        }

        public static void Study(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                var d = Save.D;
                int target = b.type == BType.University ? 2 : 1;
                w.Text($"Ваше образование: {EduName(d.edu)}" + (d.edu < 2 ? $" · прогресс {Mathf.RoundToInt(d.eduProgress * 100)}%" : ""), 28);
                if (b.type == BType.University && d.edu < 1) { w.Text("Сначала окончите школу.", 26, UIKit.Gold); return; }
                if (d.edu >= target && b.type != BType.Library) { w.Text("Вы уже окончили это учебное заведение. Можно работать здесь.", 26); w.Row("Работа", "", "ВАКАНСИИ", Blue, () => Building(b)); return; }
                if (d.edu >= 2) { w.Text("У вас высшее образование!", 26); return; }
                int price = b.type == BType.University ? 300 : 0;
                float gain = b.type == BType.Library ? 0.1f : b.type == BType.University ? 0.17f : 0.25f;
                w.Row(b.type == BType.Library ? "Читать учебники" : "Посетить занятие", $"+{Mathf.RoundToInt(gain * 100)}% к обучению · −10 энергии", price > 0 ? Hud.Money(price) : "БЕСПЛАТНО", Green, () =>
                {
                    if (Save.D.energy < 10) { Hud.Toast("Слишком устали"); return; }
                    if (price > 0 && !Eco.Spend(price)) return;
                    Window.Close();
                    Hud.FadeThrough(() =>
                    {
                        Save.D.energy -= 10;
                        Save.D.eduProgress += gain;
                        if (Save.D.eduProgress >= 0.999f)
                        {
                            Save.D.eduProgress = 0;
                            Save.D.edu++;
                            Hud.Toast(Save.D.edu == 1 ? "Вы окончили школу! Доступны новые профессии" : "Диплом университета! Доступны лучшие вакансии", UIKit.Gold);
                        }
                        else Hud.Toast("Занятие окончено: " + Mathf.RoundToInt(Save.D.eduProgress * 100) + "%");
                        Save.Write();
                    }, 1.5f);
                });
            }, 980, 600);
        }

        public static void Hotel(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Row("Номер на ночь", "Выспаться: энергия 100%", Hud.Money(250), Green, () => { if (Eco.Spend(250, "Номер в отеле")) Sleep(); });
                w.Row("Ресторан отеля", "", "МЕНЮ", Blue, () => Food(b, FoodDef.Restaurant));
                w.Row("Работа и владение", "", "ОТКРЫТЬ", Grey, () => Building(b));
            }, 900, 560);
        }

        public static void Bank(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Text($"Наличные: {Hud.Money(Save.D.money)}\nНа вкладе: {Hud.Money(Save.D.bank)} (+0,1% каждый игровой час)", 28);
                foreach (long amt in new long[] { 1000, 10000, 100000 })
                {
                    long a = amt;
                    w.Row("Положить " + Hud.Money(a), "", "ПОЛОЖИТЬ", Green, () => { if (Save.D.money >= a) { Save.D.money -= a; Save.D.bank += a; Save.Write(); Window.Refresh(); } else Hud.Toast("Недостаточно наличных"); });
                    w.Row("Снять " + Hud.Money(a), "", "СНЯТЬ", Blue, () => { if (Save.D.bank >= a) { Save.D.bank -= a; Save.D.money += a; Save.Write(); Window.Refresh(); } else Hud.Toast("На вкладе меньше"); });
                }
                w.Row("Работа в банке", "", "ВАКАНСИИ", Grey, () => Building(b));
            });
        }

        public static void Gym(Bld b)
        {
            Window.Show(b.name.ToUpper(), w =>
            {
                w.Row("Тренировка", "+15 здоровья · −20 энергии", Hud.Money(40), Green, () =>
                {
                    if (Save.D.energy < 20) { Hud.Toast("Слишком устали"); return; }
                    if (!Eco.Spend(40)) return;
                    Window.Close();
                    Hud.FadeThrough(() => { Save.D.energy -= 20; Save.D.health = Mathf.Min(100, Save.D.health + 15); Hud.Toast("Отличная тренировка!"); });
                });
                w.Row("Работа и владение", "", "ОТКРЫТЬ", Grey, () => Building(b));
            }, 900, 520);
        }

        /// <summary>Fast travel (airport, station, port tickets, phone taxi).</summary>
        public static void Travel(Bld from)
        {
            int price = from == null ? 60 : from.type == BType.Airport ? 200 : 80;
            Window.Show(from == null ? "ТАКСИ" : "БИЛЕТЫ", w =>
            {
                w.Text("Куда отправимся? Цена " + Hud.Money(price), 26, UIKit.Muted);
                (string name, System.Func<Bld> where)[] dest =
                {
                    ("Центр города", () => City.Nearest(BType.Tower, new Vector3(6.5f * City.P, 0, 7 * City.P))),
                    ("Центр занятости", () => City.Nearest(BType.JobCenter, Vector3.zero)),
                    ("Аэропорт", () => City.Nearest(BType.Airport, Vector3.zero)),
                    ("Вокзал", () => City.Nearest(BType.Station, Vector3.zero)),
                    ("Морской порт", () => City.Nearest(BType.Port, Vector3.zero)),
                    ("Пляж", () => City.All.Find(x => x.name.Contains("Пляжный бар"))),
                    ("Горы (ресторан «Перевал»)", () => City.All.Find(x => x.name.Contains("Перевал"))),
                    ("Торговый центр", () => City.Nearest(BType.Mall, PlayerCtl.I.transform.position)),
                    ("Автосалон", () => City.Nearest(BType.CarDealer, PlayerCtl.I.transform.position)),
                    ("Мой дом", () => Save.D.homes.Count > 0 ? City.Get(Save.D.homes[0].building) : null),
                };
                foreach (var (name, where) in dest)
                {
                    var b = where();
                    if (b == null) continue;
                    if (from != null && b == from) continue;
                    w.Row(name, b.name + " · " + b.address, "ПОЕХАЛИ", Green, () =>
                    {
                        if (!Eco.Spend(price, "Поездка")) return;
                        Window.Close();
                        Hud.FadeThrough(() =>
                        {
                            if (PlayerCtl.I.Car != null) PlayerCtl.I.ExitCar();
                            if (PlayerCtl.I.Inside >= 0) { Interiors.Clear(); PlayerCtl.I.Inside = -1; Streamer.SetOutdoor(true); }
                            PlayerCtl.I.Teleport(b.ExitPoint + b.FrontDir * 2f, 0);
                            Streamer.ForceNear();
                        }, 1.5f);
                    });
                }
            });
        }

        // ------------------------------------------------------------------ map / chat / settings

        public static void Map()
        {
            Window.Show("КАРТА ГОРОДА", w =>
            {
                var host = UIKit.Img(w.Content, UIKit.Glass, Color.white, "MapHost");
                host.gameObject.AddComponent<LayoutElement>().minHeight = 560;
                var raw = UIKit.Rect("Map", host.transform).gameObject.AddComponent<RawImage>();
                raw.texture = MapTex.Get();
                UIKit.Place(raw.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(540, 540));
                var pl = PlayerCtl.I;
                var me = pl.Inside >= 0 ? City.Get(pl.Inside).door : pl.transform.position;
                var uv = MapTex.UV(me);
                var dot = UIKit.Img(raw.transform, UIKit.Circle, UIKit.Cyan, "Me");
                UIKit.Place(dot.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(uv.x * 540, uv.y * 540), new Vector2(20, 20));
                foreach (var r in WorldNet.Remotes.Values)
                {
                    var ruv = MapTex.UV(r.WorldPos);
                    var rd = UIKit.Img(raw.transform, UIKit.Circle, UIKit.Pink, "Player");
                    UIKit.Place(rd.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(ruv.x * 540, ruv.y * 540), new Vector2(16, 16));
                }
                var legend = UIKit.Txt(host.transform,
                    "<color=#35E0FF>●</color> вы   <color=#FF3FA4>●</color> игроки\n" +
                    "<color=#F28C33>■</color> магазины  <color=#E65980>■</color> еда, отели\n" +
                    "<color=#F2D940>■</color> авто  <color=#F24040>■</color> больницы, пожарные\n" +
                    "<color=#66D980>■</color> учёба  <color=#5A8CD9>■</color> офисы, банки\n" +
                    "<color=#4CF2F2>■</color> центр занятости  <color=#8C99AD>■</color> жильё",
                    22, Color.white, TMPro.TextAlignmentOptions.TopLeft, false);
                legend.richText = true;
                UIKit.Place(legend.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(574, -14), new Vector2(460, 200));

                w.Text("Проложить маршрут (GPS):", 26, UIKit.Cyan);
                (string, BType)[] places =
                {
                    ("Центр занятости", BType.JobCenter), ("Автосалон", BType.CarDealer), ("Автомастерская", BType.Garage), ("Торговый центр", BType.Mall),
                    ("Супермаркет", BType.Supermarket), ("Кафе", BType.Cafe), ("Ресторан", BType.Restaurant), ("Больница", BType.Hospital), ("Банк", BType.Bank),
                    ("Школа", BType.School), ("Университет", BType.University), ("Библиотека", BType.Library), ("Гостиница", BType.Hotel), ("Спортзал", BType.Gym),
                    ("Магазин одежды", BType.Clothes), ("Мебельный", BType.Furniture), ("Подземный паркинг", BType.Parking), ("Заправка", BType.GasStation),
                    ("Пожарная часть", BType.FireStation), ("Завод", BType.Factory), ("Склад", BType.Warehouse), ("Вокзал", BType.Station), ("Порт", BType.Port), ("Аэропорт", BType.Airport),
                    ("Вилла", BType.Villa), ("Частный дом", BType.House), ("Небоскрёб", BType.Tower),
                };
                foreach (var (name, t) in places)
                {
                    var b = City.Nearest(t, me);
                    if (b == null) continue;
                    float dist = Vector3.Distance(me, b.door);
                    w.Row(name, b.name + " · " + Mathf.RoundToInt(dist) + " м", "МАРШРУТ", Blue, () => { Hud.SetGps(b.door, b.name); Window.Close(); }, true, MapTex.Cat(t));
                }
            }, 1200, 840);
        }

        public static void Chat()
        {
            Window.Show("ЧАТ · ИГРОКИ ОНЛАЙН", w =>
            {
                w.Text("Все игроки на Android, iPhone и в браузере находятся в одном мире.", 22, UIKit.Muted);
                var row = UIKit.Rect("InputRow", w.Content);
                row.gameObject.AddComponent<LayoutElement>().minHeight = 84;
                var input = UIKit.Input(row, "Сообщение…", 120);
                UIKit.Fill(input.GetComponent<RectTransform>(), 0, 4, 220, 4);
                var send = UIKit.Button(row, "ОТПРАВИТЬ", Green, null, 24);
                UIKit.Place(send.Bg.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(210, 76));
                send.OnClick = () => { if (!string.IsNullOrWhiteSpace(input.text)) { WorldNet.SendChat(input.text.Trim()); input.text = ""; Window.Refresh(); } };
                input.onSubmit.AddListener(_ => send.OnClick());
                foreach (var m in WorldNet.ChatLog)
                    w.Text($"<color=#{ColorUtility.ToHtmlStringRGB(m.color)}>{m.who}:</color> {m.text}", 24).richText = true;
                w.Text("Игроки рядом:", 26, UIKit.Cyan);
                if (WorldNet.Remotes.Count == 0) w.Text(WorldNet.Connected ? "Пока никого. Позовите друзей — у всех одна ссылка!" : "Подключение к серверу мира…", 24, UIKit.Muted);
                foreach (var r in WorldNet.Remotes.Values)
                {
                    float d = Vector3.Distance(r.WorldPos, PlayerCtl.I.transform.position);
                    var rr = r;
                    w.Row(r.Name, r.Place + " · " + Mathf.RoundToInt(d) + " м", "К НЕМУ", Blue, () => { Hud.SetGps(rr.WorldPos, rr.Name); Window.Close(); }, true, UIKit.Pink);
                }
            }, 1100, 780);
        }

        public static void Settings()
        {
            Window.Show("НАСТРОЙКИ", w =>
            {
                var q = new List<(string, Color, bool, System.Action)>
                {
                    ("Низкая", Grey, Gfx.Level == 0, () => { Gfx.Set(0); Window.Refresh(); }),
                    ("Средняя", Grey, Gfx.Level == 1, () => { Gfx.Set(1); Window.Refresh(); }),
                    ("Высокая", Grey, Gfx.Level == 2, () => { Gfx.Set(2); Window.Refresh(); }),
                };
                w.Chips("Графика", q);
                w.Chips("Звук", new List<(string, Color, bool, System.Action)>
                {
                    ("Вкл", Grey, Save.D.music == 1, () => { Save.D.music = 1; AudioListener.volume = 1; Window.Refresh(); }),
                    ("Выкл", Grey, Save.D.music == 0, () => { Save.D.music = 0; AudioListener.volume = 0; Window.Refresh(); }),
                });
                var row = UIKit.Rect("Name", w.Content);
                row.gameObject.AddComponent<LayoutElement>().minHeight = 84;
                var input = UIKit.Input(row, "Имя", 18);
                input.text = Save.D.name;
                UIKit.Fill(input.GetComponent<RectTransform>(), 0, 4, 220, 4);
                var ok = UIKit.Button(row, "СОХРАНИТЬ", Green, () => { if (input.text.Trim().Length >= 2) { Save.D.name = input.text.Trim(); Save.Write(); Hud.Toast("Имя изменено"); } }, 24);
                UIKit.Place(ok.Bg.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(210, 76));
                w.Text("ГАД 2-2 X · версия " + Application.version + "\nУправление на компьютере: WASD — ходьба/руль, мышь (правая кнопка) — камера, Shift — бег, Пробел — прыжок/тормоз, E — действие, F — сесть/выйти.", 22, UIKit.Muted);
            });
        }

        // ------------------------------------------------------------------ appearance

        /// <summary>Character editor: free at home / at creation, paid in clothes shops.</summary>
        public static void Wardrobe(bool paid, bool creation = false)
        {
            var look = Save.D.look.Clone();
            const int Price = 350;
            Window.Show(creation ? "СОЗДАНИЕ ПЕРСОНАЖА" : paid ? "МАГАЗИН ОДЕЖДЫ" : "ГАРДЕРОБ", w =>
            {
                void Apply() { PlayerCtl.I.Body.Rebuild(look); Window.Refresh(); }
                if (creation)
                {
                    var row = UIKit.Rect("Name", w.Content);
                    row.gameObject.AddComponent<LayoutElement>().minHeight = 84;
                    var input = UIKit.Input(row, "Ваше имя", 18);
                    input.text = Save.D.name;
                    UIKit.Fill(input.GetComponent<RectTransform>(), 0, 4, 0, 4);
                    input.onEndEdit.AddListener(s => Save.D.name = s.Trim());
                    input.onValueChanged.AddListener(s => Save.D.name = s.Trim());
                }
                w.Chips("Пол", new List<(string, Color, bool, System.Action)>
                {
                    ("Мужской", Grey, look.gender == 0, () => { look.gender = 0; Apply(); }),
                    ("Женский", Grey, look.gender == 1, () => { look.gender = 1; if (look.hairStyle == 2) look.hairStyle = 1; Apply(); }),
                });
                Colors(w, "Кожа", Palette.Skin, look.skin, i => { look.skin = i; Apply(); });
                w.Chips("Причёска", new List<(string, Color, bool, System.Action)>
                {
                    ("Короткая", Grey, look.hairStyle == 0, () => { look.hairStyle = 0; Apply(); }),
                    ("Длинная", Grey, look.hairStyle == 1, () => { look.hairStyle = 1; Apply(); }),
                    ("Лысый", Grey, look.hairStyle == 2, () => { look.hairStyle = 2; Apply(); }),
                    ("Пучок", Grey, look.hairStyle == 3, () => { look.hairStyle = 3; Apply(); }),
                });
                Colors(w, "Цвет волос", Palette.Hair, look.hair, i => { look.hair = i; Apply(); });
                w.Chips("Верх", new List<(string, Color, bool, System.Action)>
                {
                    ("Футболка", Grey, look.top == 0, () => { look.top = 0; Apply(); }),
                    ("Куртка", Grey, look.top == 1, () => { look.top = 1; Apply(); }),
                    ("Худи", Grey, look.top == 2, () => { look.top = 2; Apply(); }),
                    ("Костюм", Grey, look.top == 3, () => { look.top = 3; Apply(); }),
                });
                Colors(w, "Цвет верха", Palette.Cloth, look.shirt, i => { look.shirt = i; Apply(); });
                Colors(w, "Брюки", Palette.Cloth, look.pants, i => { look.pants = i; Apply(); });
                Colors(w, "Обувь", Palette.Cloth, look.shoes, i => { look.shoes = i; Apply(); });
                w.Chips("Головной убор", new List<(string, Color, bool, System.Action)>
                {
                    ("Нет", Grey, look.hat == 0, () => { look.hat = 0; Apply(); }),
                    ("Кепка", Grey, look.hat == 1, () => { look.hat = 1; Apply(); }),
                    ("Шапка", Grey, look.hat == 2, () => { look.hat = 2; Apply(); }),
                });
                string btn = creation ? "НАЧАТЬ ИГРУ" : paid ? "КУПИТЬ " + Hud.Money(Price) : "ГОТОВО";
                w.Row(creation ? "Готово?" : "Сохранить образ", paid ? "Новый образ сохранится" : "", btn, Green, () =>
                {
                    if (creation && (Save.D.name == null || Save.D.name.Length < 2)) { Hud.Toast("Введите имя (минимум 2 буквы)"); return; }
                    if (paid && !Eco.Spend(Price, "Новый образ")) return;
                    Save.D.look = look.Clone();
                    if (creation) Save.D.created = true;
                    Save.Write();
                    PlayerCtl.I.Body.Rebuild(Save.D.look);
                    WorldNet.LookChanged();
                    Window.Close();
                    if (creation) Hud.Toast("Добро пожаловать в ГАД 2-2 X, " + Save.D.name + "!", UIKit.Cyan);
                });
            }, 1180, 860).OnClose = () => { if (PlayerCtl.I != null) PlayerCtl.I.Body.Rebuild(Save.D.look); if (creation && !Save.D.created) { Save.D.created = true; if (string.IsNullOrEmpty(Save.D.name)) Save.D.name = "Игрок" + Random.Range(100, 999); Save.Write(); } };
        }

        static void Colors(Window w, string title, Color[] pal, int sel, System.Action<int> pick)
        {
            var chips = new List<(string, Color, bool, System.Action)>();
            for (int i = 0; i < pal.Length; i++) { int k = i; chips.Add(("", pal[i], sel % pal.Length == i, () => pick(k))); }
            w.Chips(title, chips);
        }

        /// <summary>Furniture: buy and recolour items for a home.</summary>
        public static void Decor(OwnedHome home = null)
        {
            if (home == null)
            {
                if (Save.D.homes.Count == 0) { Hud.Toast("Сначала купите жильё — мебель доставят туда"); return; }
                home = Save.D.homes[0];
            }
            var h = home;
            var hb = City.Get(h.building);
            Window.Show("МЕБЕЛЬ · " + (hb != null ? hb.name : ""), w =>
            {
                w.Text("Покупки доставляются в: " + (hb != null ? hb.name + (h.unit > 0 ? ", кв. " + h.unit : "") : ""), 24, UIKit.Muted);
                foreach (var d in DecorDef.All)
                {
                    var def = d;
                    var have = h.decor.Find(s => s.StartsWith(d.Slot + ":"));
                    if (have == null)
                        w.Row(d.Title, "", Hud.Money(d.Price), Green, () => { if (Eco.Spend(def.Price, def.Title)) { h.decor.Add(def.Slot + ":" + Random.Range(0, Palette.Cloth.Length)); Save.Write(); Window.Refresh(); RefreshHome(h); } });
                    else
                    {
                        int.TryParse(have.Substring(have.IndexOf(':') + 1), out int cur);
                        var chips = new List<(string, Color, bool, System.Action)>();
                        for (int i = 0; i < Palette.Cloth.Length; i++)
                        {
                            int k = i;
                            chips.Add(("", Palette.Cloth[i], cur == i, () => { h.decor.Remove(have); h.decor.Add(def.Slot + ":" + k); Save.Write(); Window.Refresh(); RefreshHome(h); }));
                        }
                        w.Chips(d.Title + " (есть) — цвет", chips);
                    }
                }
            });
        }

        static void RefreshHome(OwnedHome h)
        {
            if (Interiors.Current != null && Interiors.Current.id == h.building && Interiors.Unit == h.unit)
            {
                var pos = PlayerCtl.I.transform.position;
                Interiors.Build(Interiors.Current, h.unit);
                PlayerCtl.I.Teleport(pos, PlayerCtl.I.transform.eulerAngles.y);
            }
        }
    }
}
