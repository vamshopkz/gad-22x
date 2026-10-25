using UnityEngine;

namespace Gad
{
    /// <summary>0.8: work at the computer of your own office: pick a task, it takes a few seconds, you get paid.</summary>
    public class OfficeWork : MonoBehaviour
    {
        static OfficeWork I;
        static readonly (string name, string what, int pay, float secs)[] Tasks =
        {
            ("Ответить на письма", "Почта клиентов", 120, 6f),
            ("Звонок клиенту", "Договориться о встрече", 180, 8f),
            ("Отчёт за неделю", "Таблицы и графики", 320, 14f),
            ("Презентация проекта", "Слайды для инвесторов", 600, 24f),
        };
        int busy = -1;
        float left;

        public static void Open()
        {
            if (I == null) I = new GameObject("OfficeWork").AddComponent<OfficeWork>();
            I.Show();
        }

        void Show()
        {
            Window.Show("РАБОТА В ОФИСЕ", w =>
            {
                w.Text(busy >= 0 ? $"Выполняется: {Tasks[busy].name} — осталось {Mathf.CeilToInt(left)} с" : "Выберите задачу — деньги придут, когда она будет готова. Энергия тратится понемногу.", 26, busy >= 0 ? UIKit.Gold : UIKit.Muted);
                for (int i = 0; i < Tasks.Length; i++)
                {
                    int k = i;
                    var t = Tasks[i];
                    w.Row(t.name, $"{t.what} · {Mathf.RoundToInt(t.secs)} с · +{Hud.Money(t.pay)}", "ДЕЛАТЬ", new Color(0.15f, 0.55f, 0.85f), () => { busy = k; left = Tasks[k].secs; Window.Refresh(); }, busy < 0 && Save.D.energy > 5);
                }
            }, 1000, 700);
        }

        void Update()
        {
            if (busy < 0) return;
            left -= Time.deltaTime;
            if (Mathf.Repeat(left, 1f) > Mathf.Repeat(left + Time.deltaTime, 1f) && UIRoot.Open != null && UIRoot.Open.Title.text.StartsWith("РАБОТА В ОФИСЕ")) Window.Refresh();
            if (left > 0) return;
            var t = Tasks[busy];
            busy = -1;
            Save.D.money += t.pay;
            Save.D.energy = Mathf.Max(0, Save.D.energy - t.secs * 0.4f);
            Save.Write();
            Hud.Toast($"Готово: {t.name}. +{Hud.Money(t.pay)}", new Color(0.5f, 1f, 0.6f));
            if (UIRoot.Open != null && UIRoot.Open.Title.text.StartsWith("РАБОТА В ОФИСЕ")) Window.Refresh();
        }
    }
}
