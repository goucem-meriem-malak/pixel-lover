using System.Collections;
using UnityEngine;

/// <summary>
/// Brick or question block. Jump into it from below and it bumps (just like in Super Mario):
/// question blocks release a coin/flower or a power-up and turn into a used block, a big girl
/// smashes bricks, and anything standing on top of the block is knocked out.
/// </summary>
public class Block : MonoBehaviour
{
    public BlockType type;
    public BlockContent content;
    public bool sky;

    SpriteRenderer sr;
    Vector3 home;
    bool used, busy;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        home = transform.position;
    }

    public void Hit(PlayerController2D p)
    {
        if (busy) return;
        bool hasContent = content != BlockContent.None && !used;

        if (type == BlockType.Brick && p.IsBig && !hasContent)
        {
            Smash();
            return;
        }

        if (hasContent)
        {
            used = true;
            Release(p);
            sr.sprite = SpriteFactory.Get(sky ? "tile_block_used_sky" : "tile_block_used");
        }
        StartCoroutine(Bump());
    }

    void Release(PlayerController2D p)
    {
        var lm = LevelManager.Instance;
        Vector3 above = home + new Vector3(0f, 1f, 0f);
        if (content == BlockContent.Coin)
        {
            lm.PopCollectible(above);
        }
        else // Power: a power-up for the girl, a handful of coins for the boy
        {
            if (lm.Role == PlayerRole.Girl)
                lm.SpawnPowerUp(home, p.Form == PowerState.Small ? PowerKind.Grow : PowerKind.Fire);
            else
                lm.PopCoins(above, 3);
        }
    }

    IEnumerator Bump()
    {
        busy = true;
        KnockOutEnemiesOnTop();
        float t = 0f;
        while (t < 0.07f)
        {
            t += Time.deltaTime;
            transform.position = home + new Vector3(0f, 0.22f * Mathf.Clamp01(t / 0.07f), 0f);
            yield return null;
        }
        t = 0f;
        while (t < 0.09f)
        {
            t += Time.deltaTime;
            transform.position = home + new Vector3(0f, 0.22f * (1f - Mathf.Clamp01(t / 0.09f)), 0f);
            yield return null;
        }
        transform.position = home;
        busy = false;
    }

    void KnockOutEnemiesOnTop()
    {
        var found = Physics2D.OverlapBoxAll(home + new Vector3(0f, 0.8f, 0f), new Vector2(0.9f, 0.6f), 0f);
        foreach (var c in found)
        {
            var e = c.GetComponent<EnemyBase>();
            if (e != null && !e.Dead) e.Kill(true);
        }
    }

    void Smash()
    {
        var sprite = SpriteFactory.Get("brick_debris");
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Debris");
            go.transform.SetParent(transform.parent, false);
            go.transform.position = home + new Vector3((i % 2 == 0 ? -0.25f : 0.25f), (i < 2 ? 0.25f : -0.25f), 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = 12;
            var d = go.AddComponent<Debris>();
            d.velocity = new Vector2((i % 2 == 0 ? -1f : 1f) * Random.Range(1.5f, 3f), (i < 2 ? 7f : 4.5f) + Random.value);
        }
        KnockOutEnemiesOnTop();
        Destroy(gameObject);
    }
}

/// <summary>Flying brick piece.</summary>
public class Debris : MonoBehaviour
{
    public Vector2 velocity;
    float life = 1.2f;

    void Update()
    {
        velocity.y -= 25f * Time.deltaTime;
        transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.deltaTime;
        transform.Rotate(0f, 0f, 360f * Time.deltaTime);
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}

/// <summary>
/// Growth mushroom / fire flower that pops out of a block, then slides along the ground until the
/// girl picks it up. (Only the girl gets power-ups.)
/// </summary>
public class PowerUp : MonoBehaviour
{
    public PowerKind kind;

    Rigidbody2D rb;
    SpriteRenderer sr;
    BoxCollider2D solid;
    int dir = 1;
    bool active;
    float turnCooldown;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        solid = GetComponent<BoxCollider2D>();
    }

    void Start() { StartCoroutine(Emerge()); }

    IEnumerator Emerge()
    {
        rb.simulated = false;
        sr.sortingOrder = 2;                       // behind the block while it climbs out
        Vector3 from = transform.position, to = from + new Vector3(0f, 1.0f, 0f);
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, t / 0.45f);
            yield return null;
        }
        sr.sortingOrder = 7;
        rb.position = to;
        rb.simulated = true;
        var lm = LevelManager.Instance;
        if (lm != null && lm.PlayerCollider != null) Physics2D.IgnoreCollision(solid, lm.PlayerCollider);
        active = true;
    }

    void FixedUpdate()
    {
        if (!active) return;
        Vector2 v = rb.GetVel();
        turnCooldown -= Time.fixedDeltaTime;
        if (turnCooldown <= 0f && Mathf.Abs(v.x) < 0.3f && Mathf.Abs(v.y) < 0.5f)
        {
            dir = -dir;                              // bumped into a wall
            turnCooldown = 0.2f;
        }
        v.x = dir * 2.4f;
        rb.SetVel(v);
        if (transform.position.y < -12f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponent<PlayerController2D>();
        if (p == null || p.IsDead) return;
        p.CollectPower(kind);
        Destroy(gameObject);
    }
}

/// <summary>Bouncing fireball thrown by the fire-form girl.</summary>
public class Fireball : MonoBehaviour
{
    public int dir = 1;
    Rigidbody2D rb;
    float life = 3f;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void FixedUpdate()
    {
        Vector2 v = rb.GetVel();
        v.x = dir * 9f;
        rb.SetVel(v);
        life -= Time.fixedDeltaTime;
        if (life <= 0f || transform.position.y < -12f) Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        Vector2 n = c.GetContact(0).normal;
        if (Mathf.Abs(n.x) > 0.6f) { Destroy(gameObject); return; }   // hit a wall or pipe
        Vector2 v = rb.GetVel();
        v.y = 7f;                                                       // hop along the ground
        rb.SetVel(v);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var target = other.GetComponent<IFireHittable>();
        if (target == null) return;
        target.FireHit();
        Destroy(gameObject);
    }
}
