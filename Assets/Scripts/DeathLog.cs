using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DeathLog : MonoBehaviour
{
    const int MaxLines = 8;
    const string TotalKey = "log_total";

    readonly List<string> lines = new List<string>();
    readonly HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
    readonly int[] perLevel = new int[32];

    float levelClock;
    int lastLevel = -1;
    int lifetime;
    bool visible;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("DeathLog").AddComponent<DeathLog>();
    }

    void Awake()
    {
        lifetime = PlayerPrefs.GetInt(TotalKey, 0);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            visible = !visible;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            levelClock = 0f;
            seen.Clear();
            foreach (var cell in gm.corpses.Keys) seen.Add(cell);
        }

        if (gm.mode == GameManager.Mode.Play) levelClock += Time.deltaTime;

        seen.RemoveWhere(c => !gm.corpses.ContainsKey(c));
        foreach (var pair in gm.corpses)
        {
            if (seen.Add(pair.Key)) Record(gm, pair.Key, pair.Value);
        }
    }

    void Record(GameManager gm, Vector2Int cell, Corpse body)
    {
        perLevel[gm.levelIndex]++;
        lifetime++;
        PlayerPrefs.SetInt(TotalKey, lifetime);

        string kind = string.IsNullOrEmpty(body.kind) ? "body": body.kind;
        string line = "L" + (gm.levelIndex + 1) + "  (" + cell.x + "," + cell.y + ")  "
            + kind + "  " + Records.Fmt(levelClock);
        lines.Insert(0, line);
        if (lines.Count > MaxLines) lines.RemoveAt(lines.Count - 1);
    }

    string WorstLevel()
    {
        int best = 0;
        for (int i = 1; i < perLevel.Length; i++)
            if (perLevel[i] > perLevel[best]) best = i;
        if (perLevel[best] == 0) return "none yet";
        return "L"+ (best + 1) + "("+ perLevel[best] + ")";
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = Color.white;
        }

        float w = 300f;
        float h = 80f + lines.Count * 20f;
        var box = new Rect(Screen.width - w - 272f, Screen.height * 0.2f, w, h);
        GUI.Box(box, "");

        GUI.Label(new Rect(box.x + 10f, box.y + 6f, w - 20f, 22f), "DEATH LOG  (F2)", style);
        for (int i = 0; i < lines.Count; i++)
            GUI.Label(new Rect(box.x + 10f, box.y + 30f + i * 20f, w - 20f, 20f), lines[i], style);

        float y = box.y + 34f + lines.Count * 20f;
        GUI.Label(new Rect(box.x + 10f, y, w - 20f, 20f), "Deadliest: "+ WorstLevel(), style);
        GUI.Label(new Rect(box.x + 10f, y + 20f, w - 20f, 20f), "Lifetime deaths: "+ lifetime, style);
    }
}