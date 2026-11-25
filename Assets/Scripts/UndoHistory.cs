using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class GameManager
{
    class Remains
    {
        public Vector2Int cell;
        public string kind;
        public int shade;
        public int facing;
        public bool marked;
        public Color mark;
        public float fuse;
    }

    class Snapshot
    {
        public bool action;
        public int level;
        public int levelDeaths;
        public int winner;
        public float levelTime;
        public float runTime;
        public bool newBest;
        public int revives;
        public int[] deaths;
        public int[] scores;
        public bool[] taken;
        public PlayerController.State[] players;
        public Enemy.State[] foes;
        public Remains[] bodies;
    }

    const float SampleStep = 0.05f;
    const float HoldDelay = 0.35f;
    const float RewindSpeed = 40f;
    const int HistoryLimit = 24000;

    static readonly Key[] ActionKeys =
    {
        Key.A, Key.D, Key.W, Key.Space, Key.R, Key.LeftArrow, Key.RightArrow,
        Key.UpArrow, Key.Enter, Key.NumpadEnter, Key.Q, Key.Backspace
    };

    readonly List<Snapshot> history = new List<Snapshot>();
    Omnitricks omni;
    bool rewinding;
    float heldFor;
    float stepBudget;
    float sinceSample;

    bool UndoTick()
    {
        if (mode == Mode.Menu)
        {
            history.Clear();
            EndRewind();
            return false;
        }

        var kb = Keyboard.current;
        if (kb == null || mode == Mode.Pause) return false;

        if (!kb.uKey.isPressed)
        {
            EndRewind();
        }
        else if (kb.uKey.wasPressedThisFrame && history.Count > 0)
        {
            rewinding = true;
            heldFor = 0f;
            stepBudget = 0f;
            bool toStart = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            RewindTo(toStart ? 0 : LastAction());
        }
        else if (rewinding)
        {
            KeepRewinding();
        }

        if (rewinding)
        {
            Time.timeScale = 0f;
            return true;
        }

        if (mode == Mode.Play) Record();
        return false;
    }

    void KeepRewinding()
    {
        heldFor += Time.unscaledDeltaTime;
        if (heldFor < HoldDelay) return;

        stepBudget += Time.unscaledDeltaTime * RewindSpeed;
        int steps = Mathf.FloorToInt(stepBudget);
        if (steps <= 0) return;

        stepBudget -= steps;
        RewindTo(Mathf.Max(0, history.Count - steps));
    }

    void EndRewind()
    {
        if (!rewinding) return;
        rewinding = false;
        sinceSample = SampleStep;
        Time.timeScale = 1f;
    }

    int LastAction()
    {
        for (int i = history.Count - 1; i > 0; i--)
        {
            if (history[i].action) return i;
        }
        return 0;
    }

    void RewindTo(int index)
    {
        index = Mathf.Clamp(index, 0, history.Count - 1);
        var snap = history[index];
        int keep = Mathf.Max(1, index);
        history.RemoveRange(keep, history.Count - keep);
        Restore(snap);
    }

    void Record()
    {
        bool action = history.Count == 0 || Inp.Down(ActionKeys);
        sinceSample += Time.deltaTime;
        if (!action && sinceSample < SampleStep) return;

        sinceSample = 0f;
        history.Add(Capture(action));
        if (history.Count > HistoryLimit) Thin();
    }

    void Thin()
    {
        for (int i = history.Count / 2; i > 0; i -= 2)
        {
            if (!history[i].action) history.RemoveAt(i);
        }
    }

    Omnitricks Tricks()
    {
        if (omni == null) omni = FindAnyObjectByType<Omnitricks>();
        return omni;
    }

    Snapshot Capture(bool action)
    {
        var tricks = Tricks();
        var snap = new Snapshot
        {
            action = action,
            level = levelIndex,
            levelDeaths = levelDeaths,
            winner = winner,
            levelTime = levelTime,
            runTime = runTime,
            newBest = newBest,
            revives = tricks != null ? tricks.Revives : 0,
            deaths = (int[])deaths.Clone(),
            scores = (int[])scores.Clone(),
            taken = new bool[pickups.Count],
            players = new PlayerController.State[players.Length],
            foes = new Enemy.State[enemies.Count],
            bodies = new Remains[corpses.Count]
        };

        for (int i = 0; i < pickups.Count; i++) snap.taken[i] = pickups[i].taken;
        for (int i = 0; i < players.Length; i++) snap.players[i] = players[i].Capture();
        for (int i = 0; i < enemies.Count; i++) snap.foes[i] = enemies[i].Capture();

        int n = 0;
        foreach (var pair in corpses) snap.bodies[n++] = Remember(pair.Key, pair.Value);
        return snap;
    }

    Remains Remember(Vector2Int cell, Corpse body)
    {
        var saved = new Remains
        {
            cell = cell,
            kind = body.kind,
            shade = body.shade,
            facing = body.facing,
            fuse = Tricks() != null ? Tricks().FuseLeft(body) : -1f
        };

        var mark = body.transform.Find("vis/mark");
        if (mark != null)
        {
            saved.marked = true;
            saved.mark = mark.GetComponent<SpriteRenderer>().color;
        }
        return saved;
    }

    void Restore(Snapshot snap)
    {
        if (snap.level != levelIndex) LoadLevel(snap.level);

        Time.timeScale = 0f;
        mode = Mode.Play;
        levelDeaths = snap.levelDeaths;
        winner = snap.winner;
        levelTime = snap.levelTime;
        runTime = snap.runTime;
        newBest = snap.newBest;
        deaths = (int[])snap.deaths.Clone();
        scores = (int[])snap.scores.Clone();
        toast = "";
        toastT = 0f;
        shake = 0f;

        RestoreBodies(snap.bodies);
        RestoreFoes(snap.foes);

        for (int i = 0; i < pickups.Count && i < snap.taken.Length; i++)
        {
            pickups[i].taken = snap.taken[i];
            pickups[i].go.SetActive(!snap.taken[i]);
        }

        for (int i = 0; i < players.Length; i++) players[i].Apply(snap.players[i]);
        foreach (var pair in spikeObjs) pair.Value.SetActive(!Covered(pair.Key));
        RestoreTricks(snap);
    }

    void RestoreBodies(Remains[] saved)
    {
        var wanted = new Dictionary<Vector2Int, Remains>();
        foreach (var r in saved) wanted[r.cell] = r;

        foreach (var cell in new List<Vector2Int>(corpses.Keys))
        {
            var body = corpses[cell];
            bool same = body != null
                && wanted.TryGetValue(cell, out var r)
                && r.kind == body.kind
                && r.shade == body.shade
                && r.facing == body.facing;
            if (same) continue;

            if (body != null) Destroy(body.gameObject);
            corpses.Remove(cell);
        }

        foreach (var r in saved)
        {
            if (!corpses.ContainsKey(r.cell)) corpses[r.cell] = Rebuild(r);
        }
    }

    Corpse Rebuild(Remains r)
    {
        string look = r.marked ? "normal" : r.kind;
        var body = Corpse.Create(levelRoot.transform, r.cell, 0f, r.shade, r.facing, look);
        body.kind = r.kind;

        if (r.marked)
        {
            var vis = body.transform.Find("vis");
            var size = new Vector2(0.5f, 0.5f);
            Gfx.Make("mark", vis, Gfx.Square, r.mark, new Vector2(0f, -0.05f), size, 9);
        }
        return body;
    }

    void RestoreFoes(Enemy.State[] saved)
    {
        bool same = enemies.Count == saved.Length;
        for (int i = 0; same && i < saved.Length; i++) same = enemies[i].kind == saved[i].kind;

        if (!same)
        {
            foreach (var e in enemies) Destroy(e.gameObject);
            enemies.Clear();
            foreach (var s in saved)
                enemies.Add(Enemy.Create(levelRoot.transform, s.kind, (int)s.pos.x, (int)s.pos.y));
        }

        for (int i = 0; i < saved.Length; i++) enemies[i].Apply(saved[i]);
    }

    void RestoreTricks(Snapshot snap)
    {
        var tricks = Tricks();
        if (tricks == null) return;

        tricks.Rewind(snap.revives, this);
        foreach (var r in snap.bodies)
        {
            if (r.fuse >= 0f) tricks.Relight(corpses[r.cell], r.fuse);
        }
    }

    void DrawUndo(float vw)
    {
        var gold = new Color(1f, 0.82f, 0.4f);
        if (rewinding)
        {
            string at = "rewinding   level " + (levelIndex + 1) + "   " + Records.Fmt(levelTime);
            Txt(0, 112, vw, at, 22, gold, TextAnchor.UpperCenter);
        }
        Txt(12, 402, 460, "U: undo    hold U: rewind    Shift+U: back to the start", 12, gold);
    }
}

public partial class PlayerController
{
    public struct State
    {
        public bool alive;
        public bool jumping;
        public bool grounded;
        public Vector2 pos;
        public Vector2 vel;
        public string power;
        public float respawnT;
        public float stun;
        public float portalCd;
        public float coyote;
        public int face;
    }

    public State Capture()
    {
        bool alive = gameObject.activeSelf;
        return new State
        {
            alive = alive,
            jumping = jumping,
            grounded = wasGrounded,
            pos = alive ? rb.position : (Vector2)transform.position,
            vel = alive ? rb.linearVelocity : Vector2.zero,
            power = power,
            respawnT = respawnT,
            stun = stun,
            portalCd = portalCd,
            coyote = coyote,
            face = face
        };
    }

    public void Apply(State s)
    {
        SetAlive(s.alive);
        rb.simulated = true;
        transform.position = s.pos;
        rb.position = s.pos;
        if (s.alive) rb.linearVelocity = s.vel;

        jumping = s.jumping;
        wasGrounded = s.grounded;
        power = s.power;
        respawnT = s.respawnT;
        stun = s.stun;
        portalCd = s.portalCd;
        coyote = s.coyote;
        face = s.face;
        buffer = 0f;
        squash = 0f;
        jumpPressed = false;
    }
}

public partial class Enemy
{
    public struct State
    {
        public char kind;
        public Vector2 pos;
        public Vector2 vel;
        public bool onFloor;
        public bool chasing;
        public float stuck;
        public float knockCd;
        public int breaks;
    }

    public State Capture()
    {
        return new State
        {
            kind = kind,
            pos = pos,
            vel = vel,
            onFloor = onFloor,
            chasing = chasing,
            stuck = stuck,
            knockCd = knockCd,
            breaks = breaks
        };
    }

    public void Apply(State s)
    {
        pos = s.pos;
        vel = s.vel;
        onFloor = s.onFloor;
        chasing = s.chasing;
        stuck = s.stuck;
        knockCd = s.knockCd;
        breaks = s.breaks;
        squash = 0f;
        Render();
    }
}

public partial class Corpse
{
    public int shade;
    public int facing;
}

public partial class Omnitricks
{
    public int Revives => reviveCharges;

    public float FuseLeft(Corpse body)
    {
        return fuse.TryGetValue(body, out float left) ? left : -1f;
    }

    public void Relight(Corpse body, float left)
    {
        fuse[body] = left;
    }

    public void Rewind(int revives, GameManager gm)
    {
        reviveCharges = revives;
        pendingRevive = false;
        lastLevel = gm.levelIndex;
        wasAlive.Clear();
        fuse.Clear();
        crumble.Clear();
        seenBody.Clear();
        foreach (var pair in gm.corpses) seenBody[pair.Key] = pair.Value;
    }
}
