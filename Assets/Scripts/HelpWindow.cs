using UnityEngine;
using UnityEngine.InputSystem;

public class HelpWindow : MonoBehaviour
{
    static readonly string[] Tabs = { "Controls", "Records", "Options" };

    static readonly string[,] Binds =
    {
        { "A / D", "Move left / right" },
        { "W / Space", "Jump" },
        { "R", "Die on purpose" },
        { "Arrows + Enter", "Player 2" },
        { "M", "Toggle sound" },
        { "F1", "This window" },
        { "F2", "Death log" },
        { "F3", "Death heatmap" },
        { "F4", "Key display" },
    };

    Rect window = new Rect(40f, 40f, 340f, 340f);
    int tab;
    bool visible;
    string note = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("HelpWindow").AddComponent<HelpWindow>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;
    }

    void OnGUI()
    {
        if (!visible) return;
        window = GUI.Window(7421, window, Draw, "HELP  (F1 to close)");
    }

    void Draw(int id)
    {
        GUILayout.Space(4f);
        tab = GUILayout.Toolbar(tab, Tabs);
        GUILayout.Space(8f);

        if (tab == 0) DrawControls();
        else if (tab == 1) DrawRecords();
        else DrawOptions();

        GUILayout.FlexibleSpace();
        if (note != "") GUILayout.Label(note);

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    void DrawControls()
    {
        for (int i = 0; i < Binds.GetLength(0); i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Binds[i, 0], GUILayout.Width(120f));
            GUILayout.Label(Binds[i, 1]);
            GUILayout.EndHorizontal();
        }
    }

    void DrawRecords()
    {
        GUILayout.Label("Best times (mm:ss.cc)");
        for (int i = 0; i < 5; i++)
        {
            float best = Records.Best(i);
            string text = best > 0f && best < 99999f ? Records.Fmt(best) : "--:--.--";
            GUILayout.Label("Level " + (i + 1) + ":   " + text);
        }
        GUILayout.Label("Unlocked levels: " + Records.Unlocked);
    }

    void DrawOptions()
    {
        var gm = GameManager.I;
        if (gm == null)
        {
            GUILayout.Label("Start the game first.");
            return;
        }

        string soundLabel = gm.sfx.muted ? "Sound: OFF (click to turn on)" : "Sound: ON (click to mute)";
        if (GUILayout.Button(soundLabel, GUILayout.Height(30f)))
        {
            gm.sfx.muted = !gm.sfx.muted;
            note = gm.sfx.muted ? "Muted." : "Sound on.";
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Reset all records", GUILayout.Height(30f)))
        {
            Records.Reset();
            note = "Records cleared.";
        }
    }
}