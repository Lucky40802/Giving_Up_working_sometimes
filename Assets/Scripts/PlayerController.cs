using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController : MonoBehaviour
{
    public const float W = 22f / 32f;
    public const float H = 28f / 32f;
    const float SPEED = 7.5f;
    const float JUMP = 17.5f;
    const float GRAV = 59.4f;
    const float MAXFALL = 28f;
    const float COYOTE = 0.09f;
    const float BUFFER = 0.12f;

    public int id;
    public bool two;
    public bool active;
    public string power = "";
    public float stun;
    public float portalCd;
    public float respawnT;
    public int face = 1;
    public System.Action Jumped;
    public System.Action<bool> Landed;

    public Rigidbody2D rb;
    CapsuleCollider2D col;
    Transform vis;
    SpriteRenderer body;
    SpriteRenderer ring;
    float coyote, buffer, squash, t;
    bool jumpPressed, jumping, wasGrounded;
    public Color bodyColor;

    public static PlayerController Create(int id)
    {
        var go = new GameObject("Player" + (id + 1));
        var p = go.AddComponent<PlayerController>();
        p.id = id;
        p.bodyColor = id == 0 ? Gfx.Hex("ffd166") : Gfx.Hex("5fd3f3");
        p.Build();
        return p;
    }

    void Build()
    {
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = GRAV / 9.81f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        col = gameObject.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(W, H);
        col.direction = CapsuleDirection2D.Vertical;
        col.sharedMaterial = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };

        var v = new GameObject("vis");
        v.transform.SetParent(transform, false);
        vis = v.transform;
        Gfx.Make("outline", vis, Gfx.Square, Gfx.Hex("3a2a10"), Vector2.zero, new Vector2(W + 0.09f, H + 0.09f), 10);
        body = Gfx.Make("body", vis, Gfx.Square, bodyColor, Vector2.zero, new Vector2(W, H), 11);
        Gfx.Make("shine", vis, Gfx.Square, Color.Lerp(bodyColor, Color.white, 0.6f), new Vector2(0, H / 2f - 0.05f), new Vector2(W, 0.09f), 12);
        for (int i = 0; i < 2; i++)
        {
            float ex = i == 0 ? -0.14f : 0.14f;
            Gfx.Make("eye", vis, Gfx.Square, Color.white, new Vector2(ex, 0.12f), new Vector2(0.15f, 0.19f), 12);
            Gfx.Make("pupil", vis, Gfx.Square, Gfx.Hex("1b1020"), new Vector2(ex, 0.1f), new Vector2(0.09f, 0.09f), 13);
        }
        ring = Gfx.Make("ring", vis, Gfx.Circle, Color.clear, Vector2.zero, new Vector2(1.3f, 1.3f), 9);
    }

    public Vector2 GridPos => new Vector2(transform.position.x, Levels.ROWS - transform.position.y);

    public void SetAlive(bool v)
    {
        gameObject.SetActive(v);
        if (v) rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        if (Inp.Down(JumpKeys())) jumpPressed = true;
        t += Time.deltaTime;
        var tint = power == "pad" ? Gfx.Hex("7ef0c8") : Gfx.Hex("c77dff");
        ring.color = power == "" ? Color.clear : new Color(tint.r, tint.g, tint.b, 0.35f + 0.15f * Mathf.Sin(t * 8f));
        float sw = 1f - squash, sh = 1f + squash;
        vis.localScale = new Vector3(sw, sh, 1f);
        vis.localPosition = new Vector3(0f, (sh - 1f) * H / 2f, 0f);
    }

    Key[] JumpKeys()
    {
        if (id == 1) return new[] { Key.UpArrow };
        return two ? new[] { Key.W, Key.Space } : new[] { Key.W, Key.Space, Key.UpArrow };
    }

    Key[] LeftKeys() => id == 1 ? new[] { Key.LeftArrow } : (two ? new[] { Key.A } : new[] { Key.A, Key.LeftArrow });
    Key[] RightKeys() => id == 1 ? new[] { Key.RightArrow } : (two ? new[] { Key.D } : new[] { Key.D, Key.RightArrow });

    public bool DiePressed() => id == 1 ? Inp.Down(Key.Enter, Key.NumpadEnter) : Inp.Down(Key.R);

    bool Grounded()
    {
        var hits = Physics2D.OverlapBoxAll(new Vector2(rb.position.x, rb.position.y - H / 2f - 0.04f), new Vector2(W * 0.8f, 0.08f), 0f);
        foreach (var h in hits)
            if (h != col && !h.isTrigger) return true;
        return false;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        portalCd -= dt;
        stun -= dt;
        float dir = (Inp.Held(RightKeys()) ? 1f : 0f) - (Inp.Held(LeftKeys()) ? 1f : 0f);
        if (dir != 0f) face = dir > 0f ? 1 : -1;
        bool grounded = Grounded();
        var v = rb.linearVelocity;

        if (stun <= 0f)
        {
            float accel = dir != 0f ? (grounded ? 93.75f : 62.5f) : (grounded ? 106f : 37.5f);
            v.x = Mathf.MoveTowards(v.x, dir * SPEED, accel * dt);
        }

        coyote -= dt;
        buffer -= dt;
        if (grounded) coyote = COYOTE;
        if (jumpPressed) { buffer = BUFFER; jumpPressed = false; }

        if (buffer > 0f && coyote > 0f)
        {
            v.y = JUMP;
            buffer = 0f;
            coyote = 0f;
            jumping = true;
            squash = -0.25f;
            Jumped?.Invoke();
        }

        if (jumping && !Inp.Held(JumpKeys()) && v.y > 0f)
        {
            v.y *= 0.45f;
            jumping = false;
        }
        if (v.y <= 0f) jumping = false;

        float fall = -v.y;
        if (v.y < -MAXFALL) v.y = -MAXFALL;
        rb.linearVelocity = v;

        if (grounded && !wasGrounded && fall > 6f)
        {
            squash = 0.25f;
            Landed?.Invoke(fall > 15f);
        }
        wasGrounded = grounded;
        squash = Mathf.MoveTowards(squash, 0f, dt * 2f);
    }
}
