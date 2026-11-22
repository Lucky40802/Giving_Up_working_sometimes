using UnityEngine;

public class Corpse : MonoBehaviour
{
    public string kind = "normal";
    public Vector2Int cell;
    Transform vis;
    float drop, vy, squish, t;

    static readonly Color[] Shades = { new Color(0.54f, 0.35f, 0.42f), new Color(0.48f, 0.31f, 0.49f), new Color(0.42f, 0.35f, 0.54f) };

    public static Corpse Create(Transform parent, Vector2Int cell, float dropTiles, int n, int face, string kind)
    {
        var go = new GameObject("Corpse");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(cell.x + 0.5f, Levels.ROWS - cell.y - 0.5f, 0f);
        var c = go.AddComponent<Corpse>();
        c.cell = cell;
        c.kind = kind;
        c.drop = dropTiles;
        if (kind != "portal")
        {
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
        }
        c.Build(n, face);
        return c;
    }

    void Build(int n, int face)
    {
        var v = new GameObject("vis");
        v.transform.SetParent(transform, false);
        vis = v.transform;
        vis.localPosition = new Vector3(0, drop, 0);
        if (kind == "portal")
        {
            Gfx.Make("glow", vis, Gfx.Circle, new Color(0.5f, 0.2f, 0.9f, 0.3f), Vector2.zero, new Vector2(0.9f, 0.9f), 6);
            Gfx.Make("core", vis, Gfx.Circle, Gfx.Hex("c77dff"), Vector2.zero, new Vector2(0.55f, 0.55f), 7);
            Gfx.Make("dot", vis, Gfx.Circle, Color.white, Vector2.zero, new Vector2(0.18f, 0.18f), 8);
            return;
        }
        Color col = kind == "pad" ? Gfx.Hex("3fae8a") : Shades[n % 3];
        Gfx.Make("edge", vis, Gfx.Square, col * 0.45f, Vector2.zero, Vector2.one, 5);
        Gfx.Make("face", vis, Gfx.Square, col, Vector2.zero, new Vector2(0.94f, 0.94f), 6);
        Gfx.Make("top", vis, Gfx.Square, Color.Lerp(col, Color.white, 0.25f), new Vector2(0, 0.44f), new Vector2(0.94f, 0.09f), 7);
        var ink = Gfx.Hex("1b1020");
        foreach (float ex in new[] { -0.2f, 0.2f })
        {
            var a = Gfx.Make("x1", vis, Gfx.Square, ink, new Vector2(ex + face * 0.03f, 0.1f), new Vector2(0.22f, 0.06f), 8);
            a.transform.localRotation = Quaternion.Euler(0, 0, 45);
            var b = Gfx.Make("x2", vis, Gfx.Square, ink, new Vector2(ex + face * 0.03f, 0.1f), new Vector2(0.22f, 0.06f), 8);
            b.transform.localRotation = Quaternion.Euler(0, 0, -45);
        }
        Gfx.Make("mouth", vis, Gfx.Square, ink, new Vector2(0, -0.25f), new Vector2(0.38f, 0.06f), 8);
        if (kind == "pad")
            Gfx.Make("spring", vis, Gfx.Square, Gfx.Hex("eafff6"), new Vector2(0, 0.3f), new Vector2(0.7f, 0.08f), 8);
    }

    public void Kick() { squish = 1f; }

    void Update()
    {
        t += Time.deltaTime;
        if (drop > 0f)
        {
            vy += 59.4f * Time.deltaTime;
            drop -= vy * Time.deltaTime;
            if (drop <= 0f)
            {
                drop = 0f;
                GameManager.I.OnCorpseLanded(transform.position + Vector3.down * 0.5f, vy > 8f);
                vy = 0f;
            }
        }
        squish = Mathf.MoveTowards(squish, 0f, Time.deltaTime * 5f);
        if (kind == "portal") vis.localRotation = Quaternion.Euler(0, 0, t * 120f);
        vis.localPosition = new Vector3(0, drop + (kind == "pad" ? squish * 0.12f : 0f), 0);
    }
}
