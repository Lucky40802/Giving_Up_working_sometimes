using UnityEngine;
using UnityEngine.InputSystem;

public class StatsPanel : MonoBehaviour
{
    const string BodiesKey = "stats_bodies";
    const string SecondsKey = "stats_seconds";

    int totalBodies;
    float totalSeconds;
    int lastCount;
    int lastLevel = -1;
    bool visible;
    float saveTimer;
    GUIStyle title;
    GUIStyle line;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("StatsPanel").AddComponent<StatsPanel>();
    }

    void Start()
    {
        totalBodies = PlayerPrefs.GetInt(BodiesKey, 0);
        totalSeconds = PlayerPrefs.GetFloat(SecondsKey, 0f);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            visible = !visible;

        var gm = GameManager.I;
        if (gm == null) return;

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            lastCount = gm.corpses.Count;
        }

        int count = gm.corpses.Count;
        if (count > lastCount) totalBodies += count - lastCount;
        lastCount = count;

        if (gm.mode == GameManager.Mode.Play)
        {
            totalSeconds += Time.deltaTime;
            saveTimer += Time.deltaTime;
            if (saveTimer > 5f) Save();
        }
    }

    void Save()
    {
        saveTimer = 0f;
        PlayerPrefs.SetInt(BodiesKey, totalBodies);
        PlayerPrefs.SetFloat(SecondsKey, totalSeconds);
        PlayerPrefs.Save();
    }

    void OnApplicationQuit() { Save(); }

    void OnGUI()
    {
        if (!visible || GameManager.I == null) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            line = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        }

        var box = new Rect(Screen.width / 2f - 180f, Screen.height / 2f - 110f, 360f, 220f);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x + 20f, box.y + 10f, 320f, 40f), "STATS", title);

        int minutes = (int)(totalSeconds / 60f);
        int seconds = (int)(totalSeconds % 60f);
        float best = Records.Best(GameManager.I.levelIndex);

        GUI.Label(new Rect(box.x + 20f, box.y + 60f, 320f, 30f), "Bodies made: "+ totalBodies, line);
        GUI.Label(new Rect(box.x + 20f, box.y + 95f, 320f, 30f), "Time played: "+ minutes + "m "+ seconds + "s", line);
        GUI.Label(new Rect(box.x + 20f, box.y + 130f, 320f, 30f), "Levels open: "+ Records.Unlocked, line);
        GUI.Label(new Rect(box.x + 20f, box.y + 165f, 320f, 30f), "Best this level: "+ Records.Fmt(best), line);
    }
}
