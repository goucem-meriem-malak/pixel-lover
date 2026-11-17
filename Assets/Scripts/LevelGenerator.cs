using System.Collections.Generic;
using UnityEngine;

// Plain data describing one level. LevelManager turns this into GameObjects.
public class GroundSeg { public float x0, x1, top; }
public class PlatformSeg { public float x0, x1, top; }
public class EnemySpawn { public float x, y, minX, maxX, speed; }
public class SpikeSpawn { public float x, y; }

public class LevelData
{
    public readonly List<GroundSeg> grounds = new List<GroundSeg>();
    public readonly List<PlatformSeg> platforms = new List<PlatformSeg>();
    public readonly List<Vector2> coins = new List<Vector2>();
    public readonly List<EnemySpawn> enemies = new List<EnemySpawn>();
    public readonly List<SpikeSpawn> spikes = new List<SpikeSpawn>();
    public Vector2 startPos;   // feet position of the player spawn
    public float flagX;        // x of the end flag
    public float flagTop;      // ground height at the flag
    public float endX;         // right edge of the level
}

/// <summary>
/// Deterministic procedural generator: the same level number always gives the same level.
/// Difficulty (gap size/frequency, enemies, spikes) ramps from level 1 to level 10.
/// Jump physics in PlayerController2D: height ~2.7 units, distance ~5 units, so gaps are
/// capped at 3 units and steps at 1 unit. Every level is therefore completable.
/// </summary>
public static class LevelGenerator
{
    public static LevelData Generate(int level)
    {
        var rnd = new System.Random(4242 + level * 7919);
        var d = new LevelData();
        float diff = (level - 1) / 9f;               // 0 .. 1
        int targetLength = 60 + level * 10;
        int gapMax = level < 4 ? 2 : 3;

        float x = 0f, top = 0f;

        // Safe starting area
        AddGround(d, x, x + 10f, top);
        d.startPos = new Vector2(2f, top);
        x += 10f;

        while (x < targetLength)
        {
            float gap = 0f;
            if (level >= 2 && rnd.NextDouble() < 0.30 + 0.45 * diff)
                gap = rnd.Next(2, gapMax + 1);

            float newTop = top;
            int roll = rnd.Next(4);
            if (roll == 2) newTop += 1f;
            else if (roll == 3) newTop -= 1f;
            newTop = Mathf.Clamp(newTop, 0f, 2f);

            if (gap > 0f)
            {
                // a little arc of coins to show the player where to jump
                float h = Mathf.Max(top, newTop) + 2.0f;
                for (int i = 1; i <= 3; i++)
                    d.coins.Add(new Vector2(x + gap * i / 4f, h + (i == 2 ? 0.4f : 0f)));
            }

            x += gap;
            float w = rnd.Next(5, 10); // 5..9
            AddGround(d, x, x + w, newTop);
            Decorate(d, rnd, x, x + w, newTop, level, diff);
            x += w;
            top = newTop;
        }

        // End plaza (flat) with the flag
        float plazaStart = x;
        AddGround(d, plazaStart, plazaStart + 18f, top);
        d.flagX = plazaStart + 14f;
        d.flagTop = top;
        d.endX = plazaStart + 18f;
        for (int i = 0; i < 5; i++)
            d.coins.Add(new Vector2(plazaStart + 3f + i, top + 1.3f));

        return d;
    }

    static void AddGround(LevelData d, float x0, float x1, float top)
    {
        if (d.grounds.Count > 0)
        {
            var last = d.grounds[d.grounds.Count - 1];
            if (Mathf.Approximately(last.x1, x0) && Mathf.Approximately(last.top, top))
            {
                last.x1 = x1; // merge: avoids physics seams between neighbouring boxes
                return;
            }
        }
        d.grounds.Add(new GroundSeg { x0 = x0, x1 = x1, top = top });
    }

    static void Decorate(LevelData d, System.Random rnd, float x0, float x1, float top, int level, float diff)
    {
        float w = x1 - x0;
        float mid = (x0 + x1) / 2f;
        bool hazard = false;

        if (level >= 2 && w >= 6f && rnd.NextDouble() < 0.20 + 0.50 * diff)
        {
            d.enemies.Add(new EnemySpawn
            {
                x = mid, y = top, minX = x0 + 1.2f, maxX = x1 - 1.2f,
                speed = 1.4f + 1.8f * diff
            });
            hazard = true;
        }
        else if (level >= 3 && w >= 7f && rnd.NextDouble() < 0.15 + 0.40 * diff)
        {
            int n = (level >= 6 && rnd.NextDouble() < 0.5) ? 2 : 1;
            for (int i = 0; i < n; i++)
                d.spikes.Add(new SpikeSpawn { x = mid - (n - 1) * 0.5f + i, y = top });
            hazard = true;
        }

        if (rnd.NextDouble() < 0.75)
        {
            int n = rnd.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                if (hazard)
                {
                    // arc over the hazard
                    float t = n == 1 ? 0f : (i - (n - 1) / 2f) / ((n - 1) / 2f); // -1..1
                    d.coins.Add(new Vector2(mid + (i - (n - 1) / 2f) * 0.9f, top + 1.0f + 1.6f * (1f - t * t)));
                }
                else
                {
                    d.coins.Add(new Vector2(x0 + 1.5f + i, top + 1.3f));
                }
            }
        }

        if (x0 > 14f && w >= 6f && rnd.NextDouble() < 0.30 + 0.20 * diff)
        {
            int pw = rnd.Next(2, 4);
            float px0 = x0 + 1.5f + (float)rnd.NextDouble() * Mathf.Max(0f, w - 3f - pw);
            float ptop = top + 2.0f;
            d.platforms.Add(new PlatformSeg { x0 = px0, x1 = px0 + pw, top = ptop });
            d.coins.Add(new Vector2(px0 + pw * 0.3f, ptop + 0.9f));
            d.coins.Add(new Vector2(px0 + pw * 0.7f, ptop + 0.9f));
        }
    }
}
