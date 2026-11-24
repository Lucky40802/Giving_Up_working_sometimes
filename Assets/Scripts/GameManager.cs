using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager I;
    const int COLS = Levels.COLS;
    const int ROWS = Levels.ROWS;
    const float RESPAWN = 0.5f;
    const float PAD_FORCE = 28f;

    static readonly string[] Taunts =
    {
        "Rest in pieces.", "A foundation has been laid.", "Dead weight, finally useful.", "Solid effort.",
        "You will be remembered as a step.", "Giving up works sometimes.", "Another brick in the wall.",
    };

    class Pickup { public Vector2Int cell; public string kind; public bool taken; public GameObject go; }
    public enum Mode { Menu, Play, Clear, End, Pause }

    public Mode mode = Mode.Menu;
    public Sfx sfx;
    public float shake;
    public readonly Dictionary<Vector2Int, Corpse> corpses = new Dictionary<Vector2Int, Corpse>();
    public readonly List<Enemy> enemies = new List<Enemy>();

    List<LevelData> levels;
    public int levelIndex;
    bool[,] solid;
    readonly List<Vector2Int> spikes = new List<Vector2Int>();
    readonly Dictionary<Vector2Int, GameObject> spikeObjs = new Dictionary<Vector2Int, GameObject>();
    readonly List<Vector2Int> exits = new List<Vector2Int>();
    readonly List<Pickup> pickups = new List<Pickup>();
    readonly PlayerController[] players = new PlayerController[2];
    Vector2Int spawn, goal = new Vector2Int(-1, -1);
    GameObject levelRoot;
    SpriteRenderer goalGlow;

    public bool two;
    int[] scores = new int[2];
    int[] deaths = new int[2];
    int levelDeaths, winner;
    float levelTime, runTime, clearTimer, toastT;
    bool runFromStart, newBest, newBestRun;
    string toast = "";

    int menuState;   // 0 title, 1 select
    int cursor, sel;
    bool menuTwo;
    Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        new GameObject("Game").AddComponent<GameManager>();
    }

    void Awake()
    {
        I = this;
        levels = Levels.Get();
        cam = Camera.main;
        if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.12f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        sfx = gameObject.AddComponent<Sfx>();
        for (int i = 0; i < 2; i++)
        {
            var p = PlayerController.Create(i);
            p.Jumped = () => sfx.Play("jump");
            p.Landed = hard => { if (hard) { sfx.Play("land"); } };
            players[i] = p;
            p.SetAlive(false);
        }
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("--level=")) { StartGame(Mathf.Clamp(int.Parse(a.Substring(8)) - 1, 0, levels.Count - 1), System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--two") >= 0); return; }
        OpenMenu(0);
    }


    public bool Occupied(int c, int r)
    {
        if (c < 0 || c >= COLS || r >= ROWS) return true;
        if (r < 0) return false;
        if (solid[r, c]) return true;
        return Covered(new Vector2Int(c, r));
    }

    bool Covered(Vector2Int cell) => corpses.TryGetValue(cell, out var b) && b != null && b.kind != "portal";

    public PlayerController NearestPlayer(Vector2 gridPos)
    {
        PlayerController best = null;
        float bd = float.MaxValue;
        foreach (var p in players)
        {
            if (!p.active || !p.gameObject.activeSelf) continue;
            float d = (p.GridPos - gridPos).sqrMagnitude;
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }


    void OpenMenu(int state)
    {
        Time.timeScale = 1f;
        mode = Mode.Menu;
        menuState = state;
        cursor = 0;
        if (state == 1) sel = Mathf.Clamp(sel, 0, Mathf.Max(0, Records.Unlocked - 1));
        foreach (var p in players) p.SetAlive(false);
        ClearLevel();
    }

    void StartGame(int index, bool twoPlayers)
    {
        two = twoPlayers;
        players[1].active = two;
        players[0].active = true;
        players[0].two = two;
        players[1].two = two;
        scores = new int[2];
        deaths = new int[2];
        runTime = 0f;
        runFromStart = index == 0;
        newBestRun = false;
        sfx.Play("click");
        LoadLevel(index);
    }

    void ClearLevel()
    {
        if (levelRoot) Destroy(levelRoot);
        levelRoot = new GameObject("Level");
        corpses.Clear();
        enemies.Clear();
        spikes.Clear();
        spikeObjs.Clear();
        exits.Clear();
        pickups.Clear();
        goal = new Vector2Int(-1, -1);
        goalGlow = null;
    }

    static Vector2 W(int c, int r) => new Vector2(c + 0.5f, ROWS - r - 0.5f);

    void LoadLevel(int i)
    {
        levelIndex = i;
        ClearLevel();
        solid = new bool[ROWS, COLS];
        var map = levels[i].map;
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                char ch = map[r][c];
                solid[r, c] = ch == '#';
                ReadTile(ch, c, r);
            }
        BuildTerrain();
        levelDeaths = 0;
        levelTime = 0f;
        newBest = false;
        mode = Mode.Play;
        Time.timeScale = 1f;
        foreach (var p in players)
        {
            if (p.active) Respawn(p);
            else p.SetAlive(false);
        }
    }

    void ReadTile(char ch, int c, int r)
    {
        var parent = levelRoot.transform;
        if (ch == '^')
        {
            spikes.Add(new Vector2Int(c, r));
            var holder = new GameObject("spike");
            holder.transform.SetParent(parent, false);
            for (int k = 0; k < 3; k++)
            {
                float x = c + 0.0625f + k * 0.3125f + 0.15f;
                var s = Gfx.Make("t", holder.transform, Gfx.Tri, Gfx.Hex("e8e8f2"), new Vector2(x, ROWS - r - 1f), new Vector2(0.31f, 0.56f), 8);
                s.transform.position = new Vector3(x, ROWS - r - 1f, 0);
                var tip = Gfx.Make("tip", holder.transform, Gfx.Tri, Gfx.Hex("ff5d73"), Vector2.zero, new Vector2(0.15f, 0.25f), 9);
                tip.transform.position = new Vector3(x, ROWS - r - 1f + 0.31f, 0);
            }
            spikeObjs[new Vector2Int(c, r)] = holder;
        }
        else if (ch == 'P') spawn = new Vector2Int(c, r);
        else if (ch == 'G')
        {
            goal = new Vector2Int(c, r);
            var gp = W(c, r);
            goalGlow = Gfx.Make("glow", parent, Gfx.Square, new Color(0.5f, 1f, 0.8f, 0.15f), gp, new Vector2(1.25f, 1.25f), 2);
            Gfx.Make("door", parent, Gfx.Square, Gfx.Hex("2b6b5a"), gp + new Vector2(0, -0.03f), new Vector2(0.75f, 0.94f), 3);
            Gfx.Make("inner", parent, Gfx.Square, Gfx.Hex("7ff0cc"), gp + new Vector2(0, -0.08f), new Vector2(0.56f, 0.8f), 4);
            Gfx.Make("pole", parent, Gfx.Square, Gfx.Hex("c9c9e0"), gp + new Vector2(-0.31f, 0.4f), new Vector2(0.06f, 1.4f), 4);
            var f = Gfx.Make("flag", parent, Gfx.Tri, Gfx.Hex("ffd166"), gp + new Vector2(-0.12f, 1.0f), new Vector2(0.5f, 0.4f), 5);
            f.transform.rotation = Quaternion.Euler(0, 0, -90);
        }
        else if (ch == 'O')
        {
            exits.Add(new Vector2Int(c, r));
            var p = W(c, r);
            Gfx.Make("exitglow", parent, Gfx.Circle, new Color(0.3f, 0.5f, 1f, 0.3f), p, new Vector2(0.9f, 0.9f), 6);
            Gfx.Make("exit", parent, Gfx.Circle, Gfx.Hex("7aa7ff"), p, new Vector2(0.55f, 0.55f), 7);
            Gfx.Make("exitdot", parent, Gfx.Circle, Color.white, p, new Vector2(0.18f, 0.18f), 8);
        }
        else if (ch == 'j' || ch == 't')
        {
            var pk = new Pickup { cell = new Vector2Int(c, r), kind = ch == 'j' ? "pad" : "portal" };
            var col = ch == 'j' ? Gfx.Hex("7ef0c8") : Gfx.Hex("c77dff");
            var orb = Gfx.Make("orb", parent, Gfx.Circle, col, W(c, r), new Vector2(0.45f, 0.45f), 10);
            Gfx.Make("shine", orb.transform, Gfx.Circle, Color.white, new Vector2(-0.2f, 0.2f), new Vector2(0.3f, 0.3f), 11);
            pk.go = orb.gameObject;
            pickups.Add(pk);
        }
        else if (ch == 'E' || ch == 'W' || ch == 'C' || ch == 'B' || ch == 'K')
            enemies.Add(Enemy.Create(parent, ch, c, r));
    }

    void BuildTerrain()
    {
        var parent = levelRoot.transform;
        for (int r = 0; r < ROWS; r++)
        {
            int c = 0;
            while (c < COLS)
            {
                if (!solid[r, c]) { c++; continue; }
                int start = c;
                while (c < COLS && solid[r, c]) c++;
                var go = new GameObject("run");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3((start + c) / 2f, ROWS - r - 0.5f, 0);
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(c - start, 1f);
                for (int k = start; k < c; k++)
                {
                    float v = ((k * 7 + r * 13) % 5) * 0.012f;
                    Gfx.Make("tile", parent, Gfx.Square, new Color(0.16f + v, 0.17f + v, 0.27f + v), W(k, r), Vector2.one, 1);
                    if (r == 0 || !solid[r - 1, k])
                    {
                        Gfx.Make("cap", parent, Gfx.Square, Gfx.Hex("6f7ac9"), W(k, r) + new Vector2(0, 0.44f), new Vector2(1f, 0.12f), 2);
                    }
                }
            }
        }
    }

    void Respawn(PlayerController p)
    {
        int r = spawn.y;
        while (r > 0 && Occupied(spawn.x, r)) r--;
        float off = two ? (p.id == 0 ? -0.28f : 0.28f) : 0f;
        p.transform.position = new Vector3(spawn.x + 0.5f + off, ROWS - (r + 1) + PlayerController.H / 2f, 0);
        p.SetAlive(true);
        p.stun = 0f;
        p.power = "";
        p.respawnT = 0f;
        foreach (var pk in pickups) { pk.taken = false; pk.go.SetActive(true); }
    }

    public void OnCorpseLanded(Vector3 pos, bool hard)
    {
        Bit.Burst(pos, new[] { Gfx.Hex("9aa0c8") }, 5, 1.5f);
        if (hard) shake = Mathf.Max(shake, 0.12f);
    }

    public void BreakBody(Vector2Int cell)
    {
        if (!corpses.TryGetValue(cell, out var b) || b == null) return;
        corpses.Remove(cell);
        Bit.Burst(W(cell.x, cell.y), new[] { Gfx.Hex("8a5a6a"), Gfx.Hex("7a4f7e") }, 14, 6.5f);
        sfx.Play("smash");
        Destroy(b.gameObject);
    }

    public void Die(PlayerController p)
    {
        if (mode != Mode.Play || !p.gameObject.activeSelf) return;
        deaths[p.id]++;
        levelDeaths++;
        var gp = p.GridPos;
        int c = Mathf.Clamp(Mathf.FloorToInt(gp.x), 0, COLS - 1);
        int r = Mathf.Clamp(Mathf.FloorToInt(gp.y), 0, ROWS - 1);
        while (r > 0 && Occupied(c, r)) r--;
        if (!Occupied(c, r))
        {
            while (r + 1 < ROWS && !Occupied(c, r + 1) && new Vector2Int(c, r + 1) != goal) r++;
            var cell = new Vector2Int(c, r);
            if (corpses.ContainsKey(cell)) BreakBody(cell);
            float startTop = gp.y + PlayerController.H / 2f - 1f;
            string kind = p.power == "" ? "normal" : p.power;
            corpses[cell] = Corpse.Create(levelRoot.transform, cell, Mathf.Max(0f, r - startTop), corpses.Count, p.face, kind);
        }
        Bit.Burst(p.transform.position, new[] { p.bodyColor, Gfx.Hex("ff5d73"), Gfx.Hex("8a5a6a") }, 22, 8f);
        sfx.Play("die");
        shake = 0.3f;
        toast = Taunts[Random.Range(0, Taunts.Length)];
        toastT = 1.6f;
        p.power = "";
        p.respawnT = RESPAWN;
        p.SetAlive(false);
    }

    void Win(PlayerController p)
    {
        if (mode != Mode.Play) return;
        mode = Mode.Clear;
        clearTimer = 1.2f;
        winner = p.id;
        scores[p.id]++;
        foreach (var q in players) q.rb.simulated = false;
        newBest = Records.Submit(levelIndex, levelTime);
        sfx.Play(newBest ? "best" : "win");
        Bit.Burst(W(goal.x, goal.y), new[] { Gfx.Hex("ffd166"), Gfx.Hex("7ef0c8"), Color.white }, 36, 9f);
    }


    void Update()
    {
        if (Inp.Down(Key.M)) ToggleSound();
        toastT -= Time.unscaledDeltaTime;
        if (toastT <= 0f) toast = "";

        switch (mode)
        {
            case Mode.Menu: UpdateMenu(); break;
            case Mode.Play: UpdatePlay(); break;
            case Mode.Pause:
                if (Inp.Down(Key.Escape)) { Time.timeScale = 1f; mode = Mode.Play; sfx.Play("click"); }
                else if (Inp.Down(Key.Enter, Key.NumpadEnter)) OpenMenu(1);
                break;
            case Mode.Clear:
                clearTimer -= Time.deltaTime;
                if (clearTimer <= 0f)
                {
                    foreach (var q in players) q.rb.simulated = true;
                    if (levelIndex + 1 < levels.Count) LoadLevel(levelIndex + 1);
                    else FinishRun();
                }
                break;
            case Mode.End:
                if (Inp.Down(Key.Enter, Key.NumpadEnter, Key.Space)) OpenMenu(0);
                break;
        }
        if (mode != Mode.Menu && SoundButtonClicked()) ToggleSound();
    }

    void FinishRun()
    {
        mode = Mode.End;
        foreach (var p in players) p.SetAlive(false);
        if (runFromStart) newBestRun = Records.SubmitRun(runTime);
    }

    void ToggleSound() { sfx.Toggle(); }

    void UpdatePlay()
    {
        levelTime += Time.deltaTime;
        runTime += Time.deltaTime;
        if (Inp.Down(Key.Escape)) { Time.timeScale = 0f; mode = Mode.Pause; sfx.Play("click"); return; }
        if (Inp.Down(Key.Q, Key.Backspace)) { LoadLevel(levelIndex); return; }

        foreach (var p in players)
        {
            if (!p.active) continue;
            if (!p.gameObject.activeSelf)
            {
                p.respawnT -= Time.deltaTime;
                if (p.respawnT <= 0f) Respawn(p);
                continue;
            }
            if (p.DiePressed()) { Die(p); continue; }
            CheckPad(p);
            CheckPortal(p);
            CheckPickups(p);
            if (mode == Mode.Play) CheckHazards(p);
        }
        foreach (var kv in spikeObjs) kv.Value.SetActive(!Covered(kv.Key));
        if (goalGlow != null) goalGlow.color = new Color(0.5f, 1f, 0.8f, 0.1f + 0.08f * Mathf.Sin(Time.time * 4f));
    }

    static Rect PlayerRect(PlayerController p)
    {
        var g = p.GridPos;
        return new Rect(g.x - 0.25f, g.y - 0.34375f, 0.5f, 0.6875f);
    }

    void CheckPad(PlayerController p)
    {
        var g = p.GridPos;
        var c = new Vector2Int(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y + PlayerController.H / 2f + 0.08f));
        if (!corpses.TryGetValue(c, out var b) || b.kind != "pad") return;
        var v = p.rb.linearVelocity;
        if (v.y > 1f) return;
        v.y = PAD_FORCE;
        p.rb.linearVelocity = v;
        b.Kick();
        sfx.Play("pad");
        Bit.Burst(new Vector2(c.x + 0.5f, ROWS - c.y), new[] { Gfx.Hex("7ef0c8") }, 8, 3f);
    }

    Vector2Int PortalDestination(Vector2Int from)
    {
        var best = new Vector2Int(-1, -1);
        float bd = float.MaxValue;
        foreach (var kv in corpses)
        {
            if (kv.Value.kind != "portal" || kv.Key == from) continue;
            float d = ((Vector2)(kv.Key - from)).magnitude;
            if (d < bd) { bd = d; best = kv.Key; }
        }
        if (best.x >= 0) return best;
        foreach (var e in exits)
        {
            float d = ((Vector2)(e - from)).magnitude;
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    void CheckPortal(PlayerController p)
    {
        if (p.portalCd > 0f) return;
        var g = p.GridPos;
        var c = new Vector2Int(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y));
        if (!corpses.TryGetValue(c, out var b) || b.kind != "portal") return;
        var dest = PortalDestination(c);
        if (dest.x < 0) return;
        Bit.Burst(p.transform.position, new[] { Gfx.Hex("c77dff") }, 14, 3f);
        p.transform.position = new Vector3(dest.x + 0.5f, ROWS - (dest.y + 1) + PlayerController.H / 2f, 0);
        p.portalCd = 0.7f;
        Bit.Burst(p.transform.position, new[] { Gfx.Hex("c77dff") }, 14, 3f);
        sfx.Play("portal");
    }

    void CheckPickups(PlayerController p)
    {
        var pr = PlayerRect(p);
        foreach (var pk in pickups)
        {
            if (pk.taken) continue;
            var cr = new Rect(pk.cell.x + 0.19f, pk.cell.y + 0.19f, 0.62f, 0.62f);
            if (!pr.Overlaps(cr)) continue;
            pk.taken = true;
            pk.go.SetActive(false);
            p.power = pk.kind;
            sfx.Play("pickup");
            Bit.Burst(W(pk.cell.x, pk.cell.y), new[] { Color.white }, 10, 3f);
        }
    }

    void CheckHazards(PlayerController p)
    {
        var pr = PlayerRect(p);
        foreach (var s in spikes)
        {
            if (Covered(s)) continue;
            if (pr.Overlaps(new Rect(s.x + 0.125f, s.y + 0.4375f, 0.75f, 0.5625f))) { Die(p); return; }
        }
        foreach (var e in enemies)
        {
            if (!pr.Overlaps(e.HurtRect())) continue;
            if (e.kind == 'K') e.Knock(p);
            else { Die(p); return; }
        }
        if (goal.x >= 0 && pr.Overlaps(new Rect(goal.x + 0.125f, goal.y + 0.0625f, 0.75f, 0.9375f))) Win(p);
    }

    void LateUpdate()
    {
        float aspect = Mathf.Max(0.1f, (float)Screen.width / Screen.height);
        cam.orthographicSize = Mathf.Max(ROWS / 2f, COLS / 2f / aspect);
        var off = shake > 0f ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * shake * 0.5f : Vector3.zero;
        cam.transform.position = new Vector3(COLS / 2f, ROWS / 2f, -10f) + off;
        shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 1.3f);
    }


    float Scale => Screen.height / 480f;
    float VW => Screen.width / Scale;

    Vector2 MouseV()
    {
        if (Mouse.current == null) return new Vector2(-999, -999);
        var m = Mouse.current.position.ReadValue();
        return new Vector2(m.x / Scale, (Screen.height - m.y) / Scale);
    }

    bool Clicked() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    Rect ItemRect(int i) => new Rect(VW / 2f - 150f, 210f + i * 42f, 300f, 36f);

    Rect CellRect(int i)
    {
        float ox = (VW - (5 * 144f + 4 * 8f)) / 2f;
        return new Rect(ox + (i % 5) * 152f, 112f + (i / 5) * 86f, 144f, 78f);
    }

    Rect SoundRect() => new Rect(VW - 126f, 60f, 116f, 26f);

    bool SoundButtonClicked() => Clicked() && SoundRect().Contains(MouseV());

    void UpdateMenu()
    {
        var m = MouseV();
        if (menuState == 0)
        {
            if (Inp.Down(Key.UpArrow, Key.W)) { cursor = (cursor + 3) % 4; sfx.Play("move"); }
            if (Inp.Down(Key.DownArrow, Key.S)) { cursor = (cursor + 1) % 4; sfx.Play("move"); }
            for (int i = 0; i < 4; i++)
                if (ItemRect(i).Contains(m) && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0f && cursor != i) { cursor = i; sfx.Play("move"); }
            bool go = Inp.Down(Key.Enter, Key.NumpadEnter, Key.Space);
            if (Clicked()) for (int i = 0; i < 4; i++) if (ItemRect(i).Contains(m)) { cursor = i; go = true; }
            if (go) Activate(cursor);
        }
        else
        {
            int n = levels.Count;
            if (Inp.Down(Key.LeftArrow, Key.A)) { sel = Mathf.Max(0, sel - 1); sfx.Play("move"); }
            if (Inp.Down(Key.RightArrow, Key.D)) { sel = Mathf.Min(n - 1, sel + 1); sfx.Play("move"); }
            if (Inp.Down(Key.UpArrow, Key.W)) { sel = Mathf.Max(0, sel - 5); sfx.Play("move"); }
            if (Inp.Down(Key.DownArrow, Key.S)) { sel = Mathf.Min(n - 1, sel + 5); sfx.Play("move"); }
            if (Inp.Down(Key.Tab)) { menuTwo = !menuTwo; sfx.Play("click"); }
            if (Inp.Down(Key.U)) { Records.UnlockAll(n); sfx.Play("toggle"); }
            if (Inp.Down(Key.Delete)) { Records.Reset(); sel = 0; sfx.Play("nope"); }
            if (Inp.Down(Key.Escape, Key.Backspace)) { OpenMenu(0); sfx.Play("click"); return; }
            bool go = Inp.Down(Key.Enter, Key.NumpadEnter, Key.Space);
            if (Clicked()) for (int i = 0; i < n; i++) if (CellRect(i).Contains(m)) { sel = i; go = true; }
            if (go) Launch(sel);
        }
    }

    void Activate(int i)
    {
        if (i == 0) StartGame(0, false);
        else if (i == 1) StartGame(0, true);
        else if (i == 2) { menuState = 1; sfx.Play("click"); }
        else ToggleSound();
    }

    void Launch(int i)
    {
        if (i >= Records.Unlocked) { sfx.Play("nope"); return; }
        StartGame(i, menuTwo);
    }


    GUIStyle style;

    void Txt(float x, float y, float w, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
    {
        if (style == null) style = new GUIStyle(GUI.skin.label) { wordWrap = false };
        style.fontSize = size;
        style.alignment = a;
        var r = new Rect(x, y, w, size * 1.4f * (s.Split('\n').Length));
        style.normal.textColor = new Color(0.04f, 0.04f, 0.08f);
        foreach (var d in new[] { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2) })
            GUI.Label(new Rect(r.x + d.x, r.y + d.y, r.width, r.height), s, style);
        style.normal.textColor = c;
        GUI.Label(r, s, style);
    }

    void Box(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        float s = Scale;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        float vw = VW;
        var white = new Color(0.91f, 0.91f, 0.96f);

        if (mode == Mode.Menu)
        {
            Box(new Rect(0, 0, vw, 480), new Color(0.05f, 0.05f, 0.1f, 1f));
            if (menuState == 0) DrawTitle(vw); else DrawSelect(vw);
            return;
        }

        Txt(12, 4, 460, "LEVEL " + (levelIndex + 1) + "/" + levels.Count + "  " + levels[levelIndex].name, 16, white);
        string score = two
            ? "P1 " + scores[0] + " wins " + deaths[0] + " deaths  |  P2 " + scores[1] + " wins " + deaths[1] + " deaths"
            : "DEATHS " + deaths[0] + "   BODIES " + corpses.Count;
        Txt(12, 26, 560, score, 15, white);
        Txt(vw - 412, 4, 400, "TIME " + Records.Fmt(levelTime) + "   BEST " + Records.Fmt(Records.Best(levelIndex)), 16, white, TextAnchor.UpperRight);
        Txt(vw - 412, 26, 400, "RUN " + Records.Fmt(runTime), 15, white, TextAnchor.UpperRight);

        var sr = SoundRect();
        Box(sr, new Color(0, 0, 0, 0.5f));
        Txt(sr.x, sr.y + 3, sr.width, sfx.muted ? "Sound: OFF [M]" : "Sound: ON [M]", 13, white, TextAnchor.UpperCenter);

        var parts = new List<string>();
        foreach (var p in players)
            if (p.active && p.power != "")
                parts.Add((two ? "P" + (p.id + 1) + ": " : "") + "next body = " + (p.power == "pad" ? "JUMP PAD" : "PORTAL"));
        if (parts.Count > 0) Txt(0, 84, vw, string.Join("    ", parts), 16, new Color(0.49f, 0.94f, 0.78f), TextAnchor.UpperCenter);
        if (toast != "") Txt(0, 52, vw, toast, 22, new Color(1f, 0.56f, 0.64f), TextAnchor.UpperCenter);
        if (mode == Mode.Play) Txt(0, 446, vw, levels[levelIndex].hint, 15, new Color(0.73f, 0.74f, 0.91f), TextAnchor.UpperCenter);

        for (int i = 0; i < Mathf.Min(levelDeaths, 20); i++)
        {
            var tr = new Rect(8 + i * 14, 424, 10, 14);
            Box(tr, new Color(0.54f, 0.56f, 0.69f));
            Box(new Rect(tr.x + 4, tr.y + 3, 2, 8), new Color(0.1f, 0.06f, 0.13f));
            Box(new Rect(tr.x + 2, tr.y + 5, 6, 2), new Color(0.1f, 0.06f, 0.13f));
        }

        if (mode == Mode.Pause)
        {
            Box(new Rect(0, 0, vw, 480), new Color(0, 0, 0, 0.5f));
            Txt(0, 130, vw, "PAUSED", 48, new Color(1f, 0.82f, 0.4f), TextAnchor.UpperCenter);
            Txt(0, 210, vw, "Esc: resume\nEnter: level select", 18, white, TextAnchor.UpperCenter);
        }
        else if (mode == Mode.Clear)
        {
            Txt(0, 150, vw, newBest ? "NEW BEST!" : "CLEARED", 48, new Color(1f, 0.82f, 0.4f), TextAnchor.UpperCenter);
            Txt(0, 230, vw, two ? "Player " + (winner + 1) + " reached the exit first" : Records.Fmt(levelTime), 20, white, TextAnchor.UpperCenter);
        }
        else if (mode == Mode.End)
        {
            Box(new Rect(0, 0, vw, 480), new Color(0, 0, 0, 0.6f));
            Txt(0, 70, vw, "YOU MADE IT", 48, new Color(1f, 0.82f, 0.4f), TextAnchor.UpperCenter);
            Txt(0, 160, vw, EndText(), 18, white, TextAnchor.UpperCenter);
        }
    }

    string EndText()
    {
        var l = new List<string>();
        if (two) { l.Add("P1: " + scores[0] + " wins, " + deaths[0] + " deaths"); l.Add("P2: " + scores[1] + " wins, " + deaths[1] + " deaths"); }
        else { l.Add("It took " + deaths[0] + " deaths."); l.Add("Every one of them was load-bearing."); }
        if (runFromStart)
        {
            l.Add("");
            l.Add("Run time  " + Records.Fmt(runTime));
            l.Add("Best run  " + Records.Fmt(Records.BestRun));
            if (newBestRun) l.Add("NEW RECORD!");
        }
        l.Add("");
        l.Add("Press Enter to go back to the menu");
        return string.Join("\n", l);
    }

    void DrawTitle(float vw)
    {
        float bob = Mathf.Sin(Time.unscaledTime * 2f) * 4f;
        Txt(0, 60 + bob, vw, "GIVING UP", 52, new Color(1f, 0.82f, 0.4f), TextAnchor.UpperCenter);
        Txt(0, 120 + bob, vw, "WORKS SOMETIMES", 34, new Color(1f, 0.7f, 0.28f), TextAnchor.UpperCenter);
        Txt(0, 172, vw, "Every body you leave behind becomes part of the level.", 16, new Color(0.73f, 0.74f, 0.91f), TextAnchor.UpperCenter);
        string[] items = { "Play  (1 player)", "Play  (2 players)", "Level select", "Sound:  " + (sfx.muted ? "OFF" : "ON") };
        for (int i = 0; i < 4; i++)
        {
            var r = ItemRect(i);
            if (i == cursor)
            {
                Box(r, new Color(1, 1, 1, 0.1f));
                Box(new Rect(r.x, r.y, 4, r.height), new Color(1f, 0.82f, 0.4f));
            }
            Txt(r.x, r.y + 6, r.width, items[i], 20, i == cursor ? Color.white : new Color(0.6f, 0.63f, 0.78f), TextAnchor.UpperCenter);
        }
        Txt(0, 430, vw, "Up / Down + Enter, or click.   M toggles sound anywhere.", 14, new Color(0.49f, 0.51f, 0.69f), TextAnchor.UpperCenter);
        Txt(0, 452, vw, "1P: A D move, W Space jump, R die    2P: P1 as before, P2 arrows + Enter to die", 13, new Color(0.42f, 0.44f, 0.6f), TextAnchor.UpperCenter);
    }

    void DrawSelect(float vw)
    {
        Txt(0, 36, vw, "SELECT LEVEL", 34, new Color(1f, 0.82f, 0.4f), TextAnchor.UpperCenter);
        Txt(0, 82, vw, (menuTwo ? "Mode: 2 PLAYERS" : "Mode: 1 PLAYER") + "    (Tab to switch)", 15, new Color(0.73f, 0.74f, 0.91f), TextAnchor.UpperCenter);
        for (int i = 0; i < levels.Count; i++)
        {
            var r = CellRect(i);
            bool open = i < Records.Unlocked;
            Box(r, open ? new Color(0.16f, 0.17f, 0.27f) : new Color(0.1f, 0.1f, 0.16f));
            if (i == sel)
            {
                var gold = new Color(1f, 0.82f, 0.4f);
                Box(new Rect(r.x, r.y, r.width, 3), gold); Box(new Rect(r.x, r.yMax - 3, r.width, 3), gold);
                Box(new Rect(r.x, r.y, 3, r.height), gold); Box(new Rect(r.xMax - 3, r.y, 3, r.height), gold);
            }
            var col = open ? Color.white : new Color(0.33f, 0.35f, 0.48f);
            Txt(r.x + 8, r.y + 4, 100, (i + 1).ToString(), 22, col);
            string nm = levels[i].name;
            if (nm.Length > 14) nm = nm.Substring(0, 13) + ".";
            Txt(r.x + 8, r.y + 32, 134, nm, 13, col);
            float b = Records.Best(i);
            Txt(r.x + 8, r.y + 54, 134, open ? Records.Fmt(b) : "LOCKED", 13, open && b >= 0 ? new Color(0.49f, 0.94f, 0.78f) : new Color(0.42f, 0.44f, 0.6f));
        }
        Txt(0, 440, vw, "Arrows move, Enter plays, Esc goes back.   U unlocks all, Delete erases progress", 14, new Color(0.49f, 0.51f, 0.69f), TextAnchor.UpperCenter);
        if (Records.BestRun >= 0f) Txt(0, 460, vw, "Best full run: " + Records.Fmt(Records.BestRun), 14, new Color(0.49f, 0.94f, 0.78f), TextAnchor.UpperCenter);
    }
}
