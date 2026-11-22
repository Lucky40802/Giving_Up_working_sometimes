using UnityEngine;

public static class Gfx
{
    static Sprite square, circle, tri;

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var t = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px);
                t.Apply();
                square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int n = 64;
                var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(n / 2f - d);
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                t.Apply();
                circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
            return circle;
        }
    }

    public static Sprite Tri
    {
        get
        {
            if (tri == null)
            {
                const int n = 32;
                var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float half = (1f - y / (float)n) * n / 2f;
                        float a = Mathf.Clamp01(half - Mathf.Abs(x - n / 2f + 0.5f) + 0.5f);
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                t.Apply();
                tri = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), n);
            }
            return tri;
        }
    }

    public static SpriteRenderer Make(string name, Transform parent, Sprite s, Color c, Vector2 pos, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }
}

public class Bit : MonoBehaviour
{
    public Vector2 vel;
    public float life;
    float total;
    SpriteRenderer sr;

    void Start() { total = life; sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        vel.y -= 28f * Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);
        life -= Time.deltaTime;
        var c = sr.color;
        c.a = Mathf.Clamp01(life / Mathf.Max(0.01f, total * 0.6f));
        sr.color = c;
        if (life <= 0f) Destroy(gameObject);
    }

    public static void Burst(Vector2 pos, Color[] cols, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float s = Random.Range(0.3f, 1f) * speed;
            float size = Random.Range(0.06f, 0.18f);
            var sr = Gfx.Make("bit", null, Gfx.Square, cols[Random.Range(0, cols.Length)], pos, new Vector2(size, size), 30);
            var b = sr.gameObject.AddComponent<Bit>();
            b.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.4f) * s;
            b.life = Random.Range(0.4f, 0.9f);
        }
    }
}
