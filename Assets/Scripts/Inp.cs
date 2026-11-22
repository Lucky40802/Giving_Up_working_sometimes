using UnityEngine.InputSystem;

public static class Inp
{
    public static bool Held(Key k) { var kb = Keyboard.current; return kb != null && kb[k].isPressed; }
    public static bool Down(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }

    public static bool Held(params Key[] ks)
    {
        foreach (var k in ks) if (Held(k)) return true;
        return false;
    }

    public static bool Down(params Key[] ks)
    {
        foreach (var k in ks) if (Down(k)) return true;
        return false;
    }
}
