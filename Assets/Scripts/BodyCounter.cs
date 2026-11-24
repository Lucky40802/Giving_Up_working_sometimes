using UnityEngine;

public class BodyCounter : MonoBehaviour
{
    int lastCount;
    float flash;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("BodyCounter").AddComponent<BodyCounter>();
    }

    void Update()
    {
        if (GameManager.I == null) return;
        int count = GameManager.I.corpses.Count;
        if (count != lastCount)
        {
            flash = 1f;
            lastCount = count;
        }
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 3f);
    }

    void OnGUI()
    {
        if (GameManager.I == null || GameManager.I.mode != GameManager.Mode.Play) return;
        if (style == null) style = new GUIStyle(GUI.skin.label);

        float redness = Mathf.Clamp01(lastCount / 10f);
        Color tint = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.35f), redness);
        tint = Color.Lerp(tint, Color.white, flash);

        style.alignment = TextAnchor.UpperCenter;
        style.fontSize = Mathf.RoundToInt(Screen.height * (0.05f + 0.03f * flash));
        style.normal.textColor = tint;

        GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, Screen.height * 0.15f), lastCount + " bodies", style);
    }
}