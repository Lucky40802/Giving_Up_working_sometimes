using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Omnitricks : MonoBehaviour
{
    class Trick
    {
        public string id;
        public string name;
        public string glyph;
        public int unlockAt;
        public Color color;
        public string desc;

        public Trick(string id, string name, string glyph, int unlockAt, Color color, string desc)
        {
            this.id = id;
            this.name = name;
            this.glyph = glyph;
            this.unlockAt = unlockAt;
            this.color = color;
            this.desc = desc;
        }
    }

    const string PrefDeaths = "omni_deaths";
    const string PrefPick = "omni_pick";
    const float Slot = 48f;
    const float Gap = 6f;

    readonly List<Trick> tricks = new List<Trick>();
    readonly Dictionary<Vector2Int, Corpse> seenBody = new Dictionary<Vector2Int, Corpse>();
    readonly Dictionary<Corpse, float> fuse = new Dictionary<Corpse, float>();
    readonly Dictionary<Corpse, float> crumble = new Dictionary<Corpse, float>();
    readonly Dictionary<PlayerController, string> granted = new Dictionary<PlayerController, string>();
    readonly Dictionary<PlayerController, bool> wasAlive = new Dictionary<PlayerController, bool>();
    readonly Dictionary<PlayerController, Vector3> lastPos = new Dictionary<PlayerController, Vector3>();
    readonly Dictionary<PlayerController, float> boostCd = new Dictionary<PlayerController, float>();

    PlayerController[] players = new PlayerController[0];
    PlayerController reviveWho;
    Vector3 reviveAt;
    bool pendingRevive;
    int reviveCharges;
    int picked;
    int deaths;
    int lastLevel = -1;
    float scan;
    float bannerT;
    string banner = "";
    bool hidden;

    Texture2D pixel;
    GUIStyle small;
    GUIStyle glyphStyle;
    GUIStyle bannerStyle;
    GUIStyle info;

    Trick Armed => tricks[picked];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Omnitricks").AddComponent<Omnitricks>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        tricks.Add(new Trick("revive", "Revive", "+", 0, new Color(0.35f, 0.85f, 0.45f),
            "Die, leave a body, then stand back up where you fell. One per level."));
        tricks.Add(new Trick("pad", "Jump Pad", "^", 3, new Color(0.49f, 0.94f, 0.78f),
            "Your next body bounces anyone who lands on it."));
        tricks.Add(new Trick("portal", "Portal", "O", 8, new Color(0.78f, 0.49f, 1f),
            "Bodies link up. Walk into one, pop out of another."));
        tricks.Add(new Trick("boost", "Boost", ">", 15, new Color(1f, 0.7f, 0.28f),
            "Stand on it and get fired sideways."));
        tricks.Add(new Trick("bomb", "Bomb", "*", 25, new Color(1f, 0.36f, 0.45f),
            "Explodes after 2 seconds and smashes the bodies around it."));
        tricks.Add(new Trick("crumble", "Crumble", "~", 40, new Color(0.7f, 0.63f, 0.56f),
            "A shaky platform. Falls apart after about a second of weight."));

        deaths = PlayerPrefs.GetInt(PrefDeaths, 0);
        picked = Mathf.Clamp(PlayerPrefs.GetInt(PrefPick, 0), 0, tricks.Count - 1);
        if (!Unlocked(picked)) picked = 0;
    }

    bool Unlocked(int i)
    {
        return deaths >= tricks[i].unlockAt;
    }

    void Announce(string text)
    {
        banner = text;
        bannerT = 2.6f;
    }

    void Pick(int i)
    {
        if (!Unlocked(i))
        {
            Announce("LOCKED - " + (tricks[i].unlockAt - deaths) + " more deaths");
            return;
        }
        picked = i;
        PlayerPrefs.SetInt(PrefPick, i);
    }

    void Cycle(int dir)
    {
        for (int step = 1; step <= tricks.Count; step++)
        {
            int i = (picked + dir * step + tricks.Count * 2) % tricks.Count;
            if (!Unlocked(i)) continue;
            Pick(i);
            return;
        }
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
        for (int i = 0; i < digits.Length && i < tricks.Count; i++)
        {
            if (kb[digits[i]].wasPressedThisFrame) Pick(i);
        }

        if (kb.eKey.wasPressedThisFrame) Cycle(1);
        if (kb.zKey.wasPressedThisFrame) Cycle(-1);
        if (kb.hKey.wasPressedThisFrame) hidden = !hidden;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        HandleKeys();
        if (bannerT > 0f) bannerT -= Time.unscaledDeltaTime;

        scan -= Time.unscaledDeltaTime;
        if (scan <= 0f)
        {
            scan = 0.5f;
            players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (gm.levelIndex != lastLevel) NewLevel(gm);

        TrackBodies(gm);
        foreach (var p in players)
        {
            if (p == null) continue;
            TrackPlayer(p);
            if (!p.gameObject.activeSelf) continue;
            var under = Under(gm, p);
            if (under == null) continue;
            if (under.kind == "boost") DoBoost(p, under);
            if (under.kind == "crumble") DoCrumble(gm, under);
        }
        TickFuses(gm);
    }

    void NewLevel(GameManager gm)
    {
        lastLevel = gm.levelIndex;
        seenBody.Clear();
        foreach (var kv in gm.corpses) seenBody[kv.Key] = kv.Value;
        fuse.Clear();
        crumble.Clear();
        reviveCharges = 1;
        pendingRevive = false;
    }

    void TrackBodies(GameManager gm)
    {
        var gone = new List<Vector2Int>();
        foreach (var cell in seenBody.Keys)
        {
            if (!gm.corpses.ContainsKey(cell)) gone.Add(cell);
        }
        foreach (var cell in gone) seenBody.Remove(cell);

        foreach (var kv in gm.corpses)
        {
            seenBody.TryGetValue(kv.Key, out var old);
            if (old == kv.Value) continue;
            seenBody[kv.Key] = kv.Value;

            deaths++;
            PlayerPrefs.SetInt(PrefDeaths, deaths);
            CheckUnlocks();
            if (kv.Value != null && kv.Value.kind == "normal") Tag(kv.Value);
        }
    }

    void CheckUnlocks()
    {
        for (int i = 1; i < tricks.Count; i++)
        {
            if (tricks[i].unlockAt != deaths) continue;
            Announce("UNLOCKED: " + tricks[i].name.ToUpper());
            Pick(i);
        }
    }

    void Tag(Corpse body)
    {
        var t = Armed;
        if (t.id != "boost" && t.id != "bomb" && t.id != "crumble") return;

        body.kind = t.id;
        var vis = body.transform.Find("vis");
        Transform parent = vis != null ? vis : body.transform;
        Gfx.Make("mark", parent, Gfx.Square, t.color, new Vector2(0f, -0.05f), new Vector2(0.5f, 0.5f), 9);

        if (t.id == "bomb") fuse[body] = 2f;
    }

    void TrackPlayer(PlayerController p)
    {
        bool alive = p.gameObject.activeSelf;
        wasAlive.TryGetValue(p, out bool before);

        if (before && !alive && p.respawnT > 0f) OnDeath(p);
        if (!before && alive && pendingRevive && reviveWho == p) Revive(p);
        if (alive) lastPos[p] = p.transform.position;
        wasAlive[p] = alive;

        string want = Armed.id == "pad" || Armed.id == "portal" ? Armed.id : "";
        granted.TryGetValue(p, out string had);
        if (!string.IsNullOrEmpty(had) && had != want && p.power == had) p.power = "";
        if (want != "" && alive && p.power == "") p.power = want;
        granted[p] = want;
    }

    void OnDeath(PlayerController p)
    {
        if (Armed.id != "revive" || reviveCharges <= 0) return;
        if (!lastPos.TryGetValue(p, out var where)) return;
        pendingRevive = true;
        reviveWho = p;
        reviveAt = where;
    }

    void Revive(PlayerController p)
    {
        pendingRevive = false;
        reviveCharges--;
        p.transform.position = reviveAt + Vector3.up * 0.6f;
        p.rb.linearVelocity = Vector2.zero;
        p.stun = 0f;
        Bit.Burst(p.transform.position, new[] { tricks[0].color, Color.white }, 16, 4f);
        Announce("REVIVED");
    }

    Corpse Under(GameManager gm, PlayerController p)
    {
        var g = p.GridPos;
        var cell = new Vector2Int(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y + PlayerController.H / 2f + 0.08f));
        gm.corpses.TryGetValue(cell, out var body);
        return body;
    }

    void DoBoost(PlayerController p, Corpse body)
    {
        boostCd.TryGetValue(p, out float cd);
        if (Time.time < cd) return;

        var v = p.rb.linearVelocity;
        if (v.y > 1f) return;
        v.x = p.face * 18f;
        v.y = Mathf.Max(v.y, 2f);
        p.rb.linearVelocity = v;
        p.stun = 0.25f;
        boostCd[p] = Time.time + 0.35f;
        body.Kick();
        Bit.Burst(p.transform.position, new[] { tricks[3].color }, 8, 4f);
    }

    void DoCrumble(GameManager gm, Corpse body)
    {
        crumble.TryGetValue(body, out float t);
        t += Time.deltaTime;
        crumble[body] = t;

        if (Mathf.Repeat(t, 0.2f) < Time.deltaTime) body.Kick();
        if (t < 0.9f) return;

        crumble.Remove(body);
        gm.BreakBody(body.cell);
    }

    void TickFuses(GameManager gm)
    {
        var due = new List<Corpse>();
        foreach (var body in new List<Corpse>(fuse.Keys))
        {
            if (body == null)
            {
                fuse.Remove(body);
                continue;
            }
            fuse[body] -= Time.deltaTime;
            if (Mathf.Repeat(fuse[body], 0.4f) < Time.deltaTime) body.Kick();
            if (fuse[body] <= 0f) due.Add(body);
        }
        foreach (var body in due) Explode(gm, body);
    }

    void Explode(GameManager gm, Corpse body)
    {
        fuse.Remove(body);
        var c = body.cell;
        var world = new Vector2(c.x + 0.5f, Levels.ROWS - c.y - 0.5f);
        Bit.Burst(world, new[] { Color.white, tricks[4].color }, 24, 9f);

        Vector2Int[] around = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in around) gm.BreakBody(c + d);
    }

    void MakeStyles()
    {
        small = new GUIStyle(GUI.skin.label);
        small.fontSize = 12;
        small.alignment = TextAnchor.MiddleCenter;
        small.normal.textColor = Color.white;

        glyphStyle = new GUIStyle(small);
        glyphStyle.fontSize = 30;
        glyphStyle.fontStyle = FontStyle.Bold;

        bannerStyle = new GUIStyle(small);
        bannerStyle.fontSize = 28;
        bannerStyle.fontStyle = FontStyle.Bold;

        info = new GUIStyle(small);
        info.alignment = TextAnchor.MiddleRight;
        info.wordWrap = false;
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, pixel);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (small == null) MakeStyles();

        if (bannerT > 0f)
        {
            bannerStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, Mathf.Clamp01(bannerT / 0.5f));
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 50f), banner, bannerStyle);
        }
        if (hidden) return;

        float total = tricks.Count * (Slot + Gap) - Gap;
        float x0 = Screen.width - total - 16f;
        float y0 = Screen.height - Slot - 26f;
        Fill(new Rect(x0 - 8f, y0 - 8f, total + 16f, Slot + 34f), new Color(0f, 0f, 0f, 0.55f));

        int hover = -1;
        for (int i = 0; i < tricks.Count; i++)
        {
            var r = new Rect(x0 + i * (Slot + Gap), y0, Slot, Slot);
            if (r.Contains(Event.current.mousePosition)) hover = i;
            DrawSlot(i, r);
        }

        if (hover >= 0)
            GUI.Label(new Rect(x0 + total - 700f, y0 - 34f, 700f, 22f), tricks[hover].name + " - " + tricks[hover].desc, info);
        DrawProgress(x0, y0 + Slot + 6f, total);
    }

    void DrawSlot(int i, Rect r)
    {
        var t = tricks[i];
        bool open = Unlocked(i);

        if (i == picked)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(1f, 0.85f, 0.2f, pulse));
        }

        Color shade = open ? new Color(t.color.r * 0.55f, t.color.g * 0.55f, t.color.b * 0.55f) : new Color(0.15f, 0.15f, 0.18f);
        Fill(r, shade);

        GUI.Label(r, open ? t.glyph : "?", glyphStyle);
        GUI.Label(new Rect(r.x + 2f, r.y, 20f, 16f), (i + 1).ToString(), small);

        var foot = new Rect(r.x, r.y + r.height - 20f, r.width, 18f);
        if (!open) GUI.Label(foot, t.unlockAt.ToString(), small);
        else if (t.id == "revive") GUI.Label(foot, "x" + reviveCharges, small);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Pick(i);
    }

    void DrawProgress(float x, float y, float w)
    {
        int next = -1;
        for (int i = 0; i < tricks.Count; i++)
        {
            if (!Unlocked(i)) { next = i; break; }
        }

        Fill(new Rect(x, y, w, 6f), new Color(1f, 1f, 1f, 0.15f));
        if (next < 0)
        {
            Fill(new Rect(x, y, w, 6f), new Color(1f, 0.85f, 0.2f));
            return;
        }

        int from = next > 0 ? tricks[next - 1].unlockAt : 0;
        float frac = Mathf.InverseLerp(from, tricks[next].unlockAt, deaths);
        Fill(new Rect(x, y, w * frac, 6f), tricks[next].color);
    }
}