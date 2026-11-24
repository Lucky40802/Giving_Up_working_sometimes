using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplitTimer : MonoBehaviour
{
    const int MaxSplits = 6;

    readonly List<string> splits = new List<string>();

    int lastLevel = -1;
    GameManager.Mode lastMode;
    float clock;
    float best;
    int personalBests;
    bool visible = true;
    Texture2D pixel;
    GUIStyle big;
    GUIStyle small;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("SplitTimer").AddComponent<SplitTimer>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    bool HasBest()
    {
        return best > 0f && best < 99999f;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            visible = !visible;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            clock = 0f;
            best = Records.Best(gm.levelIndex);
        }

        bool freshRun = gm.mode == GameManager.Mode.Play
            && lastMode != GameManager.Mode.Play
            && lastMode != GameManager.Mode.Pause;
        if (freshRun)
        {
            clock = 0f;
            best = Records.Best(gm.levelIndex);
        }

        if (gm.mode == GameManager.Mode.Play) clock += Time.deltaTime;

        if (lastMode == GameManager.Mode.Play && gm.mode == GameManager.Mode.Clear)
            AddSplit(gm.levelIndex);

        lastMode = gm.mode;
    }

    void AddSplit(int level)
    {
        string delta = "first clear";
        if (HasBest())
        {
            float diff = clock - best;
            delta = (diff < 0f ? "-": "+") + Mathf.Abs(diff).ToString("0.00") + "s";
            if (diff < 0f) personalBests++;
        }

        splits.Insert(0, "l"+ (level + 1) + "  "+ Records.Fmt(clock) + "  "+ delta);
        if (splits.Count > MaxSplits) splits.RemoveAt(splits.Count - 1);
    }

    Color ClockColor()
    {
        if (!HasBest()) return Color.white;
        if (clock <= best) return new Color(0.5f, 1f, 0.6f);
        return new Color(1f, 0.45f, 0.45f);
    }

    void MakeStyles()
    {
        small = new GUIStyle(GUI.skin.label);
        small.fontSize = 13;
        small.alignment = TextAnchor.MiddleCenter;
        small.normal.textColor = Color.white;

        big = new GUIStyle(small);
        big.fontSize = 30;
        big.fontStyle = FontStyle.Bold;
    }

    void OnGUI()
    {
        if (!visible || GameManager.I == null) return;
        if (big == null) MakeStyles();

        float w = 220f;
        float h = 78f + splits.Count * 18f;
        var box = new Rect((Screen.width - w) / 2f, 8f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(box, pixel);
        GUI.color = Color.white;

        big.normal.textColor = ClockColor();
        GUI.Label(new Rect(box.x, box.y + 4f, w, 38f), Records.Fmt(clock), big);

        string bestText = HasBest() ? "best "+ Records.Fmt(best) : "no best yet";
        GUI.Label(new Rect(box.x, box.y + 42f, w, 18f), bestText + "  pbs "+ personalBests, small);

        for (int i = 0; i < splits.Count; i++)
            GUI.Label(new Rect(box.x, box.y + 64f + i * 18f, w, 18f), splits[i], small);
    }
}