using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DeathHeatmap : MonoBehaviour
{
    const int Cols = Levels.COLS;
    const int Rows = Levels.ROWS;
    const float Cell = 14f;

    readonly int[,] heat = new int[Cols, Rows];
    readonly HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

    int lastLevel = -1;
    int peak;
    Vector2Int hottest;
    bool visible;
    Texture2D pixel;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("DeathHeatmap").AddComponent<DeathHeatmap>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            visible = !visible;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            System.Array.Clear(heat, 0, heat.Length);
            seen.Clear();
            peak = 0;
        }

        seen.RemoveWhere(c => !gm.corpses.ContainsKey(c));
        foreach (var cell in gm.corpses.Keys)
        {
            if (!seen.Add(cell)) continue;
            if (cell.x < 0 || cell.x >= Cols || cell.y < 0 || cell.y >= Rows) continue;
            heat[cell.x, cell.y]++;
            if (heat[cell.x, cell.y] > peak)
            {
                peak = heat[cell.x, cell.y];
                hottest = cell;
            }
        }
    }

    Color HeatColor(int count)
    {
        if (count == 0) return new Color(1f, 1f, 1f, 0.08f);
        float t = peak <= 1 ? 1f : (float)count / peak;
        return Color.Lerp(new Color(1f, 0.85f, 0.2f, 0.85f), new Color(0.9f, 0.1f, 0.1f, 0.95f), t);
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 13;
            style.normal.textColor = Color.white;
        }

        float w = Cols * Cell;
        float h = Rows * Cell;
        float x0 = 12f;
        float y0 = Screen.height - h - 40f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x0 - 6f, y0 - 26f, w + 12f, h + 56f), pixel);

        GUI.color = Color.white;
        GUI.Label(new Rect(x0, y0 - 22f, w, 20f), "DEATH HEATMAP  (F3)", style);

        for (int cx = 0; cx < Cols; cx++)
        {
            for (int cy = 0; cy < Rows; cy++)
            {
                GUI.color = HeatColor(heat[cx, cy]);
                GUI.DrawTexture(new Rect(x0 + cx * Cell, y0 + cy * Cell, Cell - 1f, Cell - 1f), pixel);
            }
        }

        GUI.color = Color.white;
        string info = peak == 0
            ? "no deaths on this level yet"
            : "hottest: ("+ hottest.x + ","+ hottest.y + ")  x"+ peak;
        GUI.Label(new Rect(x0, y0 + h + 4f, w, 20f), info, style);
    }
}