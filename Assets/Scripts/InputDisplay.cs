using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputDisplay : MonoBehaviour
{
    class KeyBox
    {
        public Key key;
        public string label;
        public Vector2 slot;
        public int presses;
    }

    const float Size = 36f;
    const float Gap = 4f;
    const int MaxRecent = 6;

    readonly List<KeyBox> boxes = new List<KeyBox>();
    readonly List<string> recent = new List<string>();

    bool visible;
    Texture2D pixel;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("InputDisplay").AddComponent<InputDisplay>();
    }

    void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        Add(Key.W, "W", 1, 0);
        Add(Key.A, "A", 0, 1);
        Add(Key.S, "S", 1, 1);
        Add(Key.D, "D", 2, 1);
        Add(Key.Space, "SP", 3, 1);
        Add(Key.R, "R", 4, 1);
    }

    void Add(Key key, string label, int col, int row)
    {
        boxes.Add(new KeyBox
        {
            key = key,
            label = label,
            slot = new Vector2(col * (Size + Gap), row * (Size + Gap))
        });
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f4Key.wasPressedThisFrame) visible = !visible;

        foreach (var box in boxes)
        {
            if (!kb[box.key].wasPressedThisFrame) continue;
            box.presses++;
            recent.Insert(0, box.label);
            if (recent.Count > MaxRecent) recent.RemoveAt(recent.Count - 1);
        }
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
        }

        var kb = Keyboard.current;
        float x0 = Screen.width - 5f * (Size + Gap) - 16f;
        float y0 = Screen.height - 2f * (Size + Gap) - 210f;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x0 - 8f, y0 - 8f, 5f * (Size + Gap) + 12f, 2f * (Size + Gap) + 62f), pixel);

        foreach (var box in boxes)
        {
            bool down = kb != null && kb[box.key].isPressed;
            GUI.color = down ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.2f);
            var rect = new Rect(x0 + box.slot.x, y0 + box.slot.y, Size, Size);
            GUI.DrawTexture(rect, pixel);

            GUI.color = down ? Color.black : Color.white;
            GUI.Label(rect, box.label, style);
        }

        GUI.color = Color.white;
        float ty = y0 + 2f * (Size + Gap);
        GUI.Label(new Rect(x0, ty, 5f * (Size + Gap), 22f), "recent: " + string.Join(" ", recent), style);
        GUI.Label(new Rect(x0, ty + 22f, 5f * (Size + Gap), 22f), "INPUT (F4)  presses: " + TotalPresses(), style);
    }

    int TotalPresses()
    {
        int total = 0;
        foreach (var box in boxes) total += box.presses;
        return total;
    }
}