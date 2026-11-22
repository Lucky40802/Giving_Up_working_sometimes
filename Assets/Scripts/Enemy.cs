using UnityEngine;

// Enemies live in grid space (x right, y DOWN, 1 unit = 1 tile) so they can ask GameManager.Occupied(col,row).
public class Enemy : MonoBehaviour
{
    const float R = 10f / 32f;
    const float GRAVITY = 43.75f;
    const float CHASE_RANGE = 5.3f;
    const float CHASE_SPEED = 2.97f;
    const int MAX_BREAKS = 3;
    const float KNOCK_FORCE = 27f;

    public char kind;
    public Vector2 pos;
    Vector2 vel;
    bool onFloor, chasing;
    float squash, stuck, knockCd, t;
    int breaks;
    Vector2 look = Vector2.right;
    SpriteRenderer body, ring;
    Transform pupilL, pupilR, vis;

    public static Enemy Create(Transform parent, char kind, int c, int r)
    {
        var go = new GameObject("Enemy" + kind);
        go.transform.SetParent(parent, false);
        var e = go.AddComponent<Enemy>();
        e.kind = kind;
        e.pos = new Vector2(c + 0.5f, r + 0.5f);
        float sx = Random.value < 0.5f ? 1f : -1f, sy = Random.value < 0.5f ? 1f : -1f;
        switch (kind)
        {
            case 'E': e.vel = new Vector2(3.44f * sx, 2.5f * sy); break;
            case 'B': e.vel = new Vector2(3.1f * sx, 2.2f * sy); break;
            case 'K': e.vel = new Vector2(2.8f * sx, 1.9f * sy); break;
            case 'W': e.vel = new Vector2(2.2f * sx, 0f); break;
        }
        e.Build();
        return e;
    }

    void Build()
    {
        var v = new GameObject("vis");
        v.transform.SetParent(transform, false);
        vis = v.transform;
        ring = Gfx.Make("ring", vis, Gfx.Circle, Color.clear, Vector2.zero, new Vector2(R * 3.2f, R * 3.2f), 18);
        body = Gfx.Make("body", vis, Gfx.Circle, Col(), Vector2.zero, new Vector2(R * 2f, R * 2f), 20);
        Gfx.Make("core", vis, Gfx.Circle, Col() * 0.6f, Vector2.zero, new Vector2(R * 1.4f, R * 1.4f), 21);
        foreach (float ex in new[] { -0.11f, 0.11f })
        {
            Gfx.Make("eye", vis, Gfx.Circle, Color.white, new Vector2(ex, 0.06f), new Vector2(0.15f, 0.15f), 22);
            var p = Gfx.Make("pupil", vis, Gfx.Circle, Gfx.Hex("1b1020"), new Vector2(ex, 0.06f), new Vector2(0.07f, 0.07f), 23);
            if (ex < 0) pupilL = p.transform; else pupilR = p.transform;
        }
        if (kind == 'W')
            foreach (float sx in new[] { -0.19f, 0f, 0.19f })
            {
                var s = Gfx.Make("spike", vis, Gfx.Tri, Col(), new Vector2(sx, R - 0.04f), new Vector2(0.12f, 0.17f), 19);
                s.transform.localPosition = new Vector3(sx, R - 0.08f, 0);
            }
    }

    Color Col()
    {
        switch (kind)
        {
            case 'W': return Gfx.Hex("ff9f43");
            case 'C': return chasing ? Gfx.Hex("c77dff") : Gfx.Hex("9b5de5");
            case 'B': return Gfx.Hex("2ec4b6");
            case 'K': return Gfx.Hex("4895ef");
        }
        return Gfx.Hex("ff5d73");
    }

    public Rect HurtRect()
    {
        float h = R - 2f / 32f;
        return new Rect(pos.x - h, pos.y - h, h * 2f, h * 2f);
    }

    bool Blocked(Vector2 p)
    {
        foreach (float dx in new[] { -R, R })
            foreach (float dy in new[] { -R, R })
            {
                if (p.y + dy < 0f) return true;
                if (GameManager.I.Occupied(Mathf.FloorToInt(p.x + dx), Mathf.FloorToInt(p.y + dy))) return true;
            }
        return false;
    }

    bool StepX(float dx)
    {
        var np = pos + new Vector2(dx, 0f);
        if (Blocked(np) && !Blocked(pos)) { if (kind == 'B') BreakBodies(np); return false; }
        pos = np;
        return true;
    }

    bool StepY(float dy)
    {
        var np = pos + new Vector2(0f, dy);
        if (Blocked(np) && !Blocked(pos)) { if (kind == 'B') BreakBodies(np); return false; }
        pos = np;
        return true;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        knockCd -= dt;
        squash = Mathf.MoveTowards(squash, 0f, dt * 3f);
        var target = GameManager.I.NearestPlayer(pos);
        if (target != null) look = (target.GridPos - pos).normalized;

        switch (kind)
        {
            case 'E': case 'B': case 'K': Bounce(dt); break;
            case 'W': Walk(dt); break;
            case 'C': Chase(dt, target); break;
        }

        if (Blocked(pos))
        {
            stuck += dt;
            if (stuck > 0.25f) { Pop(); return; }
        }
        else stuck = 0f;
        Render();
    }

    void Bounce(float dt)
    {
        if (!StepX(vel.x * dt)) { vel.x = -vel.x; squash = 1f; }
        if (!StepY(vel.y * dt)) { vel.y = -vel.y; squash = 1f; }
    }

    void Walk(float dt)
    {
        vel.y += GRAVITY * dt;
        if (StepY(vel.y * dt)) onFloor = false;
        else { onFloor = vel.y > 0f; vel.y = 0f; }
        if (!onFloor) return;
        float dir = Mathf.Sign(vel.x);
        var ahead = pos + new Vector2(dir * (R + 0.125f), R + 0.19f);
        bool ledge = !GameManager.I.Occupied(Mathf.FloorToInt(ahead.x), Mathf.FloorToInt(ahead.y));
        if (ledge || !StepX(vel.x * dt)) { vel.x = -vel.x; squash = 1f; }
    }

    void Chase(float dt, PlayerController target)
    {
        chasing = false;
        if (target == null) return;
        var to = target.GridPos - pos;
        chasing = to.magnitude < CHASE_RANGE;
        if (chasing)
        {
            var d = to.normalized;
            StepX(d.x * CHASE_SPEED * dt);
            StepY(d.y * CHASE_SPEED * dt);
        }
    }

    void BreakBodies(Vector2 p)
    {
        foreach (float dx in new[] { -R - 0.03f, R + 0.03f })
            foreach (float dy in new[] { -R - 0.03f, R + 0.03f })
            {
                var cell = new Vector2Int(Mathf.FloorToInt(p.x + dx), Mathf.FloorToInt(p.y + dy));
                if (GameManager.I.corpses.ContainsKey(cell))
                {
                    GameManager.I.BreakBody(cell);
                    breaks++;
                    squash = 1f;
                }
            }
        if (breaks >= MAX_BREAKS) Pop();
    }

    public void Knock(PlayerController p)
    {
        if (knockCd > 0f) return;
        knockCd = 0.4f;
        var dir = (p.GridPos - pos).normalized;
        if (dir.y > -0.35f) dir.y = -0.35f;
        dir.Normalize();
        p.rb.linearVelocity = new Vector2(dir.x, -dir.y) * KNOCK_FORCE;
        p.stun = 0.35f;
        squash = 1f;
        GameManager.I.shake = 0.35f;
        GameManager.I.sfx.Play("knock");
    }

    void Pop()
    {
        Bit.Burst(World(), new[] { Col(), Color.white }, 16, 6f);
        GameManager.I.sfx.Play("pop");
        GameManager.I.enemies.Remove(this);
        Destroy(gameObject);
    }

    Vector2 World() => new Vector2(pos.x, Levels.ROWS - pos.y);

    void Render()
    {
        transform.position = World();
        float bob = kind == 'C' && !chasing ? Mathf.Sin(t * 3f) * 0.08f : 0f;
        vis.localPosition = new Vector3(0, bob, 0);
        vis.localScale = new Vector3(1f + squash * 0.3f, 1f - squash * 0.3f, 1f);
        body.color = Col();
        var l = new Vector2(look.x, -look.y) * 0.04f;
        pupilL.localPosition = new Vector3(-0.11f + l.x, 0.06f + l.y, 0);
        pupilR.localPosition = new Vector3(0.11f + l.x, 0.06f + l.y, 0);
        if (kind == 'K')
        {
            var c = Col();
            ring.color = new Color(c.r, c.g, c.b, 0.25f + 0.2f * Mathf.Sin(t * 8f));
        }
    }
}
