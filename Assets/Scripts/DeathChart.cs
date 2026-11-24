using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using UnityEngine.InputSystem;

public class DeathChart : MonoBehaviour
{
    const int Levels15 = 15;
    const float BarW = 22f;
    const float BarGap = 6f;
    const float ChartH = 140f;

    readonly int[] deaths = new int[Levels15];
    readonly HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

    int lastLevel = -1;
    int hovered = -1;
    bool visible;
    Texture2D pixel;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("DeathChart").AddComponent<DeathChart>();
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

        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            visible = !visible;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            seen.Clear();
            foreach (var cell in gm.corpses.Keys) seen.Add(cell);
        }

        seen.RemoveWhere(c => !gm.corpses.ContainsKey(c));
        foreach (var cell in gm.corpses.Keys)
        {
            if (seen.Add(cell) && gm.levelIndex < Levels15) deaths[gm.levelIndex]++;
        }
    }

    int MaxDeaths()
    {
        int max = 1;
        foreach (int d in deaths) max = Mathf.Max(max, d);
        return max;
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
        }

        var gm = GameManager.I;
        float w = Levels15 * (BarW + BarGap) + BarGap;
        float x0 = (Screen.width - w) / 2f;
        float y0 = Screen.height - ChartH - 70f;
        int max = MaxDeaths();

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(x0 - 10f, y0 - 34f, w + 20f, ChartH + 76f), pixel);

        GUI.color = Color.white;
        GUI.Label(new Rect(x0, y0 - 30f, w, 22f), "DEATHS PER LEVEL  (F5)", style);

        hovered = -1;
        Vector2 mouse = Event.current.mousePosition;

        for (int i = 0; i < Levels15; i++)
        {
            float h = ChartH * deaths[i] / max;
            var bar = new Rect(x0 + BarGap + i * (BarW + BarGap), y0 + ChartH - h, BarW, Mathf.Max(h, 2f));

            if (bar.Contains(mouse)) hovered = i;

            bool current = gm != null && gm.levelIndex == i;
            GUI.color = current ? new Color(1f, 0.85f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
            GUI.DrawTexture(bar, pixel);

            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x - 4f, y0 + ChartH + 2f, BarW + 8f, 18f), (i + 1).ToString(), style);
        }

        string info = hovered >= 0
            ? "Level "+ (hovered + 1) + ": "+ deaths[hovered] + "deaths"
            : "Total: " + Total() + " deaths";
        GUI.Label(new Rect(x0, y0 + ChartH + 22f, w, 20f), info, style);
    }

    int Total()
    {
        int sum = 0;
        foreach (int d in deaths) sum += d;
        return sum;
    }
}