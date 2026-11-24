using System.Collections.Generic;
using UnityEngine;

public class ToastBanner : MonoBehaviour
{
    struct Toast
    {
        public string text;
        public float life;
    }

    const float Duration = 2.2f;
     const float FadeTime = 0.4f;
    static readonly int[] Milestones = { 5, 10, 25, 50, 100 };

    readonly Queue<Toast> queue = new Queue<Toast>();
    readonly HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

    Toast current;
    bool showing;
    int lastLevel = -1;
    int sessionDeaths;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("ToastBanner").AddComponent<ToastBanner>();
    }

    void Push(string text)
    {
        queue.Enqueue(new Toast { text = text, life = Duration });
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            seen.Clear();
            foreach (var cell in gm.corpses.Keys) seen.Add(cell);
            Push("LEVEL "+ (gm.levelIndex + 1));
        }

        seen.RemoveWhere(c => !gm.corpses.ContainsKey(c));
        foreach (var cell in gm.corpses.Keys)
        {
            if (!seen.Add(cell)) continue;
            sessionDeaths++;
            CheckMilestone();
        }

        if (!showing && queue.Count > 0)
        {
            current = queue.Dequeue();
            showing = true;
        }

        if (showing)
        {
            current.life -= Time.unscaledDeltaTime;
            if (current.life <= 0f) showing = false;
        }
    }

    void CheckMilestone()
    {
        foreach (int m in Milestones)
        {
            if (sessionDeaths == m)
                Push(m + " DEATHS - KEEP GOING");
        }
    }

    float Alpha()
    {
        float elapsed = Duration - current.life;
        float fadeIn = Mathf.Clamp01(elapsed / FadeTime);
        float fadeOut = Mathf.Clamp01(current.life / FadeTime);
        return Mathf.Min(fadeIn, fadeOut);
    }

    void OnGUI()
    {
        if (!showing) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 34;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
        }

        float a = Alpha();
        float slide = (1f - a) * 20f;
        var rect = new Rect(0f, Screen.height * 0.22f - slide, Screen.width, 60f);

        style.normal.textColor = new Color(0f, 0f, 0f, a * 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), current.text, style);

        style.normal.textColor = new Color(1f, 0.9f, 0.4f, a);
        GUI.Label(rect, current.text, style);
    }
}
