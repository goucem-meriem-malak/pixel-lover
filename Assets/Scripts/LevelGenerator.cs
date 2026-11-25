using System.Collections.Generic;
using UnityEngine;

// Plain data describing one level. LevelManager turns this into GameObjects.
public class GroundSeg { public float x0, x1, top; }
public class BlockSpawn { public float x, y; public BlockType type; public BlockContent content; }
public class PipeSpawn { public float x, top, height; public bool plant; }   // x = centre
public class StairSpawn { public float x0, top; public int steps; }
public class EnemySpawn { public float x, y, speed; public EnemyKind kind; public int variant; }

public class LevelData
{
    public readonly List<GroundSeg> grounds = new List<GroundSeg>();
    public readonly List<BlockSpawn> blocks = new List<BlockSpawn>();
    public readonly List<PipeSpawn> pipes = new List<PipeSpawn>();
    public readonly List<StairSpawn> stairs = new List<StairSpawn>();
    public readonly List<EnemySpawn> enemies = new List<EnemySpawn>();
    public readonly List<Vector2> coins = new List<Vector2>();   // coins (boy) / flowers (girl)
    public Vector2 startPos;   // feet position of the spawn
    public float plazaStart;   // start of the final flat plaza
    public float flagX;        // x of the end flag
    public float castleX;      // x of the castle behind the flag
    public float flagTop;      // ground height at the flag
    public float endX;         // right edge of the level
}

/// <summary>
/// Deterministic procedural generator: the same level number always gives the same level.
/// Difficulty ramps from level 1 to level 10: wider gaps, more cats and turtles, pipes with
/// piranha plants, brick stairs. Jump physics in PlayerController2D (height about 2.7 units,
/// distance about 5 units) are respected: gaps are capped at 3 units and steps at 1 unit,
/// pipes are at most 2 units tall, so every level can be completed.
/// </summary>
public static class LevelGenerator
{
    static int totalCoins = -1;

    public static LevelData Generate(int level, bool hard = false)
    {
        var rnd = new System.Random(4242 + level * 7919);
        var d = new LevelData();
        // Boy levels are a lot harder than the girl's: his curve starts higher and ends past 1,
        // hers is gentler (about 70%). Gaps never exceed 3 units, so every level stays finishable.
        float diff = (level - 1) / 9f;               // 0 .. 1
        diff = hard ? Mathf.Min(1.15f, diff + 0.30f) : diff * 0.70f;
        int targetLength = 60 + level * 10 + (hard ? 12 + level * 3 : 0);
        int gapMax = (hard ? level < 2 : level < 4) ? 2 : 3;

        float x = 0f, top = 0f;

        // Safe starting area with the first row of blocks (a power-up block for the girl)
        AddGround(d, 0f, 12f, top);
        d.startPos = new Vector2(2f, top);
        d.blocks.Add(new BlockSpawn { x = 6.5f, y = top + 3.5f, type = BlockType.Brick, content = BlockContent.None });
        d.blocks.Add(new BlockSpawn { x = 7.5f, y = top + 3.5f, type = BlockType.Question, content = BlockContent.Power });
        d.blocks.Add(new BlockSpawn { x = 8.5f, y = top + 3.5f, type = BlockType.Brick, content = BlockContent.None });
        for (int i = 0; i < 4; i++) d.coins.Add(new Vector2(3f + i, top + 1.3f));
        x = 12f;

        var runs = new List<Vector3>();   // (x0, x1, top) of every decorated run
        float prevX1 = 12f, prevTop = 0f;
        bool prevTailClear = false;

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

            bool stairs = false;
            if (gap > 0f && prevTailClear && level >= 3 && rnd.NextDouble() < 0.40)
            {
                d.stairs.Add(new StairSpawn { x0 = prevX1 - 3f, top = prevTop, steps = 3 });
                stairs = true;
            }

            if (gap > 0f)
            {
                // an arc of coins that shows where to jump
                float h = Mathf.Max(top, newTop) + 2.0f + (stairs ? 1.5f : 0f);
                for (int i = 1; i <= 3; i++)
                    d.coins.Add(new Vector2(x + gap * i / 4f, h + (i == 2 ? 0.4f : 0f)));
            }

            x += gap;
            float w = rnd.Next(5, 10); // 5..9
            AddGround(d, x, x + w, newTop);
            runs.Add(new Vector3(x, x + w, newTop));
            prevTailClear = Decorate(d, rnd, x, x + w, newTop, level, diff) && w >= 6f;
            prevX1 = x + w;
            prevTop = newTop;
            x += w;
            top = newTop;
        }

        // Final plaza (flat): flag, castle, the place where the boy waits
        d.plazaStart = x;
        EnsureFrogs(d, level);
        if (hard) MakeHarder(d, runs, level, diff);
        LiftCoinsOffStairs(d);
        // keep the way to the plaza clear: no enemy within 5 units of the castle area
        d.enemies.RemoveAll(e => e.x > x - 5f);
        AddGround(d, x, x + 18f, top);
        d.flagX = x + 11f;
        d.castleX = x + 15.5f;
        d.flagTop = top;
        d.endX = x + 18f;
        for (int i = 0; i < 5; i++) d.coins.Add(new Vector2(x + 2.5f + i, top + 1.3f));

        return d;
    }

    /// <summary>
    /// Boy-only difficulty bump. It never touches the terrain (so every level stays beatable and the coin
    /// count stays the same): enemies are faster, more runs get an enemy, and more pipes have a plant.
    /// </summary>
    static void MakeHarder(LevelData d, List<Vector3> runs, int level, float diff)
    {
        var rnd = new System.Random(900 + level * 31);
        foreach (var e in d.enemies) e.speed *= 1.35f;
        foreach (var p in d.pipes)
            if (level >= 2 && !p.plant && rnd.NextDouble() < 0.7) p.plant = true;

        for (int i = 1; i < runs.Count - 1; i++)
        {
            Vector3 r = runs[i];
            float w = r.y - r.x;
            if (w < 6f || level < 2) continue;
            bool busy = false;
            foreach (var e in d.enemies) if (e.x > r.x - 0.5f && e.x < r.y + 0.5f) busy = true;
            if (busy) continue;
            bool hasPipe = false;
            foreach (var p in d.pipes) if (p.x > r.x - 1f && p.x < r.y + 1f) hasPipe = true;
            if (rnd.NextDouble() > 0.55 + 0.30 * diff) continue;
            float ex = hasPipe ? r.y - 2f : (r.x + r.y) * 0.5f;
            bool turtle = rnd.NextDouble() < 0.3;
            bool frog = !turtle && rnd.NextDouble() < 0.45;
            d.enemies.Add(new EnemySpawn
            {
                x = ex, y = r.z,
                kind = turtle ? EnemyKind.Turtle : (frog ? EnemyKind.Frog : EnemyKind.Cat),
                variant = rnd.Next(4),
                speed = (1.6f + 1.4f * diff) * 1.2f
            });
        }
    }

    /// <summary>Every level from 2 on gets at least two frogs (cats become frogs), three from level 6.</summary>
    static void EnsureFrogs(LevelData d, int level)
    {
        if (level < 2) return;
        int want = level >= 6 ? 3 : 2;
        int have = 0;
        foreach (var e in d.enemies) if (e.kind == EnemyKind.Frog) have++;
        for (int i = 0; i < d.enemies.Count && have < want; i += 2)
            if (d.enemies[i].kind == EnemyKind.Cat) { d.enemies[i].kind = EnemyKind.Frog; have++; }
        for (int i = 1; i < d.enemies.Count && have < want; i += 2)
            if (d.enemies[i].kind == EnemyKind.Cat) { d.enemies[i].kind = EnemyKind.Frog; have++; }
    }

    /// <summary>Coins that would sit inside a stair block (so they could not be collected) are moved above it.</summary>
    static void LiftCoinsOffStairs(LevelData d)
    {
        for (int c = 0; c < d.coins.Count; c++)
        {
            Vector2 p = d.coins[c];
            foreach (var s in d.stairs)
                for (int i = 0; i < s.steps; i++)
                {
                    float left = s.x0 + i - 0.5f, right = s.x0 + i + 1.5f;
                    float stepTop = s.top + i + 1;
                    if (p.x > left && p.x < right && p.y < stepTop + 0.8f && p.y > s.top - 0.5f)
                        p.y = stepTop + 0.9f;
                }
            d.coins[c] = p;
        }
    }

    /// <summary>Coins the boy can collect in one level (coin blocks count 1, power blocks 3).</summary>
    public static int CoinsIn(LevelData d)
    {
        int n = d.coins.Count;
        foreach (var b in d.blocks)
        {
            if (b.content == BlockContent.Coin) n += 1;
            else if (b.content == BlockContent.Power) n += 3;
        }
        return n;
    }

    /// <summary>All coins in the whole game (used to price the rings so they are always fair).</summary>
    public static int TotalCoins()
    {
        if (totalCoins < 0)
        {
            int sum = 0;
            for (int l = 1; l <= LevelManager.TotalLevels; l++) sum += CoinsIn(Generate(l, true));
            totalCoins = sum;
        }
        return totalCoins;
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

    /// <summary>Fills one ground run. Returns true if the last 3.5 units of the run are free (room for stairs).</summary>
    static bool Decorate(LevelData d, System.Random rnd, float x0, float x1, float top, int level, float diff)
    {
        float w = x1 - x0;
        float mid = (x0 + x1) * 0.5f;
        bool pipe = false;
        float pipeX = mid;
        float blockEnd = x0;

        // pipe (with a piranha plant from level 3)
        if (level >= 2 && w >= 8f && rnd.NextDouble() < 0.20 + 0.35 * diff)
        {
            float h = rnd.Next(1, 3);
            pipeX = x0 + 3f + Mathf.Floor((float)rnd.NextDouble() * (w - 6f));
            d.pipes.Add(new PipeSpawn { x = pipeX, top = top, height = h, plant = level >= 3 && rnd.NextDouble() < 0.75 });
            pipe = true;
        }

        // a row of bricks / question blocks you can hit from below
        if (!pipe && w >= 5f && rnd.NextDouble() < 0.55)
        {
            int n = rnd.Next(2, Mathf.Min(5, (int)w - 2) + 1);
            float bx = x0 + 1.5f + Mathf.Floor((float)rnd.NextDouble() * (w - 3f - n + 1f));
            bool anyQuestion = false;
            for (int i = 0; i < n; i++)
            {
                var b = new BlockSpawn { x = bx + i + 0.5f, y = top + 3.5f, type = BlockType.Brick, content = BlockContent.None };
                if (rnd.NextDouble() < 0.35 || (i == n / 2 && !anyQuestion && i == n - 1))
                {
                    b.type = BlockType.Question;
                    b.content = rnd.NextDouble() < 0.65 ? BlockContent.Coin : BlockContent.Power;
                    anyQuestion = true;
                }
                d.blocks.Add(b);
            }
            if (!anyQuestion)
            {
                var mb = d.blocks[d.blocks.Count - 1 - n / 2];
                mb.type = BlockType.Question;
                mb.content = BlockContent.Coin;
            }
            blockEnd = bx + n;
        }

        // cats and turtles
        if (w >= 6f && rnd.NextDouble() < 0.25 + 0.50 * diff)
        {
            int count = (w >= 8f && level >= 5 && !pipe && rnd.NextDouble() < 0.4) ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                float ex = count == 1 ? mid : (i == 0 ? x0 + 2.0f : x1 - 2.0f);
                if (pipe) ex = pipeX > mid ? x0 + 2.0f : x1 - 2.0f;
                bool turtle = level >= 2 && rnd.NextDouble() < 0.25 + 0.30 * diff;
                // frogs replace some cats (picked by a hash, so the random sequence of the level is unchanged)
                bool frog = !turtle && ((int)(ex * 7f) + level * 13) % 100 < 40;
                d.enemies.Add(new EnemySpawn
                {
                    x = ex, y = top,
                    kind = turtle ? EnemyKind.Turtle : (frog ? EnemyKind.Frog : EnemyKind.Cat),
                    variant = rnd.Next(4),
                    speed = (1.3f + 1.4f * diff) * (turtle ? 0.8f : 1f)
                });
            }
        }

        // coins (boy) / flowers (girl)
        if (rnd.NextDouble() < 0.80)
        {
            int n = rnd.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                if (pipe)
                {
                    float t = n == 1 ? 0f : (i - (n - 1) / 2f) / ((n - 1) / 2f);
                    float h = d.pipes[d.pipes.Count - 1].height;
                    d.coins.Add(new Vector2(pipeX + (i - (n - 1) / 2f) * 0.9f, top + h + 0.9f + 1.4f * (1f - t * t)));
                }
                else
                {
                    d.coins.Add(new Vector2(x0 + 1.5f + i, top + 1.3f));
                }
            }
        }

        return !pipe && blockEnd <= x1 - 3.5f;
    }
}
