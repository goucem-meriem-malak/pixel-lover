using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Shared state written by the on-screen touch buttons (UIManager) and read by the player.
/// </summary>
public static class TouchInput
{
    public static bool Left;
    public static bool Right;
    public static bool JumpHeld;
    static bool jumpPressed;
    static bool firePressed;

    public static void PressJump() { JumpHeld = true; jumpPressed = true; }
    public static void ReleaseJump() { JumpHeld = false; }
    public static bool ConsumeJumpPressed() { bool v = jumpPressed; jumpPressed = false; return v; }
    public static void PressFire() { firePressed = true; }
    public static bool ConsumeFirePressed() { bool v = firePressed; firePressed = false; return v; }
    public static void Reset() { Left = Right = JumpHeld = jumpPressed = firePressed = false; }
}

/// <summary>
/// Side-scroller controller: Rigidbody2D + BoxCollider2D, ground check with linecasts, coyote time,
/// jump buffering, variable jump height. Works with touch buttons and (for testing in the Editor)
/// the keyboard: A/D or arrows, Space/W/Up to jump, F to throw a fireball.
///
/// The boy is a knight and the girl a princess. Only the girl uses power-ups:
/// Small -> Wings (wings elixir: she grows a pair of wings, no flying) -> Fire (fire flower, throws fireballs).
/// Jumping into a block from below makes it bump (OnCollisionEnter2D).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6.5f;
    public float autoWalkSpeed = 3f;
    public float jumpVelocity = 13f;
    public float gravityScale = 3.2f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;

    [Header("Ground check (linecasts)")]
    public float groundCheckDistance = 0.12f;

    [Header("Body")]
    public Vector2 bodySize = new Vector2(0.75f, 1.45f);
    public float killY = -9f;

    public PlayerRole Role { get; private set; }
    public PowerState Form { get; private set; }
    public PlayerPose Pose { get; private set; }
    public bool HasWings { get { return Form != PowerState.Small; } }
    public bool ControlEnabled { get; set; }
    public bool Invincible { get; set; }
    public bool IsDead { get; private set; }
    public bool IsGrounded { get; private set; }
    public float VerticalVelocity { get { return rb.GetVel().y; } }
    public Collider2D BodyCollider { get { return col; } }
    public int Facing { get { return facing; } }

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer visual, wingsVisual;
    ContactFilter2D filter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[4];

    Sprite idle, jump, sit;
    Sprite[] walk;

    float inputX, externalMove;
    bool jumpHeld, jumpHeldPrev, jumping;
    float jumpBufferCounter, coyoteCounter, invulnTimer, fireCooldown, appliedVy;
    int facing = 1;
    int lastBumpFrame = -1;
    Coroutine deathRoutine;

    void Awake()
    {
        ControlEnabled = true;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        // zero friction so the player never sticks to walls or tile seams
        col.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        var v = new GameObject("Visual");
        v.transform.SetParent(transform, false);
        visual = v.AddComponent<SpriteRenderer>();
        visual.sortingOrder = 10;

        // wings (girl only, shown after the first elixir): drawn behind her body
        var w = new GameObject("Wings");
        w.transform.SetParent(transform, false);
        w.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        wingsVisual = w.AddComponent<SpriteRenderer>();
        wingsVisual.sortingOrder = 9;
        wingsVisual.enabled = false;

        filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = false;
        filter.useDepth = false;
        filter.useNormalAngle = false;

        ApplySize();
    }

    /// <summary>Knight for the boy, princess for the girl, plus the little name above the head.</summary>
    public void Setup(PlayerRole role, string displayName)
    {
        Role = role;
        string who = role == PlayerRole.Boy ? "knight" : "princess";
        wingsVisual.sprite = SpriteFactory.Get("wings_1");
        idle = SpriteFactory.Get(who + "_idle");
        jump = SpriteFactory.Get(who + "_jump");
        walk = new[] { SpriteFactory.Get(who + "_walk1"), idle, SpriteFactory.Get(who + "_walk2"), idle };
        sit = role == PlayerRole.Boy ? SpriteFactory.Get("knight_sit") : idle;
        visual.sprite = idle;
        NameTag.Create(transform, displayName, () => 1.7f + 0.12f);
    }

    // ------------------------------------------------------------------ size / power-ups

    void ApplySize()
    {
        Vector2 s = bodySize;
        col.size = s;
        col.offset = new Vector2(0f, s.y * 0.5f);
    }

    void SetForm(PowerState f)
    {
        Form = f;
        if (wingsVisual != null) wingsVisual.enabled = f != PowerState.Small;
        visual.color = f == PowerState.Fire ? new Color(1f, 0.82f, 0.74f) : Color.white;
    }

    /// <summary>Called by a PowerUp. Boys ignore them.</summary>
    public void CollectPower(PowerKind kind)
    {
        if (Role != PlayerRole.Girl || IsDead) return;
        PowerState before = Form;
        if (Form == PowerState.Small) SetForm(PowerState.Wings);              // first elixir: wings
        else if (kind == PowerKind.Fire) SetForm(PowerState.Fire);            // next one: fireballs
        if (Form != before)
        {
            invulnTimer = Mathf.Max(invulnTimer, 0.6f);
            if (LevelManager.Instance != null) LevelManager.Instance.UpdateHud();
        }
    }

    /// <summary>Damage: Fire -> Wings -> Small -> dead.</summary>
    public void Hurt()
    {
        if (IsDead || Invincible || invulnTimer > 0f) return;
        if (Form == PowerState.Fire) { SetForm(PowerState.Wings); invulnTimer = 1.5f; }
        else if (Form == PowerState.Wings) { SetForm(PowerState.Small); invulnTimer = 1.5f; }
        else Die();
        if (LevelManager.Instance != null) LevelManager.Instance.UpdateHud();
    }

    /// <summary>True when the player is coming down onto this enemy's head.</summary>
    public bool IsStompingOn(Collider2D enemy)
    {
        return col.bounds.min.y >= enemy.bounds.center.y - 0.1f && VerticalVelocity <= 0.5f;
    }

    // ------------------------------------------------------------------ input + timers

    void Update()
    {
        if (IsDead) return;

        float x = 0f;
        bool pressed = false, fire = false;
        if (ControlEnabled)
        {
            x = (TouchInput.Right ? 1f : 0f) - (TouchInput.Left ? 1f : 0f);
            pressed = TouchInput.ConsumeJumpPressed();
            fire = TouchInput.ConsumeFirePressed();
            jumpHeld = TouchInput.JumpHeld;

#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
                bool kHeld = k.spaceKey.isPressed || k.upArrowKey.isPressed || k.wKey.isPressed;
                if (kHeld && !jumpHeldPrev) pressed = true;
                jumpHeld |= kHeld;
                jumpHeldPrev = kHeld;
                if (k.fKey.wasPressedThisFrame) fire = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            x += Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump")) pressed = true;
            jumpHeld |= Input.GetButton("Jump");
            if (Input.GetKeyDown(KeyCode.F)) fire = true;
#endif
        }
        else
        {
            TouchInput.ConsumeJumpPressed();
            TouchInput.ConsumeFirePressed();
            jumpHeld = false;
        }

        inputX = Mathf.Clamp(x, -1f, 1f);

        if (pressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        fireCooldown -= Time.deltaTime;
        if (fire && Form == PowerState.Fire && fireCooldown <= 0f && LevelManager.Instance != null)
        {
            fireCooldown = 0.35f;
            LevelManager.Instance.SpawnFireball(
                new Vector2(transform.position.x + facing * 0.7f, transform.position.y + 0.9f), facing);
        }

        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            visual.enabled = invulnTimer <= 0f || ((int)(Time.time * 20f) % 2 == 0);
            wingsVisual.enabled = Form != PowerState.Small && visual.enabled;
        }

        if (transform.position.y < killY) Die();

        Animate();
    }

    void Animate()
    {
        visual.flipX = facing < 0;
        if (Pose == PlayerPose.Sit) { visual.sprite = sit; return; }

        Vector2 v = rb.GetVel();
        if (!IsGrounded) visual.sprite = jump;
        else if (Mathf.Abs(v.x) > 0.3f) visual.sprite = walk[(int)(Time.time * 9f) % walk.Length];
        else visual.sprite = idle;
    }

    // ------------------------------------------------------------------ physics

    void FixedUpdate()
    {
        if (IsDead) return;

        bool grounded = CheckGround();
        IsGrounded = grounded;
        Vector2 v = rb.GetVel();

        float move = ControlEnabled ? inputX : externalMove;
        float speed = ControlEnabled ? moveSpeed : autoWalkSpeed;
        v.x = move * speed;
        if (move > 0.01f) facing = 1;
        else if (move < -0.01f) facing = -1;

        if (grounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.fixedDeltaTime;

        if (ControlEnabled && jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            v.y = jumpVelocity;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            jumping = true;
        }

        // variable jump height: releasing the button early cuts the jump
        if (jumping && !jumpHeld && v.y > 0.1f)
        {
            v.y *= 0.5f;
            jumping = false;
        }
        if (v.y <= 0f) jumping = false;

        appliedVy = v.y;
        rb.SetVel(v);
    }

    /// <summary>Three downward linecasts from the feet (left, centre, right).</summary>
    bool CheckGround()
    {
        if (rb.GetVel().y > 0.1f) return false;

        Bounds b = col.bounds;
        const float inset = 0.05f;
        for (int i = 0; i < 3; i++)
        {
            float x = Mathf.Lerp(b.min.x + inset, b.max.x - inset, i * 0.5f);
            Vector2 from = new Vector2(x, b.min.y + 0.05f);
            Vector2 to = new Vector2(x, b.min.y - groundCheckDistance);
            int n = Physics2D.Linecast(from, to, filter, hits);
            for (int j = 0; j < n; j++)
            {
                Collider2D c = hits[j].collider;
                if (c != null && c != col && !c.isTrigger) return true;
            }
        }
        return false;
    }

    /// <summary>Head hits a block from below: bump it (bricks, question blocks).</summary>
    void OnCollisionEnter2D(Collision2D c)
    {
        if (IsDead || appliedVy < 0.5f) return;
        var blk = c.collider.GetComponent<Block>();
        if (blk == null || lastBumpFrame == Time.frameCount) return;

        Bounds bb = c.collider.bounds, pb = col.bounds;
        bool above = bb.center.y > pb.center.y;
        bool overlapX = Mathf.Abs(bb.center.x - pb.center.x) < bb.extents.x + pb.extents.x - 0.05f;
        if (above && overlapX)
        {
            lastBumpFrame = Time.frameCount;
            blk.Hit(this);
        }
    }

    // ------------------------------------------------------------------ death / respawn

    public void Bounce()
    {
        Vector2 v = rb.GetVel();
        rb.SetVel(new Vector2(v.x, 10f));
        jumping = false;
    }

    public void Die()
    {
        if (IsDead || Invincible || invulnTimer > 0f) return;
        IsDead = true;
        ControlEnabled = false;
        rb.SetVel(Vector2.zero);
        rb.simulated = false;
        deathRoutine = StartCoroutine(DeathAnim());
        if (LevelManager.Instance != null) LevelManager.Instance.OnPlayerDied();
    }

    IEnumerator DeathAnim()
    {
        visual.enabled = true;
        visual.sprite = jump;
        wingsVisual.enabled = false;
        visual.color = new Color(1f, 0.6f, 0.6f);
        Vector3 baseLocal = visual.transform.localPosition;
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            visual.transform.localPosition = baseLocal + new Vector3(0f, 7f * t - 12f * t * t, 0f);
            yield return null;
        }
    }

    public void Respawn(Vector3 pos)
    {
        if (deathRoutine != null) StopCoroutine(deathRoutine);
        transform.position = pos;
        rb.position = pos;
        rb.simulated = true;
        rb.SetVel(Vector2.zero);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        SetForm(PowerState.Small);
        visual.enabled = true;
        IsDead = false;
        ControlEnabled = true;
        invulnTimer = 1.2f;
        jumpBufferCounter = 0f;
    }

    // ------------------------------------------------------------------ scripted movement (finale / cutscene)

    /// <summary>Walks (without player input) to targetX. Use with: yield return player.WalkTo(x);</summary>
    public IEnumerator WalkTo(float targetX)
    {
        ControlEnabled = false;
        float dir = Mathf.Sign(targetX - transform.position.x);
        while ((targetX - transform.position.x) * dir > 0.05f)
        {
            externalMove = dir;
            yield return null;
        }
        externalMove = 0f;
        Vector2 v = rb.GetVel();
        rb.SetVel(new Vector2(0f, v.y));
    }

    public void Face(int dir) { facing = dir >= 0 ? 1 : -1; visual.flipX = facing < 0; }

    /// <summary>Boy at the end of level 10: stop and sit down next to the flag.</summary>
    public void SitDown()
    {
        ControlEnabled = false;
        Invincible = true;
        externalMove = 0f;
        rb.SetVel(Vector2.zero);
        Pose = PlayerPose.Sit;
        facing = 1;
        visual.flipX = false;
        visual.sprite = sit;
    }

    /// <summary>Freeze physics so the cutscene can move the transform directly.</summary>
    public void FreezeForCutscene()
    {
        ControlEnabled = false;
        Invincible = true;
        externalMove = 0f;
        rb.SetVel(Vector2.zero);
        rb.simulated = false;
        visual.sprite = idle;
        IsGrounded = true;
    }
}
