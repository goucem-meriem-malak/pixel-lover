using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates every fallback sprite at runtime (pixel-art characters, tiles, coins, hearts...).
/// Nothing here needs imported art, so the project runs out of the box.
/// To use your own art, assign sprites on the LevelManager component instead.
/// </summary>
public static class SpriteFactory
{
    public const float PPU = 16f;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ------------------------------------------------------------------ core helpers

    static Sprite Cached(string key, Func<Sprite> make)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;
        s = make();
        cache[key] = s;
        return s;
    }

    static Texture2D NewTex(int w, int h, bool repeat)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        return tex;
    }

    static Sprite MakeSprite(Texture2D tex, Vector2 pivot, float ppu)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
    }

    static Sprite FromFunc(string key, int w, int h, Vector2 pivot, bool repeat, Func<int, int, Color> px)
    {
        return Cached(key, () =>
        {
            var tex = NewTex(w, h, repeat);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = px(x, y);
            tex.SetPixels(pixels);
            return MakeSprite(tex, pivot, PPU);
        });
    }

    /// rows[0] is the TOP row. '.' = transparent.
    static Sprite FromPattern(string key, string[] rows, Dictionary<char, Color> pal)
    {
        return Cached(key, () =>
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = NewTex(w, h, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < w; x++)
                {
                    char ch = x < row.Length ? row[x] : '.';
                    Color c;
                    pixels[(h - 1 - y) * w + x] = (ch != '.' && pal.TryGetValue(ch, out c)) ? c : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            return MakeSprite(tex, new Vector2(0.5f, 0f), PPU); // pivot = bottom-centre
        });
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // ------------------------------------------------------------------ primitives

    /// 1x1 world-unit solid square (scale it with transform.localScale).
    public static Sprite Square(Color color)
    {
        return Cached("sq" + ColorUtility.ToHtmlStringRGBA(color), () =>
        {
            var tex = NewTex(1, 1, false);
            tex.SetPixel(0, 0, color);
            return MakeSprite(tex, new Vector2(0.5f, 0.5f), 1f);
        });
    }

    /// ~1 world-unit pixel circle.
    public static Sprite Circle(Color color)
    {
        return FromFunc("ci" + ColorUtility.ToHtmlStringRGBA(color), 16, 16, new Vector2(0.5f, 0.5f), false, (x, y) =>
        {
            float dx = x - 7.5f, dy = y - 7.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 7.4f) return Color.clear;
            if (d > 6.2f) return color * 0.7f + new Color(0, 0, 0, 0.3f);
            if (dx < -1f && dy > 1f && d < 5f) return Color.Lerp(color, Color.white, 0.55f); // shine
            return color;
        });
    }

    public static Sprite Coin() { return Circle(C(1f, 0.82f, 0.1f)); }

    // ------------------------------------------------------------------ tiles (16x16, repeat)

    public static Sprite Dirt()
    {
        return FromFunc("dirt", 16, 16, new Vector2(0.5f, 0.5f), true, (x, y) =>
            ((x * 7 + y * 13) % 11 == 0) ? C(0.42f, 0.26f, 0.14f) : C(0.55f, 0.35f, 0.20f));
    }

    public static Sprite Grass()
    {
        return FromFunc("grass", 16, 16, new Vector2(0.5f, 0.5f), true, (x, y) =>
        {
            if (y >= 14) return C(0.45f, 0.85f, 0.30f);
            if (y >= 12) return ((x + y) % 4 == 0) ? C(0.45f, 0.85f, 0.30f) : C(0.28f, 0.65f, 0.20f);
            return ((x * 7 + y * 13) % 11 == 0) ? C(0.42f, 0.26f, 0.14f) : C(0.55f, 0.35f, 0.20f);
        });
    }

    public static Sprite Brick()
    {
        return FromFunc("brick", 16, 16, new Vector2(0.5f, 0.5f), true, (x, y) =>
        {
            int row = y / 8;
            bool horizontal = (y % 8) == 7;
            bool vertical = (x + (row == 0 ? 0 : 4)) % 8 == 0;
            if (horizontal || vertical) return C(0.35f, 0.20f, 0.15f);
            return C(0.82f, 0.42f, 0.22f);
        });
    }

    public static Sprite Spike()
    {
        return FromFunc("spike", 16, 8, new Vector2(0.5f, 0f), false, (x, y) =>
        {
            float half = (7 - y) * 0.5f + 0.5f;
            float cx = (x % 8) + 0.5f;
            if (Mathf.Abs(cx - 4f) > half) return Color.clear;
            return cx < 4f ? C(0.92f, 0.94f, 0.98f) : C(0.65f, 0.68f, 0.75f);
        });
    }

    // ------------------------------------------------------------------ pixel-art characters

    static Dictionary<char, Color> BoyPalette()
    {
        return new Dictionary<char, Color>
        {
            {'r', C(0.86f, 0.15f, 0.15f)}, {'s', C(0.99f, 0.80f, 0.62f)}, {'h', C(0.35f, 0.20f, 0.10f)},
            {'e', C(0.10f, 0.10f, 0.12f)}, {'b', C(0.15f, 0.42f, 0.92f)}, {'n', C(0.10f, 0.16f, 0.50f)},
            {'o', C(0.30f, 0.15f, 0.05f)}
        };
    }

    static Dictionary<char, Color> GirlPalette()
    {
        return new Dictionary<char, Color>
        {
            {'h', C(0.95f, 0.72f, 0.18f)}, {'p', C(1.00f, 0.25f, 0.55f)}, {'s', C(0.99f, 0.80f, 0.62f)},
            {'e', C(0.10f, 0.10f, 0.12f)}, {'d', C(1.00f, 0.45f, 0.72f)}, {'o', C(0.45f, 0.20f, 0.25f)}
        };
    }

    static readonly string[] BoyStandRows =
    {
        "...rrrrrr...",
        "..rrrrrrrrr.",
        "..hsssssss..",
        "..hsssessss.",
        "..hssssssss.",
        "...sssssss..",
        "...bbbbbb...",
        "..bbbbbbbb..",
        ".ssbbbbbbss.",
        ".ssbbbbbbss.",
        "..bbbbbbbb..",
        "..nnnnnnnn..",
        "..nnn..nnn..",
        "..nnn..nnn..",
        "..ooo..ooo..",
        ".oooo..oooo."
    };

    static readonly string[] BoySitRows =
    {
        "............",
        "............",
        "............",
        "...rrrrrr...",
        "..rrrrrrrrr.",
        "..hsssssss..",
        "..hsssessss.",
        "..hssssssss.",
        "...sssssss..",
        "...bbbbbb...",
        "..bbbbbbbb..",
        ".ssbbbbbbss.",
        "..bbbbbbbb..",
        "..nnnnnnnn..",
        "..nnnnnnnnoo",
        "..nnnnnnnnoo"
    };

    static readonly string[] GirlStandRows =
    {
        "...hhhhhh...",
        "..hhhhhhhhp.",
        ".hhsssssss..",
        ".hhsssessss.",
        ".hhssssssss.",
        ".hhhsssss...",
        ".hhhdddddd..",
        "..hddddddd..",
        ".ssddddddss.",
        ".ssddddddss.",
        "..dddddddd..",
        ".dddddddddd.",
        "dddddddddddd",
        "...ss..ss...",
        "...ss..ss...",
        "..ooo..ooo.."
    };

    static readonly string[] EnemyRows =
    {
        "....bbbb....",
        "..bbbbbbbb..",
        ".bbbbbbbbbb.",
        ".bwkbbbbkwb.",
        ".bbbbbbbbbb.",
        "..bbbbbbbb..",
        "...cccccc...",
        "...cccccc...",
        "..oo....oo..",
        ".ooo....ooo."
    };

    static readonly string[] HeartRows =
    {
        ".rr...rr.",
        "rwrr.rrrr",
        "rrrrrrrrr",
        "rrrrrrrrr",
        ".rrrrrrr.",
        "..rrrrr..",
        "...rrr...",
        "....r...."
    };

    static readonly string[] BouquetRows =
    {
        "..rr..yy..",
        ".rrrr.yyyy",
        "..rr.pppp.",
        ".yyy.pppp.",
        "..yggggp..",
        "...gggg...",
        "..wwwwww..",
        "..wwwwww..",
        "...wwww...",
        "....ww...."
    };

    public static Sprite BoyStand() { return FromPattern("boyStand", BoyStandRows, BoyPalette()); }
    public static Sprite BoySit() { return FromPattern("boySit", BoySitRows, BoyPalette()); }
    public static Sprite GirlStand() { return FromPattern("girlStand", GirlStandRows, GirlPalette()); }

    public static Sprite Enemy()
    {
        return FromPattern("enemy", EnemyRows, new Dictionary<char, Color>
        {
            {'b', C(0.62f, 0.34f, 0.14f)}, {'w', Color.white}, {'k', C(0.05f, 0.05f, 0.05f)},
            {'c', C(0.96f, 0.84f, 0.62f)}, {'o', C(0.25f, 0.12f, 0.05f)}
        });
    }

    public static Sprite Heart()
    {
        return FromPattern("heart", HeartRows, new Dictionary<char, Color>
        {
            {'r', C(1.00f, 0.20f, 0.38f)}, {'w', C(1.00f, 0.75f, 0.82f)}
        });
    }

    public static Sprite Bouquet()
    {
        return FromPattern("bouquet", BouquetRows, new Dictionary<char, Color>
        {
            {'r', C(1.00f, 0.25f, 0.35f)}, {'y', C(1.00f, 0.88f, 0.20f)}, {'p', C(0.85f, 0.45f, 1.00f)},
            {'g', C(0.20f, 0.65f, 0.25f)}, {'w', C(1.00f, 0.95f, 0.90f)}
        });
    }
}
