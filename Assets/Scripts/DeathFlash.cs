using UnityEngine;

public class DeathFlash : MonoBehaviour
{
    const float FlashTime = 0.35f;
    const float SlowTime = 0.15f;

    float flash;
    float slow;
    int lastCount;
    int lastLevel = -1;
    Texture2D pixel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("DeathFlash").AddComponent<DeathFlash>();
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

        if (gm.levelIndex != lastLevel)
        {
            lastLevel = gm.levelIndex;
            lastCount = gm.corpses.Count;
        }

        int count = gm.corpses.Count;
        if (count > lastCount)
        {
            flash = FlashTime;
            slow = SlowTime;
        }
        lastCount = count;

        if (slow > 0f && gm.mode == GameManager.Mode.Play)
        {
            slow -= Time.unscaledDeltaTime;
            Time.timeScale = 0.3f;
            if (slow <= 0f) Time.timeScale = 1f;
        }

        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (flash <= 0f) return;
        float alpha = flash / FlashTime * 0.45f;
        GUI.color = new Color(1f, 0.1f, 0.15f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), pixel);
        GUI.color = Color.white;
    }
}