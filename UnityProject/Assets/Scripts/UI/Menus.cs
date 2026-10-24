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
                w.Row("Скины", "Готовые образы: бизнесмен, киберпанк, инопланетянин…", "СКИНЫ", new Color(0.55f, 0.3f, 0.9f), Skins);
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
                if (b.type == BType.Apartment || b.type == BType.Tower) w.Row("Квартиры в этом доме", "", "СМОТРЕТЬ", Blue, () => Flats(b));
            });
        }

        // ------------------------------------------------------------------ property

        /// <summary>The lift's panel: one button per floor.</summary>
        public static void Lift(Bld b, Elevator lift)
        {
            Window.Show("ЛИФТ · " + b.name.ToUpper(), w =>
            {
                int top = Interiors.TopFloor(b);
                w.Text("Сейчас: " + Elevator.FloorName(lift.Floor) + ". Выберите этаж:", 26, UIKit.Muted);
                var chips = new List<(string, Color, bool, System.Action)>();
                for (int f = 0; f <= top; f++)
                {
                    int fl = f;
                    string label = f == 0 ? "Лобби" : (f + 1).ToString();
                    if (b.type == BType.Agency && f == top) label = "Lounge";
                    bool mine = Save.D.homes.Exists(h => h.building == b.id && h.unit / 10 == f);
                    chips.Add((label, mine ? new Color(0.15f, 0.6f, 0.3f) : new Color(0.2f, 0.25f, 0.35f), f == lift.Floor, () => { Window.Close(); lift.Go(fl); }));
                }
                w.Chips("Этажи (зелёный — там ваша квартира)", chips);
            }, 1180, 720);
        }

        /// <summary>The concierge: every flat in this building, by floor.</summary>
        public static void Flats(Bld b)
        {
            Window.Show(b.name.ToUpper() + " · КВАРТИРЫ", w =>
            {
                w.Text("На каждом этаже 5 квартир. Свободные можно осмотреть и купить, свою — обустроить. Поднимайтесь на лифте!", 24, UIKit.Muted);
                int top = Interiors.TopFloor(b);
                for (int f = 1; f <= top; f++)
                    for (int k = 1; k <= 5; k++)
                    {
                        int unit = f * 10 + k;
                        int st = Interiors.FlatStatus(b, unit);
                        if (st == 0) continue;
                        int fl = f;
                        w.Row(((b.type == BType.Tower) ? "Апартаменты " : "Квартира ") + ((f + 1) * 100 + k) + (st == 2 ? "  (МОЯ)" : ""),
                              $"{f + 1} этаж · {(st == 2 ? "ваша" : Hud.Money(b.UnitPrice(unit)))}", "НА ЭТАЖ", st == 2 ? Green : Blue, () =>
                        {
                            Window.Close();
                            Hud.FadeThrough(() =>
                            {
                                var lift = Elevator.I;
                                if (lift != null) { Object.Destroy(lift.gameObject); }
                                Interiors.Enter(b, fl);
                            }, 2.5f);
                        });
                    }
            });
        }

        /// <summary>Real-estate agency: every home in the city for sale.</summary>
        public static void RealEstate()
        {
            int tab = 0;
            Window.Show("GOD CITY · НЕДВИЖИМОСТЬ", w =>
            {
                var tabs = new List<(string, Color, bool, System.Action)>
                {
                    ("Квартиры", Blue, tab == 0, () => { tab = 0; Window.Refresh(); }),
                    ("Пентхаусы", Blue, tab == 1, () => { tab = 1; Window.Refresh(); }),
                    ("Дома", Blue, tab == 2, () => { tab = 2; Window.Refresh(); }),
                    ("Виллы", Blue, tab == 3, () => { tab = 3; Window.Refresh(); }),
                };
                w.Chips("Что ищем?", tabs);
                var type = tab switch { 0 => BType.Apartment, 1 => BType.Tower, 2 => BType.House, _ => BType.Villa };
                var me = PlayerCtl.I.Inside >= 0 ? City.Get(PlayerCtl.I.Inside).door : PlayerCtl.I.transform.position;
                var list = City.OfType(type);
                list.Sort((x, y) => (x.door - me).sqrMagnitude.CompareTo((y.door - me).sqrMagnitude));
                int shown = 0;
                foreach (var bb in list)
                {
                    if (shown >= 25) break;
                    var bld = bb;
                    if (type == BType.Apartment || type == BType.Tower)
                    {
                        int free = 0, first = -1, top = Interiors.TopFloor(bb);
                        for (int f = type == BType.Tower ? bb.floors / 2 + 1 : 1; f <= top; f++)
                            for (int k = 1; k <= 5; k++) if (Interiors.FlatStatus(bb, f * 10 + k) == 1) { free++; if (first < 0) first = f * 10 + k; }
                        if (free == 0) continue;
                        int unit = first;
                        w.Row(bb.name, $"{bb.address} · свободно {free} · от {Hud.Money(bb.UnitPrice(unit))}", "КУПИТЬ", Green, () =>
                        {
                            Window.Close();
                            if (!Eco.Spend(bld.UnitPrice(unit), "Покупка через агентство")) return;
                            Save.D.homes.Add(new OwnedHome { building = bld.id, unit = unit });
                            Save.Write();
                            Hud.SetGps(bld.door, bld.name);
                            Hud.Toast($"Квартира {(unit / 10 + 1) * 100 + unit % 10} на {unit / 10 + 1} этаже — ваша! Маршрут проложен", new Color(0.5f, 1f, 0.6f));
                        }, Save.D.money >= bb.UnitPrice(unit), MapTex.Cat(type));
                    }
                    else
                    {
                        bool own = Eco.OwnsHome(bb.id, 0);
                        w.Row(bb.name + (own ? "  (ВАШ)" : ""), $"{bb.address} · {Hud.Money(bb.UnitPrice(0))}", own ? "GPS" : "КУПИТЬ", own ? Blue : Green, () =>
                        {
                            Window.Close();
                            if (!own)
                            {
                                if (!Eco.Spend(bld.UnitPrice(0), "Покупка через агентство")) return;
                                Save.D.homes.Add(new OwnedHome { building = bld.id, unit = 0 });
                                Save.Write();
                                Hud.Toast("Поздравляем с покупкой: " + bld.name + "!", new Color(0.5f, 1f, 0.6f));
                            }
                            Hud.SetGps(bld.door, bld.name);
                        }, own || Save.D.money >= bb.UnitPrice(0), MapTex.Cat(type));
                    }
                    shown++;
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
                            if (PlayerCtl.I.Car != null) PlayerCtl.I.ExitCarInstant();
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
                w.Text("GOD 2-2X · версия " + Application.version + "\nУправление на компьютере: WASD — ходьба/руль, мышь (правая кнопка) — камера, Shift — бег, Пробел — прыжок/тормоз, E — действие, F — сесть/выйти.", 22, UIKit.Muted);
            });
        }

        // ------------------------------------------------------------------ appearance

        static readonly (Wardrobe.Slot slot, string name)[] Depts =
        {
            (Wardrobe.Slot.HGender, "Пол"), (Wardrobe.Slot.HFace, "Форма лица"), (Wardrobe.Slot.HSkin, "Кожа"), (Wardrobe.Slot.HHair, "Причёска"), (Wardrobe.Slot.HEyes, "Глаза"),
            (Wardrobe.Slot.HMouth, "Лицо"), (Wardrobe.Slot.HTop, "Верх"), (Wardrobe.Slot.HBottom, "Низ"), (Wardrobe.Slot.HShoes, "Обувь"), (Wardrobe.Slot.HAcc, "Аксессуары"),
        };

        /// <summary>First launch: the player must choose a nickname before anything else.</summary>
        public static void Nick()
        {
            var w = Window.Show("ДОБРО ПОЖАЛОВАТЬ В GOD 2-2X", win =>
            {
                win.Text("Придумайте ник — его увидят другие игроки над вашей головой и в чате.", 28);
                var row = UIKit.Rect("Nick", win.Content);
                row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
                var input = UIKit.Input(row, "Ваш ник (3–16 символов)", 16, 38);
                UIKit.Fill(input.GetComponent<RectTransform>(), 0, 6, 0, 6);
                input.text = Save.D.name ?? "";
                var info = win.Text("", 24, UIKit.Muted);
                var go = win.Row("Готово", "Дальше — внешность персонажа", "ДАЛЕЕ", Green, null);
                go.OnClick = () =>
                {
                    var n = (input.text ?? "").Trim().Replace("|", "");
                    if (n.Length < 3) { info.text = "Ник слишком короткий — минимум 3 символа"; info.color = UIKit.Red; AudioFx.Play("error"); return; }
                    Save.D.name = n;
                    Save.Write();
                    Window.Close();
                    Dress(false, true);
                };
                input.onSubmit.AddListener(_ => go.OnClick());
                input.Select(); input.ActivateInputField();
            }, 1000, 560);
            var close = w.GetComponentInChildren<UIButton>(true);
            foreach (var b in w.GetComponentsInChildren<UIButton>(true)) if (b.name == "Close") b.gameObject.SetActive(false);
            w.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            _ = close;
        }

        /// <summary>
        /// Character editor (free at home / at creation, paid in clothes shops): a live 3D preview of
        /// the character on the left, the clothes themselves as pictures on the right.
        /// </summary>
        public static void Dress(bool paid, bool creation = false)
        {
            var look = Save.D.skin.Clone();
            const int Price = 350;
            int dept = creation ? 0 : 5;
            var studio = Studio.Get();
            studio.SetLive(look);
            var w = Window.Show(creation ? "СОЗДАНИЕ ПЕРСОНАЖА · " + Save.D.name : paid ? "МАГАЗИН ОДЕЖДЫ" : "ГАРДЕРОБ", win =>
            {
                void Apply() { PlayerCtl.I.Body.Rebuild(look); studio.SetLive(look); Window.Refresh(); }
                string btn = creation ? "НАЧАТЬ ИГРУ" : paid ? "КУПИТЬ " + Hud.Money(Price) : "ГОТОВО";
                win.Row(creation ? "Готово?" : "Сохранить образ", creation ? "Готовые образы — в Телефон → Скины" : paid ? "Новый образ сохранится" : "", btn, Green, () =>
                {
                    if (creation && (Save.D.name == null || Save.D.name.Length < 3)) { Window.Close(); Nick(); return; }
                    if (paid && !Eco.Spend(Price, "Новый образ")) return;
                    Save.D.skin = look.Clone();
                    if (creation) Save.D.created = true;
                    Save.Write();
                    PlayerCtl.I.Body.Rebuild(Save.D.skin);
                    WorldNet.LookChanged();
                    Window.Close();
                    if (creation) Hud.Toast("Добро пожаловать в GOD 2-2X, " + Save.D.name + "!", UIKit.Cyan);
                });
                var tabs = new List<(string, Color, bool, System.Action)>();
                for (int i = 0; i < Depts.Length; i++) { int k = i; tabs.Add((Depts[i].name, new Color(0.2f, 0.4f, 0.75f), dept == i, () => { dept = k; Window.Refresh(); })); }
                win.Chips("Раздел", tabs);
                var slot = Depts[dept].slot;
                var cells = new List<(string id, string label, bool sel, System.Action act, SkinSet with)>();
                foreach (var d in global::Gad.Wardrobe.OfSlot(slot))
                {
                    if (slot == global::Gad.Wardrobe.Slot.HTop && d.style == "kit") continue;
                    if (slot == global::Gad.Wardrobe.Slot.HSkin && (d.style == "gold" || d.id == "hs_alien") && !creation && !Save.D.skins.Contains(d.id)) continue;
                    var def = d;
                    var with = look.Clone();
                    with.Set(slot, d.id);
                    if (slot == global::Gad.Wardrobe.Slot.HTop && d.style == "dress") with.hBottom = "hb_none";
                    cells.Add((d.id, d.name, look.Get(slot) == d.id, () => { look.Set(slot, def.id); if (slot == global::Gad.Wardrobe.Slot.HTop && def.style == "dress") look.hBottom = "hb_none"; Apply(); }, with));
                }
                win.ItemGrid(cells, slot, look);
            }, 1560, 880);
            // the live preview stands on the left of the window
            var panel = w.transform.GetChild(0) as RectTransform;
            var view = panel.Find("Viewport") as RectTransform;
            if (view != null) view.offsetMin = new Vector2(440, view.offsetMin.y);
            var raw = UIKit.Rect("Preview", panel).gameObject.AddComponent<RawImage>();
            raw.texture = studio.LiveRT;
            UIKit.Place(raw.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, -10), new Vector2(400, 667));
            w.OnClose = () =>
            {
                Studio.Get().Hide();
                if (PlayerCtl.I != null) PlayerCtl.I.Body.Rebuild(Save.D.skin);
                if (creation && !Save.D.created) { Save.D.created = true; if (string.IsNullOrEmpty(Save.D.name)) Save.D.name = "Игрок" + Random.Range(100, 999); Save.Write(); }
            };
        }

        /// <summary>Ready-made looks: tap to wear (bought ones stay yours).</summary>
        public static readonly (string id, string name, int price, SkinSet look)[] SkinPresets =
        {
            ("city", "Горожанин", 0, new SkinSet { hHair = "hh_short_brown", hTop = "ht_tee_white", hBottom = "hb_jeans_blue", hShoes = "hf_sneakers_white" }),
            ("city_f", "Горожанка", 0, new SkinSet { hGender = "hg_f", hHair = "hh_long_brown", hEyes = "he_lashes", hMouth = "hm_lips", hTop = "ht_print_heart", hBottom = "hb_jeans_dark", hShoes = "hf_sneakers_white" }),
            ("boss", "Бизнесмен", 1500, new SkinSet { hHair = "hh_short_black", hTop = "ht_suit_navy", hBottom = "hb_pants_navy", hShoes = "hf_shoes_black", hAcc = "ha_shades_gold", hMouth = "hm_smirk" }),
            ("boss_f", "Бизнес-леди", 1500, new SkinSet { hGender = "hg_f", hHair = "hh_bun_black", hEyes = "he_lashes", hMouth = "hm_lips", hTop = "ht_suit_grey", hBottom = "hb_skirt_black", hShoes = "hf_shoes_black" }),
            ("cyber", "Киберпанк", 2500, new SkinSet { hSkin = "hs_peach", hHair = "hh_mohawk_blue", hEyes = "he_angry", hTop = "ht_hoodie_black", hBottom = "hb_joggers_black", hShoes = "hf_hightop_black", hAcc = "ha_sunglasses" }),
            ("cyber_f", "Неон-девушка", 2500, new SkinSet { hGender = "hg_f", hHair = "hh_ponytail_pink", hEyes = "he_big", hMouth = "hm_lips", hTop = "ht_hoodie_pink", hBottom = "hb_skirt_black", hShoes = "hf_sneakers_neon", hAcc = "ha_headphones_pink" }),
            ("sport", "Спортсмен", 800, new SkinSet { hSkin = "hs_tan", hHair = "hh_buzz_black", hTop = "ht_tank_white", hBottom = "hb_shorts_black", hShoes = "hf_sneakers_red" }),
            ("street", "Уличный стиль", 900, new SkinSet { hSkin = "hs_brown", hHair = "hh_afro_black", hTop = "ht_print_bolt", hBottom = "hb_cargo_khaki", hShoes = "hf_hightop_red", hAcc = "ha_headphones_black" }),
            ("worker", "Рабочий", 300, new SkinSet { hHair = "hh_short_blonde", hMouth = "hm_beard", hTop = "ht_flannel_red", hBottom = "hb_cargo_olive", hShoes = "hf_boots_brown" }),
            ("student", "Студентка", 600, new SkinSet { hGender = "hg_f", hHair = "hh_bangs_red", hEyes = "he_lashes", hMouth = "hm_smile", hTop = "ht_shirt_white", hBottom = "hb_skirt_plaid", hShoes = "hf_shoes_brown", hAcc = "ha_glasses_round" }),
            ("kaz_man", "Казах в чапане", 3000, new SkinSet { hSkin = "hs_tan", hHair = "hh_short_black", hTop = "ht_chapan_blue", hBottom = "hb_pants_black", hShoes = "hf_sapogi_black", hAcc = "ha_kalpak" }),
            ("kaz_girl", "Казашка в саукеле", 3000, new SkinSet { hGender = "hg_f", hSkin = "hs_tan", hHair = "hh_long_black", hEyes = "he_lashes", hMouth = "hm_lips", hTop = "ht_koylek", hBottom = "hb_none", hShoes = "hf_shoes_brown", hAcc = "ha_saukele" }),
            ("alien", "Инопланетянин", 12000, new SkinSet { hSkin = "hs_alien", hHair = "hh_none", hEyes = "he_big", hMouth = "hm_open", hTop = "ht_tee_black", hBottom = "hb_joggers_black", hShoes = "hf_sneakers_neon" }),
            ("gold", "Золотой человек", 50000, new SkinSet { hSkin = "hs_gold", hHair = "hh_none", hEyes = "he_happy", hTop = "ht_none", hBottom = "hb_none", hShoes = "hf_none" }),
        };

        public static void Skins()
        {
            Window.Show("СКИНЫ", w =>
            {
                w.Text("Готовые образы. Купленный скин остаётся вашим — надевайте в любой момент. Свой образ — в гардеробе дома или в магазине одежды.", 22, UIKit.Muted);
                foreach (var p in SkinPresets)
                {
                    var preset = p;
                    bool own = p.price == 0 || Save.D.skins.Contains(p.id);
                    bool wearing = Save.D.skin.Encode() == p.look.Encode();
                    w.Row(p.name + (wearing ? "  (надет)" : ""), own ? "Ваш скин" : "Цена " + Hud.Money(p.price), own ? "НАДЕТЬ" : "КУПИТЬ", own ? Green : Orange, () =>
                    {
                        if (!own) { if (!Eco.Spend(preset.price, "Скин: " + preset.name)) return; Save.D.skins.Add(preset.id); if (preset.look.hSkin == "hs_gold" || preset.look.hSkin == "hs_alien") Save.D.skins.Add(preset.look.hSkin); }
                        Save.D.skin = preset.look.Clone();
                        Save.Write();
                        PlayerCtl.I.Body.Rebuild(Save.D.skin);
                        WorldNet.LookChanged();
                        Window.Refresh();
                    }, true, Wardrobe_Color(p.look));
                }
            });
        }

        static Color Wardrobe_Color(SkinSet s) => global::Gad.Wardrobe.Find(s.hTop)?.color ?? Grey;

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
