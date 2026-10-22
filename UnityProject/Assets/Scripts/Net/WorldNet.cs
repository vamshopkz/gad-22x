using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// The shared world: every player (Android app, iPhone/any browser) publishes their state to one
    /// topic on two free public MQTT relays and sees everyone else. Phones use MQTT over TCP, browsers
    /// use the same brokers over secure WebSockets, so all platforms meet in the same city.
    /// </summary>
    public class WorldNet : MonoBehaviour
    {
        public const string Topic = "gad22x/v1/world";
        static readonly string[] Brokers = { "broker.emqx.io", "broker.hivemq.com" };
        public static WorldNet I;
        public static readonly Dictionary<string, RemoteAvatar> Remotes = new Dictionary<string, RemoteAvatar>();
        public static readonly List<(string who, string text, Color color)> ChatLog = new List<(string, string, Color)>();
        public static int OnlineCount => Remotes.Count;
        public static bool Connected => I != null && I.links.Exists(l => l != null && l.Ready);
        readonly List<ILink> links = new List<ILink>();
        readonly float[] retryAt = new float[2];
        readonly List<NetIncoming> inbox = new List<NetIncoming>();
        readonly Dictionary<string, int> lastSeq = new Dictionary<string, int>();
        int seq;
        float sendT, lastSent;
        Vector3 lastPos;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Create()
        {
            var go = new GameObject("WorldNet");
            I = go.AddComponent<WorldNet>();
            for (int i = 0; i < Brokers.Length; i++) I.links.Add(null);
        }

        void Update()
        {
            // (re)connect
            for (int i = 0; i < Brokers.Length; i++)
            {
                var l = links[i];
                if ((l == null || l.Failed) && Time.unscaledTime >= retryAt[i])
                {
                    l?.Close();
                    links[i] = MqttLink.Create(Brokers[i], Topic, Topic);
                    retryAt[i] = Time.unscaledTime + 6f;
                }
            }
            inbox.Clear();
            foreach (var l in links) l?.Poll(inbox);
            foreach (var m in inbox)
                foreach (var line in m.text.Split('\n'))
                    if (line.Length > 2) Handle(line);

            // send my state
            var pl = PlayerCtl.I;
            if (pl != null && Save.D.created)
            {
                sendT += Time.unscaledDeltaTime;
                var pos = pl.transform.position;
                bool moving = (pos - lastPos).sqrMagnitude > 0.0004f || pl.Car != null && Mathf.Abs(pl.Car.Speed) > 0.1f;
                float interval = moving ? 0.2f : 1.5f;
                if (sendT >= interval)
                {
                    sendT = 0;
                    lastPos = pos;
                    SendState();
                }
            }

            // drop silent players
            var gone = new List<string>();
            foreach (var kv in Remotes) if (Time.unscaledTime - kv.Value.LastHeard > 12f) gone.Add(kv.Key);
            foreach (var id in gone) { if (Remotes[id] != null) Destroy(Remotes[id].gameObject); Remotes.Remove(id); }
        }

        static string Clean(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", " ");

        void Send(string msg)
        {
            foreach (var l in links) if (l != null && l.Ready) l.Send(msg);
        }

        void SendState()
        {
            var pl = PlayerCtl.I;
            var c = pl.Car;
            var t = c != null ? c.transform : pl.transform;
            int inside = pl.Inside >= 0 ? Interiors.Code : -1;
            string car = c != null ? c.Def.Id + ":" + c.ColorIndex : "";
            int flags = pl.Body.Pose | (pl.CC.isGrounded ? 0 : 8);
            float speed = c != null ? c.Speed : pl.PlanarSpeed;
            Send(string.Join("|", "P", Save.D.id, (++seq).ToString(), Clean(Save.D.name), Save.D.look.Pack(),
                t.position.x.ToString("0.00", Inv), t.position.y.ToString("0.00", Inv), t.position.z.ToString("0.00", Inv),
                t.eulerAngles.y.ToString("0", Inv), speed.ToString("0.0", Inv), inside.ToString(), car, flags.ToString()));
        }

        public static void LookChanged() { if (I != null) I.sendT = 99; }

        public static void SendChat(string text)
        {
            if (I == null) return;
            text = Clean(text);
            if (text.Length > 140) text = text.Substring(0, 140);
            I.Send(string.Join("|", "C", Save.D.id, (++I.seq).ToString(), Clean(Save.D.name), text));
            AddChat(Save.D.name, text, UIKit.Cyan);
            PlayerSay(text);
        }

        static void PlayerSay(string text) { /* own bubble not needed: chat line shows it */ }

        static void AddChat(string who, string text, Color c)
        {
            ChatLog.Add((who, text, c));
            if (ChatLog.Count > 40) ChatLog.RemoveAt(0);
            Hud.ChatLine(who, text, c);
        }

        void Handle(string line)
        {
            var p = line.Split('|');
            if (p.Length < 3) return;
            string id = p[1];
            if (id == Save.D.id) return;
            if (!int.TryParse(p[2], out int s)) return;
            string key = p[0] + id;
            // both relays deliver a copy: keep only newer messages (a restart resets seq → accept big jumps back)
            if (lastSeq.TryGetValue(key, out int last) && s <= last && last - s < 1000) return;
            lastSeq[key] = s;
            switch (p[0])
            {
                case "P" when p.Length >= 13:
                {
                    if (!Remotes.TryGetValue(id, out var r) || r == null)
                    {
                        r = RemoteAvatar.Create(id);
                        Remotes[id] = r;
                        Hud.Toast(Clean(p[3]) + " в мире", new Color(1f, 0.6f, 0.85f));
                    }
                    float F(int i) => float.TryParse(p[i], NumberStyles.Float, Inv, out var v) ? v : 0;
                    int.TryParse(p[10], out int inside);
                    int.TryParse(p[12], out int flags);
                    r.SetState(Clean(p[3]), p[4], new Vector3(F(5), F(6), F(7)), F(8), F(9), inside, p[11], flags);
                    break;
                }
                case "C" when p.Length >= 5:
                {
                    AddChat(p[3], p[4], UIKit.Pink);
                    if (Remotes.TryGetValue(id, out var r) && r != null) r.Say(p[4]);
                    AudioFx.Play("chat");
                    break;
                }
            }
        }

        void OnApplicationQuit()
        {
            foreach (var l in links) l?.Close();
        }
    }

    /// <summary>Another player: a person (or their car) smoothly following the received positions.</summary>
    public class RemoteAvatar : MonoBehaviour
    {
        public string Id, Name = "Игрок";
        public float LastHeard;
        public int InsideCode = -1;
        public Vector3 WorldPos;
        public string Place = "";
        Human human;
        GameObject carGo;
        Car carComp;
        string carKey = "", lookKey = "";
        Vector3 target, vel;
        float yaw, speed;
        TextMeshPro tag, bubble;
        float bubbleUntil;

        public static RemoteAvatar Create(string id)
        {
            var go = new GameObject("Remote " + id) { layer = 11 };
            var r = go.AddComponent<RemoteAvatar>();
            r.Id = id;
            r.human = Human.Create(go.transform, new Look(), "Body");
            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0, 0.9f, 0); cc.height = 1.8f; cc.radius = 0.3f;
            r.tag = MakeText(go.transform, 2.25f, 3.2f, Color.white);
            r.bubble = MakeText(go.transform, 2.75f, 2.8f, new Color(1f, 0.95f, 0.6f));
            r.bubble.gameObject.SetActive(false);
            return r;
        }

        static TextMeshPro MakeText(Transform parent, float y, float size, Color c)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            var t = go.AddComponent<TextMeshPro>();
            t.font = UIKit.Font; t.fontSharedMaterial = UIKit.TextOutline;
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(6, 1.5f);
            return t;
        }

        public void SetState(string name, string look, Vector3 pos, float yawDeg, float spd, int inside, string car, int flags)
        {
            LastHeard = Time.unscaledTime;
            if (Name != name) { Name = name; tag.text = name; }
            if (look != lookKey) { lookKey = look; human.Rebuild(Look.Unpack(look)); }
            bool teleport = (pos - target).sqrMagnitude > 60f * 60f || inside != InsideCode;
            InsideCode = inside;
            target = pos; yaw = yawDeg; speed = spd;
            if (teleport) transform.position = pos;
            human.Pose = flags & 7;
            human.Grounded = (flags & 8) == 0;
            var b = inside >= 0 ? City.Get(inside / 1000) : null;
            WorldPos = b != null ? b.door : pos;
            Place = b != null ? b.name : City.DistrictAt(pos);
            if (car != carKey)
            {
                carKey = car;
                if (carGo != null) Destroy(carGo);
                carGo = null;
                if (!string.IsNullOrEmpty(car))
                {
                    var parts = car.Split(':');
                    int.TryParse(parts.Length > 1 ? parts[1] : "0", out int col);
                    var def = CarDef.Get(parts[0]);
                    carComp = Car.Spawn(def, col, transform.position, yaw, true);
                    carGo = carComp.gameObject;
                    carGo.transform.SetParent(transform, false);
                    carGo.transform.localPosition = Vector3.zero;
                    carGo.transform.localRotation = Quaternion.identity;
                    carComp.enabled = false;   // no physics here, we only show it
                    Car.Live.Remove(carComp);
                }
            }
            human.gameObject.SetActive(carGo == null);
            tag.transform.localPosition = new Vector3(0, carGo != null ? 2.6f : 2.25f, 0);
        }

        public void Say(string text)
        {
            bubble.text = text;
            bubble.gameObject.SetActive(true);
            bubbleUntil = Time.unscaledTime + 8f;
        }

        void Update()
        {
            int mine = PlayerCtl.I != null && PlayerCtl.I.Inside >= 0 ? Interiors.Code : -1;
            bool visible = mine == InsideCode;
            foreach (Transform c in transform) c.gameObject.SetActive(visible && (c.gameObject != human.gameObject || carGo == null) && (c.gameObject != bubble.gameObject || Time.unscaledTime < bubbleUntil));
            var dt = Time.deltaTime;
            // extrapolate a little along the heading, then ease towards the target
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var goal = target + fwd * speed * Mathf.Min(0.25f, Time.unscaledTime - LastHeard);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, 0.12f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, 0), dt * 10f);
            human.Speed = carGo == null ? Mathf.Abs(speed) : 0;
            if (carComp != null) carComp.SpinWheels(speed * dt);
            var cam = PlayerCtl.I != null ? PlayerCtl.I.Cam : null;
            if (cam != null)
            {
                tag.transform.rotation = Quaternion.LookRotation(tag.transform.position - cam.transform.position);
                bubble.transform.rotation = tag.transform.rotation;
            }
        }
    }
}
