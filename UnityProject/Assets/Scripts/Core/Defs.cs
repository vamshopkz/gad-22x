using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    public enum BType
    {
        Apartment, House, Villa, Tower, Office, Shop, Supermarket, Mall, CarDealer, Garage, Restaurant, Cafe, Hotel,
        Hospital, FireStation, School, University, Library, Factory, Warehouse, Bank, JobCenter, GasStation, Parking,
        Airport, Station, Port, Clothes, Furniture, Gym, Agency, Plot, GunShop, Corp
    }

    public enum District { Downtown, Business, Residential, Suburb, Beach, Industrial, Park, Outskirts }

    /// <summary>What a kind of building is and what can be done in it.</summary>
    public class BInfo
    {
        public string Title;          // «Супермаркет»
        public string[] Names;        // brand names for signs
        public int BizPrice;          // >0: can be bought as a business
        public int HomePrice;         // >0: can be bought as a home (apartments: per flat)
        public Color Sign = new Color(0.15f, 0.5f, 0.95f);

        public static readonly Dictionary<BType, BInfo> All = new Dictionary<BType, BInfo>
        {
            [BType.Apartment] = new BInfo { Title = "Жилой дом", Names = new[] { "ЖК «Северный»", "ЖК «Парковый»", "ЖК «Солнечный»", "ЖК «Новый квартал»", "ЖК «Звёздный»", "ЖК «Акация»" }, HomePrice = 35000, Sign = new Color(0.3f, 0.6f, 0.35f) },
            [BType.GunShop] = new BInfo { Title = "Оружейный магазин", Names = new[] { "Оружие «Кольт»", "Тир «Меткий глаз»" }, BizPrice = 150000, Sign = new Color(0.55f, 0.55f, 0.6f) },
            [BType.Corp] = new BInfo { Title = "Офисный небоскрёб", Names = new[] { "Herbal Tower", "Herbal Plaza" }, BizPrice = 0, Sign = new Color(0.3f, 0.85f, 0.45f) },
            [BType.Plot] = new BInfo { Title = "Участок под дом", Names = new[] { "Участок" }, Sign = new Color(0.2f, 0.7f, 0.3f) },
            [BType.House] = new BInfo { Title = "Частный дом", Names = new[] { "Дом" }, HomePrice = 90000, Sign = new Color(0.55f, 0.4f, 0.25f) },
            [BType.Villa] = new BInfo { Title = "Вилла", Names = new[] { "Вилла «Бриз»", "Вилла «Лагуна»", "Вилла «Ривьера»", "Вилла «Палм»" }, HomePrice = 420000, Sign = new Color(0.85f, 0.65f, 0.2f) },
            [BType.Tower] = new BInfo { Title = "Небоскрёб", Names = new[] { "X-Tower", "Сити Плаза", "Гранд Тауэр", "Неон Сити", "Скай Хаб" }, HomePrice = 650000, BizPrice = 900000, Sign = new Color(0.2f, 0.75f, 1f) },
            [BType.Office] = new BInfo { Title = "Бизнес-центр", Names = new[] { "БЦ «Альфа»", "БЦ «Орбита»", "БЦ «Меридиан»", "БЦ «Вектор»", "БЦ «Пульс»" }, BizPrice = 260000, Sign = new Color(0.25f, 0.45f, 0.8f) },
            [BType.Shop] = new BInfo { Title = "Магазин", Names = new[] { "Продукты 24", "Мини-маркет «Угол»", "Лавка", "Магазин «Ромашка»" }, BizPrice = 45000, Sign = new Color(0.95f, 0.45f, 0.15f) },
            [BType.Supermarket] = new BInfo { Title = "Супермаркет", Names = new[] { "Супермаркет «Магнум»", "Гипер «Корзина»", "Супермаркет «Свежий»" }, BizPrice = 140000, Sign = new Color(0.9f, 0.2f, 0.2f) },
            [BType.Mall] = new BInfo { Title = "Торговый центр", Names = new[] { "ТРЦ «Мега»", "ТЦ «Галактика»", "ТРЦ «Атмосфера»" }, BizPrice = 520000, Sign = new Color(0.75f, 0.25f, 0.85f) },
            [BType.CarDealer] = new BInfo { Title = "Автосалон", Names = new[] { "Автосалон «Драйв»", "Мотор Плаза", "Автосалон «Турбо»" }, BizPrice = 300000, Sign = new Color(0.1f, 0.3f, 0.6f) },
            [BType.Garage] = new BInfo { Title = "Автомастерская", Names = new[] { "СТО «Гараж»", "Автосервис «Ключ»", "Тюнинг «Форсаж»" }, BizPrice = 70000, Sign = new Color(0.95f, 0.75f, 0.1f) },
            [BType.Restaurant] = new BInfo { Title = "Ресторан", Names = new[] { "Ресторан «Панорама»", "Ресторан «Самарканд»", "Гриль «Огонь»", "Ресторан «Море»" }, BizPrice = 120000, Sign = new Color(0.7f, 0.15f, 0.2f) },
            [BType.Cafe] = new BInfo { Title = "Кафе", Names = new[] { "Кофейня «Зерно»", "Кафе «Уют»", "Бургерная", "Пиццерия «Мама»" }, BizPrice = 40000, Sign = new Color(0.55f, 0.35f, 0.2f) },
            [BType.Hotel] = new BInfo { Title = "Гостиница", Names = new[] { "Отель «Империал»", "Гостиница «Волна»", "Отель «Мираж»" }, BizPrice = 380000, Sign = new Color(0.85f, 0.7f, 0.3f) },
            [BType.Hospital] = new BInfo { Title = "Больница", Names = new[] { "Городская больница", "Клиника «Здоровье»" }, Sign = new Color(0.9f, 0.15f, 0.15f) },
            [BType.FireStation] = new BInfo { Title = "Пожарная часть", Names = new[] { "Пожарная часть №1", "Пожарная часть №2" }, Sign = new Color(0.85f, 0.1f, 0.1f) },
            [BType.School] = new BInfo { Title = "Школа", Names = new[] { "Школа №1", "Школа №7", "Гимназия №3" }, Sign = new Color(0.2f, 0.55f, 0.85f) },
            [BType.University] = new BInfo { Title = "Университет", Names = new[] { "Университет Будущего", "Политех" }, Sign = new Color(0.25f, 0.25f, 0.6f) },
            [BType.Library] = new BInfo { Title = "Библиотека", Names = new[] { "Городская библиотека", "Библиотека им. Абая" }, Sign = new Color(0.4f, 0.3f, 0.2f) },
            [BType.Factory] = new BInfo { Title = "Завод", Names = new[] { "Завод «Техмаш»", "Фабрика «Прогресс»", "Завод «Электрон»" }, BizPrice = 450000, Sign = new Color(0.5f, 0.5f, 0.5f) },
            [BType.Warehouse] = new BInfo { Title = "Склад", Names = new[] { "Склад «Логистик»", "Терминал «Груз»", "Склад №4" }, BizPrice = 90000, Sign = new Color(0.6f, 0.45f, 0.2f) },
            [BType.Bank] = new BInfo { Title = "Банк", Names = new[] { "Банк «Капитал»", "Банк «Будущее»" }, Sign = new Color(0.1f, 0.55f, 0.35f) },
            [BType.JobCenter] = new BInfo { Title = "Центр занятости", Names = new[] { "Центр занятости" }, Sign = new Color(0.15f, 0.6f, 0.8f) },
            [BType.GasStation] = new BInfo { Title = "Заправка", Names = new[] { "АЗС «Петрол»", "АЗС «Газ+»" }, BizPrice = 110000, Sign = new Color(0.1f, 0.65f, 0.3f) },
            [BType.Parking] = new BInfo { Title = "Подземная парковка", Names = new[] { "Паркинг P1", "Паркинг P2", "Паркинг «Центр»" }, BizPrice = 150000, Sign = new Color(0.15f, 0.4f, 0.9f) },
            [BType.Airport] = new BInfo { Title = "Аэропорт", Names = new[] { "Аэропорт Herbal" }, Sign = new Color(0.2f, 0.5f, 0.9f) },
            [BType.Station] = new BInfo { Title = "Вокзал", Names = new[] { "Центральный вокзал" }, Sign = new Color(0.35f, 0.35f, 0.75f) },
            [BType.Port] = new BInfo { Title = "Морской порт", Names = new[] { "Морской порт" }, Sign = new Color(0.1f, 0.4f, 0.7f) },
            [BType.Clothes] = new BInfo { Title = "Магазин одежды", Names = new[] { "Бутик «Стиль»", "Одежда «Тренд»", "Мода Плюс" }, BizPrice = 75000, Sign = new Color(0.9f, 0.3f, 0.6f) },
            [BType.Furniture] = new BInfo { Title = "Мебельный", Names = new[] { "Мебель «Дом»", "Интерьер Центр" }, BizPrice = 85000, Sign = new Color(0.6f, 0.45f, 0.3f) },
            [BType.Agency] = new BInfo { Title = "Агентство недвижимости", Names = new[] { "Herbal City · Недвижимость" }, Sign = new Color(0.2f, 0.9f, 1f) },
            [BType.Gym] = new BInfo { Title = "Спортзал", Names = new[] { "Фитнес «Сила»", "Спортзал «Атлет»" }, BizPrice = 65000, Sign = new Color(0.95f, 0.35f, 0.1f) },
        };
    }

    /// <summary>A job. Kind: 0 shift inside the building, 1 courier (walk/drive to a door), 2 taxi (needs a car).</summary>
    public class JobDef
    {
        public string Id, Title;
        public BType Where;
        public int Pay, Kind, Edu;   // pay per shift / per delivery; education needed (0 none, 1 school, 2 university)
        public float Seconds = 12f;

        public static readonly List<JobDef> All = new List<JobDef>
        {
            new JobDef { Id = "cashier", Title = "Продавец-кассир", Where = BType.Supermarket, Pay = 220 },
            new JobDef { Id = "shopseller", Title = "Продавец", Where = BType.Shop, Pay = 180 },
            new JobDef { Id = "waiter", Title = "Официант", Where = BType.Restaurant, Pay = 250 },
            new JobDef { Id = "cook", Title = "Повар", Where = BType.Restaurant, Pay = 340, Edu = 1 },
            new JobDef { Id = "barista", Title = "Бариста", Where = BType.Cafe, Pay = 200 },
            new JobDef { Id = "mechanic", Title = "Автомеханик", Where = BType.Garage, Pay = 380, Edu = 1 },
            new JobDef { Id = "carseller", Title = "Менеджер автосалона", Where = BType.CarDealer, Pay = 480, Edu = 1 },
            new JobDef { Id = "nurse", Title = "Медсестра / медбрат", Where = BType.Hospital, Pay = 420, Edu = 1 },
            new JobDef { Id = "doctor", Title = "Врач", Where = BType.Hospital, Pay = 900, Edu = 2 },
            new JobDef { Id = "firefighter", Title = "Пожарный", Where = BType.FireStation, Pay = 520, Edu = 1 },
            new JobDef { Id = "teacher", Title = "Учитель", Where = BType.School, Pay = 450, Edu = 2 },
            new JobDef { Id = "librarian", Title = "Библиотекарь", Where = BType.Library, Pay = 260 },
            new JobDef { Id = "worker", Title = "Рабочий завода", Where = BType.Factory, Pay = 330 },
            new JobDef { Id = "engineer", Title = "Инженер", Where = BType.Factory, Pay = 820, Edu = 2 },
            new JobDef { Id = "loader", Title = "Кладовщик", Where = BType.Warehouse, Pay = 280 },
            new JobDef { Id = "clerk", Title = "Офисный сотрудник", Where = BType.Office, Pay = 560, Edu = 1 },
            new JobDef { Id = "manager", Title = "Менеджер проектов", Where = BType.Tower, Pay = 1100, Edu = 2 },
            new JobDef { Id = "banker", Title = "Банковский служащий", Where = BType.Bank, Pay = 700, Edu = 2 },
            new JobDef { Id = "receptionist", Title = "Администратор отеля", Where = BType.Hotel, Pay = 380, Edu = 1 },
            new JobDef { Id = "trainer", Title = "Фитнес-тренер", Where = BType.Gym, Pay = 360 },
            new JobDef { Id = "docker", Title = "Портовый грузчик", Where = BType.Port, Pay = 400 },
            new JobDef { Id = "courier", Title = "Курьер (доставка посылок)", Where = BType.Warehouse, Pay = 260, Kind = 1 },
            new JobDef { Id = "foodcourier", Title = "Доставка еды", Where = BType.Cafe, Pay = 190, Kind = 1 },
            new JobDef { Id = "taxi", Title = "Таксист (нужна машина)", Where = BType.JobCenter, Pay = 420, Kind = 2 },
        };

        public static JobDef Get(string id) => All.Find(j => j.Id == id);
    }

    /// <summary>Car catalogue: real-world proportions for every model (see CarMesh).</summary>
    public class CarDef
    {
        public string Id, Title, Sub;
        public int Price;
        public float MaxSpeed, Accel;
        public CarSpec Spec;
        public int ForceColor = -1;
        public int Shape => 1;
        public float L => Spec.L;
        public float W => Spec.W;

        public static readonly List<CarDef> All = new List<CarDef>
        {
            new CarDef { Id = "mini", Title = "Ситикар Мини", Sub = "городской хэтчбек", Price = 9000, MaxSpeed = 30, Accel = 6,
                Spec = new CarSpec { L = 3.85f, W = 1.75f, H = 1.48f, WB = 2.5f, R = 0.31f, FrontOH = 0.72f, HoodY = 0.9f, DeckY = 0.95f, NoseY = 0.62f, TailY = 0.78f, TailBottom = 0.3f,
                    ZA = 0.72f, ZB = 0.12f, ZC = -1.35f, ZD = -1.78f, Tumble = 0.82f, Grille = 2, Head = 1, Tail = 2, Exhaust = 1 } },
            new CarDef { Id = "sedan", Title = "Седан Комфорт", Sub = "семейный седан", Price = 18000, MaxSpeed = 36, Accel = 7,
                Spec = new CarSpec { L = 4.65f, W = 1.82f, H = 1.45f, WB = 2.75f, R = 0.32f, FrontOH = 0.9f, HoodY = 0.93f, DeckY = 0.98f, NoseY = 0.58f, TailY = 0.66f,
                    ZA = 0.5f, ZB = -0.08f, ZC = -0.82f, ZD = -1.3f, Grille = 2, Head = 3, Tail = 1, Exhaust = 1 } },
            new CarDef { Id = "taxi", Title = "Седан Такси", Sub = "для работы в такси", Price = 16000, MaxSpeed = 35, Accel = 7, ForceColor = 5,
                Spec = new CarSpec { L = 4.65f, W = 1.82f, H = 1.45f, WB = 2.75f, R = 0.32f, FrontOH = 0.9f, HoodY = 0.93f, DeckY = 0.98f, NoseY = 0.58f, TailY = 0.66f,
                    ZA = 0.5f, ZB = -0.08f, ZC = -0.82f, ZD = -1.3f, Grille = 2, Head = 3, Tail = 1, Exhaust = 1, Taxi = true } },
            new CarDef { Id = "bmw_g30", Title = "BMW 5 Series (G30) 540i", Sub = "бизнес-седан · M Sport", Price = 65000, MaxSpeed = 52, Accel = 11.5f,
                Spec = new CarSpec { L = 4.94f, W = 1.87f, H = 1.47f, WB = 2.975f, R = 0.335f, FrontOH = 0.86f, HoodY = 0.97f, DeckY = 1.02f, NoseY = 0.62f, NoseBottom = 0.16f, TailY = 0.7f, TailBottom = 0.3f,
                    ZA = 0.62f, ZB = 0.02f, ZC = -0.78f, ZD = -1.32f, Tumble = 0.79f, NoseNarrow = 0.78f, Grille = 1, Head = 0, Tail = 0, Badge = true, Exhaust = 2, Spokes = 5,
                    Caliper = new Color(0.1f, 0.28f, 0.8f) } },
            new CarDef { Id = "pickup", Title = "Пикап Ранчо", Sub = "полноразмерный пикап", Price = 26000, MaxSpeed = 34, Accel = 7,
                Spec = new CarSpec { L = 5.3f, W = 2.0f, H = 1.86f, WB = 3.3f, R = 0.4f, FrontOH = 0.95f, Sill = 0.4f, HoodY = 1.22f, DeckY = 1.2f, NoseY = 0.88f, NoseBottom = 0.38f, TailY = 1.0f, TailBottom = 0.55f,
                    ZA = 1.0f, ZB = 0.45f, ZC = -0.45f, ZD = -0.72f, Tumble = 0.9f, Bed = true, Grille = 3, Head = 3, Tail = 2, Exhaust = 1 } },
            new CarDef { Id = "van", Title = "Фургон Карго", Sub = "грузовой фургон", Price = 24000, MaxSpeed = 30, Accel = 5.5f,
                Spec = new CarSpec { L = 5.0f, W = 2.0f, H = 2.05f, WB = 3.1f, R = 0.36f, FrontOH = 0.85f, Sill = 0.32f, HoodY = 1.08f, DeckY = 1.15f, NoseY = 0.78f, TailY = 1.2f, TailBottom = 0.4f,
                    ZA = 1.6f, ZB = 1.05f, ZC = -2.3f, ZD = -2.44f, Tumble = 0.93f, Grille = 2, Head = 3, Tail = 2, Exhaust = 1 } },
            new CarDef { Id = "suv", Title = "Внедорожник X5", Sub = "премиальный SUV", Price = 42000, MaxSpeed = 40, Accel = 8,
                Spec = new CarSpec { L = 4.92f, W = 2.0f, H = 1.75f, WB = 2.97f, R = 0.39f, FrontOH = 0.9f, Sill = 0.34f, HoodY = 1.13f, DeckY = 1.14f, NoseY = 0.8f, NoseBottom = 0.3f, TailY = 0.95f, TailBottom = 0.45f,
                    ZA = 0.75f, ZB = 0.12f, ZC = -1.75f, ZD = -2.2f, Tumble = 0.84f, Grille = 1, Head = 0, Tail = 0, RoofRails = true, Exhaust = 2 } },
            new CarDef { Id = "sport", Title = "Спорткар GT", Sub = "купе 2+2", Price = 85000, MaxSpeed = 55, Accel = 12,
                Spec = new CarSpec { L = 4.5f, W = 1.92f, H = 1.28f, WB = 2.65f, R = 0.35f, FrontOH = 0.95f, Sill = 0.18f, HoodY = 0.86f, DeckY = 0.9f, NoseY = 0.5f, NoseBottom = 0.12f, TailY = 0.6f, TailBottom = 0.25f,
                    ZA = 0.32f, ZB = -0.35f, ZC = -0.85f, ZD = -1.65f, Tumble = 0.76f, Spoiler = true, Grille = 2, Head = 3, Tail = 1, Exhaust = 4, Caliper = new Color(0.85f, 0.1f, 0.1f) } },
            new CarDef { Id = "electro", Title = "Электрокар Volt", Sub = "электро-седан будущего", Price = 64000, MaxSpeed = 50, Accel = 14,
                Spec = new CarSpec { L = 4.7f, W = 1.9f, H = 1.44f, WB = 2.88f, R = 0.34f, FrontOH = 0.85f, HoodY = 0.92f, DeckY = 0.98f, NoseY = 0.56f, TailY = 0.7f,
                    ZA = 0.75f, ZB = 0.05f, ZC = -0.9f, ZD = -1.45f, Tumble = 0.78f, Grille = 0, Head = 2, Tail = 1, Exhaust = 0 } },
            new CarDef { Id = "hyper", Title = "Гиперкар X-22", Sub = "гиперкар будущего", Price = 320000, MaxSpeed = 65, Accel = 18,
                Spec = new CarSpec { L = 4.6f, W = 2.05f, H = 1.15f, WB = 2.7f, R = 0.36f, FrontOH = 1.0f, Sill = 0.15f, HoodY = 0.74f, DeckY = 0.95f, NoseY = 0.42f, NoseBottom = 0.1f, TailY = 0.62f, TailBottom = 0.22f,
                    ZA = 0.6f, ZB = -0.2f, ZC = -0.6f, ZD = -1.4f, Tumble = 0.72f, Wing = true, Grille = 0, Head = 3, Tail = 1, Exhaust = 2, Caliper = new Color(0.95f, 0.75f, 0.1f) } },
        };

        public static CarDef Get(string id) => All.Find(c => c.Id == id) ?? All[1];
    }

    public static class Palette
    {
        public static readonly Color[] Car =
        {
            new Color(0.85f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.75f), new Color(0.95f, 0.95f, 0.95f), new Color(0.08f, 0.08f, 0.09f),
            new Color(0.6f, 0.62f, 0.65f), new Color(0.95f, 0.75f, 0.1f), new Color(0.1f, 0.55f, 0.25f), new Color(0.95f, 0.45f, 0.05f),
            new Color(0.45f, 0.15f, 0.6f), new Color(0.2f, 0.75f, 0.85f),
        };
        public static readonly string[] CarNames = { "Красный", "Синий", "Белый", "Чёрный", "Серебро", "Жёлтый", "Зелёный", "Оранжевый", "Фиолетовый", "Бирюза" };

        public static readonly Color[] Skin =
        {
            new Color(1f, 0.86f, 0.74f), new Color(0.95f, 0.76f, 0.6f), new Color(0.82f, 0.62f, 0.45f), new Color(0.6f, 0.42f, 0.28f), new Color(0.38f, 0.25f, 0.16f),
        };
        public static readonly Color[] Hair =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.3f, 0.18f, 0.1f), new Color(0.65f, 0.45f, 0.2f), new Color(0.95f, 0.85f, 0.55f),
            new Color(0.7f, 0.2f, 0.12f), new Color(0.85f, 0.85f, 0.88f), new Color(0.2f, 0.45f, 0.95f), new Color(0.95f, 0.35f, 0.7f),
        };
        public static readonly Color[] Cloth =
        {
            new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.1f, 0.12f), new Color(0.8f, 0.12f, 0.15f), new Color(0.15f, 0.3f, 0.75f),
            new Color(0.15f, 0.55f, 0.3f), new Color(0.95f, 0.75f, 0.15f), new Color(0.55f, 0.2f, 0.65f), new Color(0.95f, 0.5f, 0.1f),
            new Color(0.5f, 0.5f, 0.52f), new Color(0.35f, 0.25f, 0.18f), new Color(0.2f, 0.75f, 0.85f), new Color(0.95f, 0.5f, 0.7f),
            new Color(0.2f, 0.3f, 0.45f), new Color(0.7f, 0.6f, 0.45f),
        };

        public static Color Get(Color[] p, int i) => p[Mathf.Abs(i) % p.Length];
    }

    /// <summary>Food items (restaurants, cafés, shops).</summary>
    public class FoodDef
    {
        public string Title; public int Price, Food;
        public static readonly FoodDef[] Cafe = { new FoodDef { Title = "Кофе", Price = 15, Food = 10 }, new FoodDef { Title = "Круассан", Price = 20, Food = 20 }, new FoodDef { Title = "Бургер", Price = 35, Food = 40 }, new FoodDef { Title = "Пицца", Price = 45, Food = 55 } };
        public static readonly FoodDef[] Restaurant = { new FoodDef { Title = "Суп дня", Price = 40, Food = 35 }, new FoodDef { Title = "Плов", Price = 70, Food = 70 }, new FoodDef { Title = "Стейк", Price = 140, Food = 90 }, new FoodDef { Title = "Ужин из 3 блюд", Price = 220, Food = 100 } };
        public static readonly FoodDef[] Shop = { new FoodDef { Title = "Хлеб", Price = 5, Food = 10 }, new FoodDef { Title = "Вода", Price = 3, Food = 5 }, new FoodDef { Title = "Сэндвич", Price = 14, Food = 25 }, new FoodDef { Title = "Набор продуктов", Price = 60, Food = 80 } };
    }

    /// <summary>Furniture for decorating a home: each slot has a few styles.</summary>
    public class DecorDef
    {
        public string Slot, Title; public int Price;
        public static readonly DecorDef[] All =
        {
            new DecorDef { Slot = "sofa", Title = "Диван", Price = 1200 },
            new DecorDef { Slot = "tv", Title = "Телевизор", Price = 1800 },
            new DecorDef { Slot = "bed", Title = "Кровать", Price = 1500 },
            new DecorDef { Slot = "table", Title = "Обеденный стол", Price = 900 },
            new DecorDef { Slot = "plant", Title = "Растения", Price = 300 },
            new DecorDef { Slot = "rug", Title = "Ковёр", Price = 500 },
            new DecorDef { Slot = "lamp", Title = "Торшер", Price = 400 },
            new DecorDef { Slot = "art", Title = "Картина", Price = 700 },
            new DecorDef { Slot = "shelf", Title = "Книжный шкаф", Price = 800 },
            new DecorDef { Slot = "aquarium", Title = "Аквариум", Price = 2500 },
            new DecorDef { Slot = "piano", Title = "Пианино", Price = 6000 },
            new DecorDef { Slot = "pc", Title = "Игровой компьютер", Price = 3500 },
        };
    }
}
