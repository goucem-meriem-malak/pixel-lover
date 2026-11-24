using System.Collections;
using UnityEngine;

/// <summary>Anything a fireball can kill.</summary>
public interface IFireHittable { void FireHit(); }

/// <summary>
/// Shared walking logic for cats and turtles: kinematic body that walks, turns at walls and cliffs,
/// falls with simple gravity and is stomped from above. The collider is a trigger so the player
/// overlaps it; the stomp is decided in code.
/// </summary>
public abstract class EnemyBase : MonoBehaviour, IFireHittable
{
    protected Rigidbody2D rb;
    protected BoxCollider2D col;
    protected SpriteRenderer sr;
    protected SpriteAnimator anim;
    protected int dir = -1;
    protected float vy;
    protected bool grounded;

    public bool Dead { get; protected set; }

    static ContactFilter2D filter;
    static bool filterReady;
    static readonly RaycastHit2D[] hits = new RaycastHit2D[6];

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<SpriteAnimator>();
        if (!filterReady)
        {
            filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.useLayerMask = false;
            filter.useDepth = false;
            filter.useNormalAngle = false;
            filterReady = true;
        }
    }

    static bool IsSolid(Collider2D c) { return c != null && !c.isTrigger && c.attachedRigidbody == null; }

    bool AnySolid(int n)
    {
        for (int i = 0; i < n; i++) if (IsSolid(hits[i].collider)) return true;
        return false;
    }

    protected bool WallAhead()
    {
        Bounds b = col.bounds;
        int n = Physics2D.BoxCast(b.center, new Vector2(b.size.x * 0.9f, b.size.y * 0.6f), 0f, new Vector2(dir, 0f), filter, hits, 0.15f);
        return AnySolid(n);
    }

    protected bool GroundAhead()
    {
        Bounds b = col.bounds;
        Vector2 o = new Vector2(b.center.x + dir * (b.extents.x + 0.1f), b.min.y + 0.15f);
        return AnySolid(Physics2D.Raycast(o, Vector2.down, filter, hits, 0.6f));
    }

    protected bool OnGround()
    {
        Bounds b = col.bounds;
        return AnySolid(Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, filter, hits, 0.12f));
    }

    /// <summary>Walk one physics step. Call from FixedUpdate.</summary>
    protected void StepMove(float hSpeed, bool turnAtCliff)
    {
        grounded = OnGround();
        if (grounded && vy <= 0f) vy = 0f; else vy -= 30f * Time.fixedDeltaTime;

        float dx = 0f;
        if (hSpeed > 0f)
        {
            if (WallAhead()) dir = -dir;
            else if (grounded && turnAtCliff && !GroundAhead()) dir = -dir;
            dx = dir * hSpeed * Time.fixedDeltaTime;
        }
        rb.MovePosition(rb.position + new Vector2(dx, vy * Time.fixedDeltaTime));

        if (transform.position.y < -12f) Destroy(gameObject);
    }

    protected void Touch(Collider2D other)
    {
        if (Dead) return;
        var p = other.GetComponent<PlayerController2D>();
        if (p == null || p.IsDead) return;
        OnPlayerTouch(p);
    }

    void OnTriggerEnter2D(Collider2D other) { Touch(other); }
    void OnTriggerStay2D(Collider2D other) { Touch(other); }

    protected abstract void OnPlayerTouch(PlayerController2D p);

    /// <summary>Kill it. flipDeath = spins upside-down and falls out of the level.</summary>
    public virtual void Kill(bool flipDeath)
    {
        if (Dead) return;
        Dead = true;
        col.enabled = false;
        if (anim != null) anim.enabled = false;
        if (flipDeath) StartCoroutine(FlipFall());
        else Destroy(gameObject);
    }

    public virtual void FireHit() { Kill(true); }

    IEnumerator FlipFall()
    {
        sr.flipY = true;
        float v = 6f, t = 0f;
        while (t < 1.6f)
        {
            t += Time.deltaTime;
            v -= 28f * Time.deltaTime;
            transform.position += new Vector3(dir * 1.2f * Time.deltaTime, v * Time.deltaTime, 0f);
            yield return null;
        }
        Destroy(gameObject);
    }

    protected IEnumerator SquashAndDie()
    {
        Dead = true;
        col.enabled = false;
        if (anim != null) anim.enabled = false;
        transform.localScale = new Vector3(1f, 0.3f, 1f);
        yield return new WaitForSeconds(0.35f);
        Destroy(gameObject);
    }
}

/// <summary>Cat: walks back and forth. Four coat colours (orange, black, white, grey).</summary>
public class Cat : EnemyBase
{
    public float speed = 2f;
    public int variant;

    static readonly string[] Colors = { "orange", "black", "white", "gray" };

    void Start()
    {
        anim.SetFrames(SpriteFactory.Frames("cat_" + Colors[Mathf.Abs(variant) % Colors.Length] + "_", 2), 5f);
    }

    void FixedUpdate()
    {
        if (Dead) return;
        StepMove(speed, true);
        sr.flipX = dir > 0;
    }

    protected override void OnPlayerTouch(PlayerController2D p)
    {
        if (p.IsStompingOn(col))
        {
            p.Bounce();
            StartCoroutine(SquashAndDie());
        }
        else p.Hurt();
    }
}

/// <summary>
/// Turtle: walks; stomp it and it becomes a shell. Touch a still shell to kick it: it slides,
/// bounces off walls and knocks out every cat and turtle it hits (and hurts you if it hits you).
/// </summary>
public class Turtle : EnemyBase
{
    enum State { Walk, Shell, Sliding }

    public float speed = 1.2f;
    public bool purple;

    State state = State.Walk;
    float kickCooldown;
    Sprite[] walkFrames;
    Sprite shellSprite;

    void Start()
    {
        string c = purple ? "purple" : "green";
        walkFrames = SpriteFactory.Frames("turtle_" + c + "_", 2);
        shellSprite = SpriteFactory.Get("turtle_" + c + "_shell");
        anim.SetFrames(walkFrames, 4f);
        SetWalkCollider();
    }

    void SetWalkCollider() { col.size = new Vector2(0.95f, 0.65f); col.offset = new Vector2(0f, 0.36f); }
    void SetShellCollider() { col.size = new Vector2(0.9f, 0.5f); col.offset = new Vector2(0f, 0.28f); }

    void FixedUpdate()
    {
        if (Dead) return;
        if (kickCooldown > 0f) kickCooldown -= Time.fixedDeltaTime;

        switch (state)
        {
            case State.Walk:
                StepMove(speed, true);
                sr.flipX = dir > 0;
                break;
            case State.Shell:
                StepMove(0f, false);
                break;
            case State.Sliding:
                StepMove(8f, false);
                HitOthers();
                break;
        }
    }

    void HitOthers()
    {
        var found = Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, 0f);
        foreach (var c in found)
        {
            var e = c.GetComponent<EnemyBase>();
            if (e != null && e != this && !e.Dead) e.Kill(true);
        }
    }

    void BecomeShell()
    {
        state = State.Shell;
        anim.enabled = false;
        sr.sprite = shellSprite;
        SetShellCollider();
    }

    protected override void OnPlayerTouch(PlayerController2D p)
    {
        switch (state)
        {
            case State.Walk:
                if (p.IsStompingOn(col)) { p.Bounce(); BecomeShell(); kickCooldown = 0.2f; }
                else p.Hurt();
                break;

            case State.Shell:
                if (kickCooldown > 0f) return;
                dir = p.transform.position.x < transform.position.x ? 1 : -1;   // kick away from the player
                state = State.Sliding;
                kickCooldown = 0.3f;
                if (p.IsStompingOn(col)) p.Bounce();
                break;

            case State.Sliding:
                if (p.IsStompingOn(col)) { p.Bounce(); state = State.Shell; kickCooldown = 0.3f; }
                else if (kickCooldown <= 0f) p.Hurt();
                break;
        }
    }

    public override void FireHit()
    {
        if (state == State.Walk) Kill(true);   // shells shrug off fireballs
    }
}

/// <summary>Frog: sits, then hops toward (or away from) the player. Stomp it, or burn it with a fireball.</summary>
public class Frog : EnemyBase
{
    public float speed = 3f;

    Sprite sitting, hopping;
    float timer;

    void Start()
    {
        sitting = SpriteFactory.Get("frog_1");
        hopping = SpriteFactory.Get("frog_2");
        if (anim != null) anim.enabled = false;
        sr.sprite = sitting;
        timer = 0.5f + Random.value;
        dir = Random.value < 0.5f ? -1 : 1;
    }

    void FixedUpdate()
    {
        if (Dead) return;
        timer -= Time.fixedDeltaTime;
        if (grounded && vy <= 0f && timer <= 0f)
        {
            var lm = LevelManager.Instance;
            if (lm != null && lm.PlayerTransform != null && Mathf.Abs(lm.PlayerTransform.position.x - transform.position.x) < 9f)
                dir = lm.PlayerTransform.position.x < transform.position.x ? -1 : 1;
            vy = 9.5f;
            timer = 0.9f + Random.value * 0.9f;
        }
        bool airborne = !grounded || vy > 0f;
        StepMove(airborne ? speed : 0f, false);
        sr.sprite = (!grounded || vy > 0f) ? hopping : sitting;
    }

    protected override void OnPlayerTouch(PlayerController2D p)
    {
        if (p.IsStompingOn(col))
        {
            p.Bounce();
            StartCoroutine(SquashAndDie());
        }
        else p.Hurt();
    }
}

/// <summary>Piranha plant that pops out of a pipe. Stays hidden while the player stands next to the pipe.</summary>
public class PiranhaPlant : MonoBehaviour, IFireHittable
{
    public float pipeTop;

    SpriteRenderer sr;
    BoxCollider2D col;
    float phaseTime;
    int phase;                 // 0 hidden, 1 rising, 2 up, 3 falling
    const float Hidden = 1.6f, Rise = 0.5f, Up = 1.3f;
    const float Depth = 1.55f;
    bool dead;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
    }

    void Start()
    {
        var a = GetComponent<SpriteAnimator>();
        a.SetFrames(SpriteFactory.Frames("plant_", 2), 4f);
        phaseTime = Random.value * 1.5f;

        // The plant only shows above the pipe's rim: a sprite mask covers the space over the pipe, and the
        // plant is visible inside it only. (Fixes the plant peeking out under short pipes while hidden.)
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        maskObj = new GameObject("PlantMask");
        maskObj.transform.SetParent(transform.parent, false);
        maskObj.transform.position = new Vector3(transform.position.x, pipeTop + 2f, 0f);
        maskObj.transform.localScale = new Vector3(3f, 4f, 1f);
        var mask = maskObj.AddComponent<SpriteMask>();
        mask.sprite = MaskSprite();
        SetHeight(0f);
    }

    GameObject maskObj;
    static Sprite maskSprite;

    static Sprite MaskSprite()
    {
        if (maskSprite == null)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            maskSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);   // 1 x 1 world unit
        }
        return maskSprite;
    }

    void OnDestroy() { if (maskObj != null) Destroy(maskObj); }

    bool PlayerNear()
    {
        var lm = LevelManager.Instance;
        if (lm == null || lm.PlayerTransform == null) return false;
        Vector3 p = lm.PlayerTransform.position;
        return Mathf.Abs(p.x - transform.position.x) < 1.7f && p.y > pipeTop - 0.5f;
    }

    void SetHeight(float k)   // 0 = hidden inside the pipe, 1 = fully out
    {
        transform.position = new Vector3(transform.position.x, pipeTop - Depth + k * Depth, 0f);
        col.enabled = k > 0.45f && !dead;
        sr.enabled = k > 0.02f && !dead;
    }

    void Update()
    {
        if (dead) return;
        phaseTime += Time.deltaTime;
        switch (phase)
        {
            case 0:
                SetHeight(0f);
                if (phaseTime > Hidden && !PlayerNear()) { phase = 1; phaseTime = 0f; }
                break;
            case 1:
                SetHeight(Mathf.Clamp01(phaseTime / Rise));
                if (phaseTime > Rise) { phase = 2; phaseTime = 0f; }
                break;
            case 2:
                SetHeight(1f);
                if (phaseTime > Up) { phase = 3; phaseTime = 0f; }
                break;
            case 3:
                SetHeight(1f - Mathf.Clamp01(phaseTime / Rise));
                if (phaseTime > Rise) { phase = 0; phaseTime = 0f; }
                break;
        }
    }

    void Touch(Collider2D other)
    {
        if (dead) return;
        var p = other.GetComponent<PlayerController2D>();
        if (p != null && !p.IsDead) p.Hurt();
    }

    void OnTriggerEnter2D(Collider2D other) { Touch(other); }
    void OnTriggerStay2D(Collider2D other) { Touch(other); }

    public void FireHit()
    {
        if (dead || phase == 0) return;
        dead = true;
        col.enabled = false;
        Destroy(gameObject);
    }
}
