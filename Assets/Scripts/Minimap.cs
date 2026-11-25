using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Minimap : MonoBehaviour
{
    const float Cell = 9f;
    const float Pad = 10f;
    const float Header = 26f;
    const float Footer = 44f;
    const float Margin = 16f;

    readonly Dictionary<string, int> kindCounts = new Dictionary<string, int>();
    readonly List<string> kindOrder = new List<string>();

    List<LevelData> levels;
    bool visible = true;
    bool compact;
    int lastCount = -1;
    int lastLevel = -1;
    float blink;
    int peakBodies;

    Texture2D pixel;
    GUIStyle titleStyle;
    GUIStyle smallStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Minimap").AddComponent<Minimap>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        levels = Levels.Get();
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.xKey.wasPressedThisFrame) visible = !visible;
            if (kb.nKey.wasPressedThisFrame) compact = !compact;
        }

        blink += Time.unscaledDeltaTime * 6f;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            peakBodies = 0;
            lastCount = -1;
        }

        if (gm.corpses.Count != lastCount)
        {
            lastCount = gm.corpses.Count;
            CountKinds(gm);
        }

        peakBodies = Mathf.Max(peakBodies, gm.corpses.Count);
    }

    void CountKinds(GameManager gm)
    {
        kindCounts.Clear();
        kindOrder.Clear();

        foreach (var body in gm.corpses.Values)
        {
            if (body == null) continue;
            if (!kindCounts.ContainsKey(body.kind))
            {
                kindCounts[body.kind] = 0;
                kindOrder.Add(body.kind);
            }
            kindCounts[body.kind]++;
        }

        kindOrder.Sort();
    }

    Color KindColor(string kind)
    {
        if (kind == "pad") return new Color(0.4f, 0.9f, 0.5f);
        if (kind == "portal") return new Color(0.7f, 0.45f, 1f);
        return new Color(0.85f, 0.85f, 0.9f);
    }

    Color TileColor(char tile)
    {
        if (tile == '#') return new Color(0.35f, 0.38f, 0.5f);
        if (tile == '^') return new Color(0.9f, 0.3f, 0.3f);
        if (tile == 'G') return new Color(1f, 0.85f, 0.3f);
        if (tile == 'P') return new Color(0.4f, 0.7f, 1f);
        if (tile == 'j') return new Color(0.2f, 0.75f, 0.6f);
        if (tile == 't') return new Color(0.55f, 0.25f, 0.85f);
        if (tile == 'O') return new Color(0.3f, 0.45f, 1f);
        return new Color(0f, 0f, 0f, 0f);
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, pixel);
        GUI.color = Color.white;
    }

    void MakeStyles()
    {
        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 11;
        smallStyle.alignment = TextAnchor.MiddleLeft;
        smallStyle.normal.textColor = new Color(0.85f, 0.87f, 0.95f);

        titleStyle = new GUIStyle(smallStyle);
        titleStyle.fontSize = 13;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
    }

    char TileAt(int col, int row)
    {
        if (levels == null || lastLevel < 0 || lastLevel >= levels.Count) return ' ';

        var map = levels[lastLevel].map;
        if (row < 0 || row >= map.Length) return ' ';
        if (col < 0 || col >= map[row].Length) return ' ';
        return map[row][col];
    }

    int Neighbours(GameManager gm, Vector2Int cell)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (gm.corpses.ContainsKey(new Vector2Int(cell.x + dx, cell.y + dy))) count++;
            }
        }
        return count;
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null || !visible) return;
        if (gm.mode == GameManager.Mode.Menu || gm.mode == GameManager.Mode.End) return;

        if (titleStyle == null) MakeStyles();

        float mapW = Levels.COLS * Cell;
        float mapH = Levels.ROWS * Cell;
        float width = mapW + Pad * 2f;
        float height = compact ? Header + 6f : Header + mapH + Pad + Footer;

        var panel = new Rect(Screen.width - width - Margin, Screen.height * 0.2f, width, height);
        Fill(panel, new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(panel.x, panel.y, panel.width, 2f), new Color(0.4f, 0.7f, 1f));

        string name = levels != null && lastLevel >= 0 && lastLevel < levels.Count ? levels[lastLevel].name : "level";
        GUI.Label(new Rect(panel.x + Pad, panel.y + 4f, panel.width - Pad * 2f, 20f), "map  " + name, titleStyle);

        if (compact) return;

        var origin = new Vector2(panel.x + Pad, panel.y + Header);
        Fill(new Rect(origin.x, origin.y, mapW, mapH), new Color(0.04f, 0.04f, 0.08f, 0.9f));

        DrawTiles(origin);
        DrawBodies(gm, origin);
        DrawEnemies(gm, origin);
        DrawPlayers(origin);
        DrawFooter(gm, panel);
    }

    void DrawTiles(Vector2 origin)
    {
        for (int row = 0; row < Levels.ROWS; row++)
        {
            for (int col = 0; col < Levels.COLS; col++)
            {
                char tile = TileAt(col, row);
                Color c = TileColor(tile);
                if (c.a <= 0f) continue;
                Fill(new Rect(origin.x + col * Cell, origin.y + row * Cell, Cell - 1f, Cell - 1f), c);
            }
        }
    }

    void DrawBodies(GameManager gm, Vector2 origin)
    {
        foreach (var pair in gm.corpses)
        {
            var cell = pair.Key;
            if (cell.x < 0 || cell.x >= Levels.COLS || cell.y < 0 || cell.y >= Levels.ROWS) continue;

            string kind = pair.Value != null ? pair.Value.kind : "normal";
            Color c = KindColor(kind);
            float crowd = Mathf.Clamp01(Neighbours(gm, cell) / 8f);
            c = Color.Lerp(c, new Color(1f, 0.55f, 0.2f), crowd * 0.6f);
            c.a = 0.95f;

            Fill(new Rect(origin.x + cell.x * Cell, origin.y + cell.y * Cell, Cell - 1f, Cell - 1f), c);
        }
    }

    void DrawEnemies(GameManager gm, Vector2 origin)
    {
        Color c = new Color(1f, 0.35f, 0.55f);
        foreach (var enemy in gm.enemies)
        {
            if (enemy == null) continue;

            float x = origin.x + enemy.pos.x * Cell - Cell * 0.5f;
            float y = origin.y + enemy.pos.y * Cell - Cell * 0.5f;
            Fill(new Rect(x + 1f, y + 1f, Cell - 3f, Cell - 3f), c);
        }
    }

    void DrawPlayers(Vector2 origin)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        bool on = Mathf.Sin(blink) > -0.3f;

        foreach (var p in players)
        {
            if (p == null) continue;

            var g = p.GridPos;
            float x = origin.x + g.x * Cell - Cell * 0.5f;
            float y = origin.y + g.y * Cell - Cell * 0.5f;
            Color c = p.id == 0 ? Color.white : p.bodyColor;
            if (!on) c.a = 0.4f;

            Fill(new Rect(x - 1f, y - 1f, Cell + 1f, Cell + 1f), new Color(0f, 0f, 0f, 0.8f));
            Fill(new Rect(x, y, Cell - 1f, Cell - 1f), c);
        }
    }

    void DrawFooter(GameManager gm, Rect panel)
    {
        float y = panel.yMax - Footer + 2f;

        string line = "bodies " + gm.corpses.Count + "   peak " + peakBodies + "   enemies " + gm.enemies.Count;
        GUI.Label(new Rect(panel.x + Pad, y, panel.width - Pad * 2f, 16f), line, smallStyle);

        float x = panel.x + Pad;
        foreach (string kind in kindOrder)
        {
            Fill(new Rect(x, y + 22f, 8f, 8f), KindColor(kind));
            string text = kind + " " + kindCounts[kind];
            GUI.Label(new Rect(x + 12f, y + 17f, 80f, 18f), text, smallStyle);
            x += 84f;
        }

        if (kindOrder.Count == 0)
            GUI.Label(new Rect(panel.x + Pad, y + 17f, panel.width - Pad * 2f, 18f), "X hides   N shrinks", smallStyle);
    }
}