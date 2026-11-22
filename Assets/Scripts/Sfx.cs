using System.Collections.Generic;
using UnityEngine;

public class Sfx : MonoBehaviour
{
    const int RATE = 22050;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource[] pool;
    AudioSource music;
    public bool muted;

    void Awake()
    {
        pool = new AudioSource[8];
        for (int i = 0; i < pool.Length; i++) pool[i] = gameObject.AddComponent<AudioSource>();
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = 0.25f;
        Build();
        music.clip = clips["music"];
        SetMuted(Records.Muted);
    }

    static float Wave(string kind, float phase)
    {
        float ph = phase - Mathf.Floor(phase);
        switch (kind)
        {
            case "sine": return Mathf.Sin(phase * Mathf.PI * 2f);
            case "square": return ph < 0.5f ? 1f : -1f;
            case "saw": return ph * 2f - 1f;
            case "tri": return Mathf.Abs(ph * 4f - 2f) - 1f;
        }
        return Random.value * 2f - 1f;
    }

    static float[] Tone(float f0, float f1, float dur, string kind, float vol = 0.5f, float decay = 3f)
    {
        int n = (int)(dur * RATE);
        var o = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            phase += Mathf.Lerp(f0, f1, k) / RATE;
            float env = Mathf.Exp(-decay * k) * Mathf.Min(1f, i / 60f);
            o[i] = Wave(kind, phase) * env * vol;
        }
        return o;
    }

    static float[] Cat(params float[][] parts)
    {
        var l = new List<float>();
        foreach (var p in parts) l.AddRange(p);
        return l.ToArray();
    }

    static float[] Mix(float[] a, float[] b)
    {
        var o = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < o.Length; i++)
            o[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
        return o;
    }

    static float[] Arp(float[] freqs, float step, string kind = "square", float vol = 0.3f)
    {
        var parts = new float[freqs.Length][];
        for (int i = 0; i < freqs.Length; i++) parts[i] = Tone(freqs[i], freqs[i], step, kind, vol, 2.5f);
        return Cat(parts);
    }

    void Add(string name, float[] data)
    {
        var c = AudioClip.Create(name, data.Length, 1, RATE, false);
        c.SetData(data, 0);
        clips[name] = c;
    }

    void Build()
    {
        Add("jump", Tone(260, 620, 0.13f, "square", 0.22f, 4));
        Add("land", Mix(Tone(1, 1, 0.09f, "noise", 0.25f, 6), Tone(110, 50, 0.1f, "sine", 0.4f, 4)));
        Add("die", Mix(Tone(520, 60, 0.42f, "saw", 0.28f, 2.5f), Tone(1, 1, 0.26f, "noise", 0.22f, 5)));
        Add("win", Arp(new float[] { 523, 659, 784, 1047, 1319 }, 0.1f));
        Add("best", Arp(new float[] { 784, 988, 1175, 1568, 1175, 1568 }, 0.09f, "tri", 0.4f));
        Add("pad", Tone(180, 950, 0.24f, "sine", 0.5f, 2));
        Add("portal", Cat(Tone(300, 900, 0.12f, "sine", 0.4f, 1), Tone(900, 300, 0.14f, "sine", 0.4f, 2)));
        Add("pickup", Cat(Tone(660, 660, 0.06f, "square", 0.22f, 1.5f), Tone(990, 990, 0.12f, "square", 0.22f, 3)));
        Add("smash", Mix(Tone(1, 1, 0.26f, "noise", 0.35f, 4), Tone(200, 40, 0.26f, "saw", 0.3f, 3)));
        Add("knock", Tone(240, 50, 0.3f, "saw", 0.4f, 3));
        Add("pop", Tone(700, 200, 0.12f, "square", 0.25f, 4));
        Add("click", Tone(800, 800, 0.04f, "square", 0.18f, 2));
        Add("move", Tone(500, 520, 0.03f, "square", 0.12f, 2));
        Add("toggle", Tone(440, 880, 0.1f, "tri", 0.4f, 2));
        Add("nope", Tone(160, 120, 0.12f, "square", 0.2f, 3));
        var lead = Arp(new float[] { 220, 262, 330, 392, 330, 262, 196, 262 }, 0.25f, "tri", 0.3f);
        var bass = Cat(Tone(110, 110, 0.5f, "sine", 0.4f, 1.2f), Tone(131, 131, 0.5f, "sine", 0.4f, 1.2f),
                       Tone(98, 98, 0.5f, "sine", 0.4f, 1.2f), Tone(131, 131, 0.5f, "sine", 0.4f, 1.2f));
        Add("music", Mix(lead, bass));
    }

    public void Play(string name, float pitch = 1f)
    {
        if (muted || !clips.ContainsKey(name)) return;
        foreach (var p in pool)
        {
            if (!p.isPlaying) { Fire(p, name, pitch); return; }
        }
        Fire(pool[0], name, pitch);
    }

    void Fire(AudioSource p, string name, float pitch)
    {
        p.clip = clips[name];
        p.pitch = pitch;
        p.Play();
    }

    public void SetMuted(bool m)
    {
        muted = m;
        Records.Muted = m;
        if (m) music.Stop();
        else if (!music.isPlaying) music.Play();
    }

    public void Toggle()
    {
        SetMuted(!muted);
        if (!muted) Play("toggle");
    }
}
