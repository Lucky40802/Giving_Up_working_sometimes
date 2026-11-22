using UnityEngine;

public static class Records
{
    public static int Unlocked
    {
        get => PlayerPrefs.GetInt("unlocked", 1);
        set { PlayerPrefs.SetInt("unlocked", value); PlayerPrefs.Save(); }
    }

    public static bool Muted
    {
        get => PlayerPrefs.GetInt("muted", 0) == 1;
        set { PlayerPrefs.SetInt("muted", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static float BestRun
    {
        get => PlayerPrefs.GetFloat("bestrun", -1f);
        set { PlayerPrefs.SetFloat("bestrun", value); PlayerPrefs.Save(); }
    }

    public static float Best(int level) => PlayerPrefs.GetFloat("best_" + level, -1f);

    public static bool Submit(int level, float t)
    {
        float old = Best(level);
        bool isBest = old < 0f || t < old;
        if (isBest) PlayerPrefs.SetFloat("best_" + level, t);
        if (level + 2 > Unlocked) Unlocked = level + 2;
        PlayerPrefs.Save();
        return isBest;
    }

    public static bool SubmitRun(float t)
    {
        bool isBest = BestRun < 0f || t < BestRun;
        if (isBest) BestRun = t;
        return isBest;
    }

    public static void UnlockAll(int count) { Unlocked = count; }

    public static void Reset()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    public static string Fmt(float t)
    {
        if (t < 0f) return "--:--.--";
        int total = (int)(t * 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", total / 6000, (total / 100) % 60, total % 100);
    }
}
