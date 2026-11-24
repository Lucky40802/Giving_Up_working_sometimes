using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Badges : MonoBehaviour
{
    enum Tier { Bronze, Silver, Gold }

    class Badge
    {
        public string id;
        public string name;
        public string desc;
        public Tier tier;
        public string stat;
        public float target;
        public bool lowerIsBetter;
        public bool secret;
        public bool done;

        public Badge(string id, string name, string desc, Tier tier, string stat, float target,
            bool lowerIsBetter = false, bool secret = false)
        {
            this.id = id;
            this.name = name;
            this.desc = desc;
            this.tier = tier;
            this.stat = stat;
            this.target = target;
            this.lowerIsBetter = lowerIsBetter;
            this.secret = secret;
        }

        public int Points
        {
            get
            {
                if (tier == Tier.Gold) return 50;
                if (tier == Tier.Silver) return 25;
                return 10;
            }
        }
    }

    const float VW = 1280f;
    const float VH = 720f;
    const int PerPage = 18;
    const float NoClear = 9999f;
    const float PopupTime = 3.5f;
    const string BadgePrefix = "bdg_";
    const string StatPrefix = "bst_";
    const string KindsKey = "bst_kinds";

    static readonly string[] StatNames =
    {
        "deaths", "clears", "jumps", "playSec", "stack", "bodies", "fast", "flawless",
        "levelMax", "tricks", "twoP", "pb", "instant", "rapid", "comeback", "mute"
    };

    static readonly string[] FilterNames = { "all", "done", "todo" };

    readonly List<Badge> badges = new List<Badge>();
    readonly Dictionary<string, float> stats = new Dictionary<string, float>();
    readonly Dictionary<Vector2Int, Corpse> seen = new Dictionary<Vector2Int, Corpse>();
    readonly HashSet<string> kinds = new HashSet<string>();
    readonly Queue<Badge> popups = new Queue<Badge>();
    readonly List<float> recentDeaths = new List<float>();

    Badge shownBadge;
    float popupT;

    bool open;
    int page;
    int filter;

    int lastLevel = -1;
    GameManager.Mode lastMode;
    float runClock;
    int attemptDeaths;
    float bestAtStart;
    float secondAcc;
    bool lastMuted;
    bool muteReady;
    bool dirty;
    float saveTimer;

    Texture2D pixel;
    GUIStyle titleStyle;
    GUIStyle nameStyle;
    GUIStyle descStyle;
    GUIStyle iconStyle;
    GUIStyle smallStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Badges").AddComponent<Badges>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        DefineBadges();
        LoadAll();
    }

    void OnDisable()
    {
        Save();
    }

    void DefineBadges()
    {
        badges.Add(new Badge("first", "first blood", "leave your very first body", Tier.Bronze, "deaths", 1));
        badges.Add(new Badge("weight", "dead weight", "die 25 times in total", Tier.Bronze, "deaths", 25));
        badges.Add(new Badge("yard", "graveyard", "die 100 times in total", Tier.Silver, "deaths", 100));
        badges.Add(new Badge("necro", "necropolis", "die 500 times in total", Tier.Gold, "deaths", 500));

        badges.Add(new Badge("steps", "baby steps", "clear a level", Tier.Bronze, "clears", 1));
        badges.Add(new Badge("roll", "on a roll", "clear 5 levels", Tier.Bronze, "clears", 5));
        badges.Add(new Badge("half", "halfway there", "reach level 8", Tier.Silver, "levelMax", 8));
        badges.Add(new Badge("finish", "finisher", "clear the final level", Tier.Gold, "levelMax", 15));

        badges.Add(new Badge("clean", "clean run", "clear a level without dying", Tier.Bronze, "flawless", 1));
        badges.Add(new Badge("untouch", "untouchable", "5 flawless clears", Tier.Silver, "flawless", 5));

        badges.Add(new Badge("quick", "quick feet", "clear a level in 15 seconds", Tier.Silver, "fast", 15f, true));
        badges.Add(new Badge("blink", "blink", "clear a level in 8 seconds", Tier.Gold, "fast", 8f, true));
        badges.Add(new Badge("pb1", "personal best", "beat your own best time", Tier.Bronze, "pb", 1));
        badges.Add(new Badge("pb5", "always improving", "beat your best time 5 times", Tier.Silver, "pb", 5));

        badges.Add(new Badge("tower", "mini tower", "stack 3 bodies in a column", Tier.Bronze, "stack", 3));
        badges.Add(new Badge("sky", "skyscraper", "stack 6 bodies in a column", Tier.Silver, "stack", 6));
        badges.Add(new Badge("pile", "body pile", "have 25 bodies on one level", Tier.Bronze, "bodies", 25));
        badges.Add(new Badge("mass", "mass grave", "have 60 bodies on one level", Tier.Gold, "bodies", 60));

        badges.Add(new Badge("gadget", "gadgeteer", "use 3 different tricks", Tier.Silver, "tricks", 3));
        badges.Add(new Badge("omni", "omnitrickster", "use all 5 tricks", Tier.Gold, "tricks", 5));

        badges.Add(new Badge("settle", "settling in", "play for 5 minutes", Tier.Bronze, "playSec", 300));
        badges.Add(new Badge("dedic", "dedicated", "play for 30 minutes", Tier.Silver, "playSec", 1800));
        badges.Add(new Badge("marathon", "marathon", "play for an hour", Tier.Gold, "playSec", 3600));

        badges.Add(new Badge("hop", "hopper", "jump 100 times", Tier.Bronze, "jumps", 100));
        badges.Add(new Badge("pogo", "pogo stick", "jump 1000 times", Tier.Silver, "jumps", 1000));

        badges.Add(new Badge("buddy", "buddy system", "clear a level in 2 player mode", Tier.Silver, "twoP", 1));

        badges.Add(new Badge("instant", "speedy suicide", "die within a second of starting", Tier.Bronze, "instant", 1, false, true));
        badges.Add(new Badge("triple", "triple tap", "die 3 times in 5 seconds", Tier.Silver, "rapid", 1, false, true));
        badges.Add(new Badge("comeback", "comeback kid", "clear a level after 20 deaths", Tier.Gold, "comeback", 1, false, true));
        badges.Add(new Badge("quiet", "quiet please", "toggle the sound 3 times", Tier.Bronze, "mute", 3, false, true));
    }

    float Stat(string key)
    {
        stats.TryGetValue(key, out float value);
        return value;
    }

    void Set(string key, float value)
    {
        if (Mathf.Approximately(Stat(key), value)) return;
        stats[key] = value;
        dirty = true;
        CheckBadges();
    }

    void Add(string key, float amount)
    {
        Set(key, Stat(key) + amount);
    }

    void SetMax(string key, float value)
    {
        if (value > Stat(key)) Set(key, value);
    }

    void SetMin(string key, float value)
    {
        if (value < Stat(key)) Set(key, value);
    }

    void LoadAll()
    {
        foreach (string name in StatNames)
        {
            float fallback = name == "fast" ? NoClear : 0f;
            stats[name] = PlayerPrefs.GetFloat(StatPrefix + name, fallback);
        }

        foreach (var badge in badges)
            badge.done = PlayerPrefs.GetInt(BadgePrefix + badge.id, 0) == 1;

        string saved = PlayerPrefs.GetString(KindsKey, "");
        if (saved != "")
        {
            foreach (string kind in saved.Split(','))
                kinds.Add(kind);
        }
    }

    void Save()
    {
        foreach (var pair in stats)
            PlayerPrefs.SetFloat(StatPrefix + pair.Key, pair.Value);

        PlayerPrefs.SetString(KindsKey, string.Join(",", kinds));
        PlayerPrefs.Save();
        dirty = false;
        saveTimer = 0f;
    }

    bool Met(Badge b)
    {
        float value = Stat(b.stat);
        return b.lowerIsBetter ? value <= b.target : value >= b.target;
    }

    void CheckBadges()
    {
        foreach (var badge in badges)
        {
            if (badge.done || !Met(badge)) continue;
            badge.done = true;
            PlayerPrefs.SetInt(BadgePrefix + badge.id, 1);
            popups.Enqueue(badge);
        }
    }

    float Progress(Badge b)
    {
        if (b.done) return 1f;
        float value = Stat(b.stat);
        if (b.lowerIsBetter)
            return value >= NoClear ? 0f : Mathf.Clamp01(b.target / value);
        return Mathf.Clamp01(value / b.target);
    }

    string ProgressText(Badge b)
    {
        float value = Stat(b.stat);

        if (b.lowerIsBetter)
        {
            if (value >= NoClear) return "no clear yet";
            return value.ToString("0.0") + "s / " + b.target + "s";
        }

        if (b.stat == "playSec")
        {
            int have = Mathf.FloorToInt(Mathf.Min(value, b.target) / 60f);
            return have + " / " + Mathf.RoundToInt(b.target / 60f) + " min";
        }

        return Mathf.FloorToInt(Mathf.Min(value, b.target)) + " / " + b.target;
    }

    int Unlocked()
    {
        int count = 0;
        foreach (var badge in badges)
        {
            if (badge.done) count++;
        }
        return count;
    }

    int Score()
    {
        int total = 0;
        foreach (var badge in badges)
        {
            if (badge.done) total += badge.Points;
        }
        return total;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        HandleKeys();
        TickPopup();
        TrackMode(gm);
        TrackBodies(gm);
        TrackJumps(gm);
        TrackMute(gm);

        saveTimer += Time.unscaledDeltaTime;
        if (dirty && saveTimer > 2f) Save();
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.bKey.wasPressedThisFrame) open = !open;
        if (!open) return;

        if (kb.rightBracketKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) page++;
        if (kb.leftBracketKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) page--;
    }

    void TickPopup()
    {
        if (popupT > 0f)
        {
            popupT -= Time.unscaledDeltaTime;
            return;
        }

        if (popups.Count == 0) return;
        shownBadge = popups.Dequeue();
        popupT = PopupTime;
    }

    void ResetRun(GameManager gm)
    {
        runClock = 0f;
        attemptDeaths = 0;
        bestAtStart = Records.Best(gm.levelIndex);
    }

    void TrackMode(GameManager gm)
    {
        bool levelChanged = gm.levelIndex != lastLevel;
        bool freshRun = gm.mode == GameManager.Mode.Play
            && lastMode != GameManager.Mode.Play
            && lastMode != GameManager.Mode.Pause;

        if (levelChanged)
        {
            lastLevel = gm.levelIndex;
            seen.Clear();
            foreach (var pair in gm.corpses) seen[pair.Key] = pair.Value;
        }
        if (levelChanged || freshRun) ResetRun(gm);

        if (gm.mode == GameManager.Mode.Play)
        {
            runClock += Time.deltaTime;
            secondAcc += Time.unscaledDeltaTime;
            if (secondAcc >= 1f)
            {
                Add("playSec", secondAcc);
                secondAcc = 0f;
            }
        }

        if (lastMode == GameManager.Mode.Play && gm.mode == GameManager.Mode.Clear) OnClear(gm);
        lastMode = gm.mode;
    }

    void OnClear(GameManager gm)
    {
        Add("clears", 1);
        SetMax("levelMax", gm.levelIndex + 1);
        SetMin("fast", runClock);

        if (attemptDeaths == 0) Add("flawless", 1);
        if (attemptDeaths >= 20) Set("comeback", 1);
        if (AnyTwoPlayer()) Add("twoP", 1);

        bool hadBest = bestAtStart > 0f && bestAtStart < 99999f;
        if (hadBest && runClock < bestAtStart - 0.01f) Add("pb", 1);
    }

    bool AnyTwoPlayer()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.two) return true;
        }
        return false;
    }

    void TrackBodies(GameManager gm)
    {
        var gone = new List<Vector2Int>();
        foreach (var cell in seen.Keys)
        {
            if (!gm.corpses.ContainsKey(cell)) gone.Add(cell);
        }
        foreach (var cell in gone) seen.Remove(cell);

        bool added = false;
        foreach (var pair in gm.corpses)
        {
            seen.TryGetValue(pair.Key, out var old);
            if (old == pair.Value) continue;

            seen[pair.Key] = pair.Value;
            OnDeath();
            added = true;
        }

        if (added) SetMax("stack", TallestStack(gm));
        SetMax("bodies", gm.corpses.Count);
        CountKinds(gm);
    }

    void OnDeath()
    {
        Add("deaths", 1);
        attemptDeaths++;

        if (attemptDeaths == 1 && runClock < 1f) Set("instant", 1);

        recentDeaths.Add(Time.time);
        recentDeaths.RemoveAll(t => Time.time - t > 5f);
        if (recentDeaths.Count >= 3) Set("rapid", 1);
    }

    int TallestStack(GameManager gm)
    {
        int best = 0;
        foreach (var cell in gm.corpses.Keys)
        {
            if (gm.corpses.ContainsKey(new Vector2Int(cell.x, cell.y - 1))) continue;

            int height = 0;
            while (gm.corpses.ContainsKey(new Vector2Int(cell.x, cell.y + height))) height++;
            best = Mathf.Max(best, height);
        }
        return best;
    }

    void CountKinds(GameManager gm)
    {
        foreach (var body in gm.corpses.Values)
        {
            if (body == null || body.kind == "normal") continue;
            if (!kinds.Add(body.kind)) continue;
            Set("tricks", kinds.Count);
            dirty = true;
        }
    }

    void TrackJumps(GameManager gm)
    {
        var kb = Keyboard.current;
        if (kb == null || gm.mode != GameManager.Mode.Play) return;

        bool jump = kb.spaceKey.wasPressedThisFrame
            || kb.wKey.wasPressedThisFrame
            || kb.upArrowKey.wasPressedThisFrame;
        if (jump) Add("jumps", 1);
    }

    void TrackMute(GameManager gm)
    {
        bool muted = gm.sfx.muted;
        if (!muteReady)
        {
            lastMuted = muted;
            muteReady = true;
            return;
        }

        if (muted == lastMuted) return;
        lastMuted = muted;
        Add("mute", 1);
    }

    static Color TierColor(Tier tier)
    {
        if (tier == Tier.Gold) return new Color(1f, 0.82f, 0.25f);
        if (tier == Tier.Silver) return new Color(0.75f, 0.78f, 0.86f);
        return new Color(0.8f, 0.5f, 0.2f);
    }

    static string TierLetter(Tier tier)
    {
        if (tier == Tier.Gold) return "G";
        if (tier == Tier.Silver) return "S";
        return "B";
    }

    static Color Dim(Color c, float amount)
    {
        return new Color(c.r * amount, c.g * amount, c.b * amount, 0.95f);
    }

    void MakeStyles()
    {
        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 13;
        smallStyle.alignment = TextAnchor.MiddleCenter;
        smallStyle.normal.textColor = Color.white;

        titleStyle = new GUIStyle(smallStyle);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleLeft;

        nameStyle = new GUIStyle(smallStyle);
        nameStyle.fontSize = 16;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.alignment = TextAnchor.MiddleLeft;

        descStyle = new GUIStyle(smallStyle);
        descStyle.fontSize = 12;
        descStyle.alignment = TextAnchor.UpperLeft;
        descStyle.wordWrap = true;
        descStyle.normal.textColor = new Color(0.78f, 0.8f, 0.9f);

        iconStyle = new GUIStyle(smallStyle);
        iconStyle.fontSize = 28;
        iconStyle.fontStyle = FontStyle.Bold;
        iconStyle.normal.textColor = new Color(0.1f, 0.06f, 0.13f);
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, pixel);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (titleStyle == null) MakeStyles();

        var oldMatrix = GUI.matrix;
        float s = Mathf.Min(Screen.width / VW, Screen.height / VH);
        var offset = new Vector3((Screen.width - VW * s) / 2f, (Screen.height - VH * s) / 2f, 0f);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

        DrawPopup();
        if (open) DrawRoom();

        GUI.matrix = oldMatrix;
    }

    void DrawPopup()
    {
        if (shownBadge == null || popupT <= 0f) return;

        float slideIn = Mathf.Clamp01((PopupTime - popupT) / 0.35f);
        float slideOut = Mathf.Clamp01(popupT / 0.35f);
        float k = Mathf.Min(slideIn, slideOut);
        float eased = 1f - (1f - k) * (1f - k);

        var r = new Rect(Mathf.Lerp(-340f, 16f, eased), 500f, 320f, 78f);
        Color tc = TierColor(shownBadge.tier);

        Fill(r, Dim(tc, 0.35f));
        Fill(new Rect(r.x, r.y, 6f, r.height), tc);

        var icon = new Rect(r.x + 16f, r.y + 12f, 54f, 54f);
        Fill(icon, tc);
        GUI.Label(icon, TierLetter(shownBadge.tier), iconStyle);

        GUI.Label(new Rect(r.x + 80f, r.y + 6f, 230f, 20f), "badge unlocked", descStyle);
        GUI.Label(new Rect(r.x + 80f, r.y + 24f, 230f, 26f), shownBadge.name, nameStyle);
        GUI.Label(new Rect(r.x + 80f, r.y + 50f, 230f, 20f), "+" + shownBadge.Points + " points", descStyle);
    }

    List<Badge> FilteredList()
    {
        var list = new List<Badge>();
        foreach (var badge in badges)
        {
            if (filter == 1 && !badge.done) continue;
            if (filter == 2 && badge.done) continue;
            list.Add(badge);
        }
        return list;
    }

    void DrawRoom()
    {
        var e = Event.current;
        var list = FilteredList();
        int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)PerPage));

        if (e.type == EventType.ScrollWheel)
        {
            page += e.delta.y > 0f ? 1 : -1;
            e.Use();
        }
        page = Mathf.Clamp(page, 0, pages - 1);

        Fill(new Rect(0f, 0f, VW, VH), new Color(0f, 0f, 0f, 0.78f));
        var panel = new Rect(140f, 40f, 1000f, 640f);
        Fill(panel, new Color(0.08f, 0.08f, 0.14f, 0.98f));
        Fill(new Rect(panel.x, panel.y, panel.width, 4f), new Color(1f, 0.82f, 0.4f));

        GUI.Label(new Rect(panel.x + 24f, panel.y + 12f, 400f, 44f), "trophy room", titleStyle);
        string summary = Unlocked() + " / " + badges.Count + " unlocked    " + Score() + " points";
        GUI.Label(new Rect(panel.x + 24f, panel.y + 52f, 500f, 22f), summary, descStyle);

        DrawFilters(panel);
        DrawStatStrip(panel);
        DrawGrid(panel, list);
        DrawFooter(panel, pages);
    }

    void DrawFilters(Rect panel)
    {
        for (int i = 0; i < FilterNames.Length; i++)
        {
            var r = new Rect(panel.xMax - 24f - 70f * (FilterNames.Length - i) - 30f, panel.y + 18f, 64f, 28f);
            if (i == filter) Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(1f, 0.82f, 0.4f));
            if (GUI.Button(r, FilterNames[i]))
            {
                filter = i;
                page = 0;
            }
        }

        if (GUI.Button(new Rect(panel.xMax - 44f, panel.y + 18f, 28f, 28f), "x")) open = false;
    }

    void DrawStatStrip(Rect panel)
    {
        int minutes = Mathf.FloorToInt(Stat("playSec") / 60f);
        string line = "deaths " + Mathf.FloorToInt(Stat("deaths"))
            + "     clears " + Mathf.FloorToInt(Stat("clears"))
            + "     jumps " + Mathf.FloorToInt(Stat("jumps"))
            + "     played " + minutes + " min"
            + "     tallest stack " + Mathf.FloorToInt(Stat("stack"));
        GUI.Label(new Rect(panel.x + 24f, panel.y + 82f, 950f, 22f), line, descStyle);
    }

    void DrawGrid(Rect panel, List<Badge> list)
    {
        const float cardW = 312f;
        const float cardH = 74f;
        const float gapX = 12f;
        const float gapY = 8f;

        float x0 = panel.x + 24f;
        float y0 = panel.y + 116f;
        int first = page * PerPage;

        for (int i = 0; i < PerPage; i++)
        {
            int index = first + i;
            if (index >= list.Count) break;

            int col = i % 3;
            int row = i / 3;
            var r = new Rect(x0 + col * (cardW + gapX), y0 + row * (cardH + gapY), cardW, cardH);
            DrawCard(list[index], r);
        }

        if (list.Count == 0)
            GUI.Label(new Rect(panel.x, panel.y + 300f, panel.width, 30f), "nothing here yet", smallStyle);
    }

    void DrawCard(Badge b, Rect r)
    {
        Color tc = TierColor(b.tier);
        bool hidden = b.secret && !b.done;
        Color gray = new Color(0.25f, 0.26f, 0.32f);

        Fill(r, b.done ? Dim(tc, 0.3f) : new Color(0.12f, 0.12f, 0.17f, 0.95f));
        Fill(new Rect(r.x, r.y, 6f, r.height), b.done ? tc : gray);

        var icon = new Rect(r.x + 14f, r.y + 10f, 54f, 54f);
        Fill(icon, b.done ? tc : gray);
        GUI.Label(icon, b.done ? TierLetter(b.tier) : "?", iconStyle);

        string title = hidden ? "???" : b.name;
        string text = hidden ? "a secret. keep playing to find it." : b.desc;
        GUI.Label(new Rect(r.x + 78f, r.y + 4f, r.width - 140f, 22f), title, nameStyle);
        GUI.Label(new Rect(r.x + 78f, r.y + 26f, r.width - 88f, 30f), text, descStyle);
        GUI.Label(new Rect(r.xMax - 62f, r.y + 4f, 56f, 22f), "+" + b.Points, smallStyle);

        if (hidden) return;

        var bar = new Rect(r.x + 78f, r.yMax - 12f, r.width - 190f, 6f);
        Fill(bar, new Color(1f, 1f, 1f, 0.15f));
        Fill(new Rect(bar.x, bar.y, bar.width * Progress(b), bar.height), b.done ? tc : new Color(0.5f, 0.7f, 1f));
        GUI.Label(new Rect(bar.xMax + 6f, r.yMax - 20f, 110f, 20f), b.done ? "done" : ProgressText(b), descStyle);
    }

    void DrawFooter(Rect panel, int pages)
    {
        float y = panel.yMax - 44f;
        if (GUI.Button(new Rect(panel.x + 24f, y, 90f, 30f), "prev")) page--;
        if (GUI.Button(new Rect(panel.x + 124f, y, 90f, 30f), "next")) page++;

        GUI.Label(new Rect(panel.x + 230f, y, 200f, 30f), "page " + (page + 1) + " of " + pages, smallStyle);
        GUI.Label(new Rect(panel.xMax - 520f, y, 496f, 30f), "B closes     [ ] or the mouse wheel turns pages", descStyle);
    }
}
