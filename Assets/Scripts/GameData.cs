using UnityEngine;

public enum PlayerRole { Boy, Girl }
public enum PowerState { Small, Wings, Fire }
public enum PlayerPose { Normal, Sit, Kneel }
public enum BlockType { Question, Brick }
public enum BlockContent { None, Coin, Power }
public enum EnemyKind { Cat, Turtle, Frog }
public enum PowerKind { Wings, Fire }

/// <summary>What the girl's phone learns about the boy's room.</summary>
public class RoomInfo
{
    public string code;
    public string boyName = "";
    public int ring;
}

/// <summary>The rings the boy can buy after level 10. Prices scale with the coins available in the game.</summary>
public static class Rings
{
    public const int Count = 5;
    public static readonly string[] Names = { "CLOVER RING", "SILVER BAND", "RUBY RING", "SAPPHIRE RING", "DIAMOND RING" };
    // share of all coins in the game each ring costs (the clover ring is free so nobody is ever stuck)
    static readonly float[] Share = { 0f, 0.12f, 0.28f, 0.45f, 0.65f };

    public static int PriceFor(int index)
    {
        if (index <= 0) return 0;
        int total = LevelGenerator.TotalCoins();
        return Mathf.Max(5, Mathf.RoundToInt(total * Share[Mathf.Clamp(index, 0, Count - 1)] / 5f) * 5);
    }

    public static Sprite Icon(int index) { return SpriteFactory.Get("ring_" + Mathf.Clamp(index, 0, Count - 1)); }
    public static string NameOf(int index) { return Names[Mathf.Clamp(index, 0, Count - 1)]; }
}

public static class NameUtil
{
    public const int MaxLength = 10;

    public static string Clean(string raw, string fallback)
    {
        string s = string.IsNullOrEmpty(raw) ? "" : raw.Trim();
        if (s.Length > MaxLength) s = s.Substring(0, MaxLength);
        return s.Length == 0 ? fallback : s;
    }
}
