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

    public static void PressJump() { JumpHeld = true; jumpPressed = true; }
    public static void ReleaseJump() { JumpHeld = false; }
    public static bool ConsumeJumpPressed() { bool v = jumpPressed; jumpPressed = false; return v; }
    public static void Reset() { Left = Right = JumpHeld = jumpPressed = false; }
}

/// <summary>
/// Side-scroller controller: Rigidbody2D + BoxCollider2D, ground check with linecasts,
/// coyote time, jump buffering, variable jump height. Works with touch buttons and
/// (for testing in the Editor) the keyboard: A/D or arrows + Space/W/Up.
/// The Rigidbody2D and BoxCollider2D are added/configured automatically.
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

    [Header("Collider (auto-configured when enabled)")]
    public bool autoConfigureCollider = true;
    public Vector2 colliderSize = new Vector2(0.55f, 0.95f);

    [Header("Misc")]
    public float killY = -9f;

    public bool ControlEnabled { get; set; }
    public bool Invincible { get; set; }
    public bool IsDead { get; private set; }
    public bool IsGrounded { get; private set; }
    public float VerticalVelocity { get { return Vel.y; } }
    public Transform VisualTransform { get { return visual != null ? visual.transform : transform; } }

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer visual;
    Sprite standSprite, sitSprite;
    ContactFilter2D filter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[4];

    float inputX, externalMove;
    bool jumpHeld, jumpHeldPrev, jumping, sitting;
    float jumpBufferCounter, coyoteCounter, invulnTimer;
    int facing = 1;
    Coroutine deathRoutine;

    Vector2 Vel
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
    }

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

        if (autoConfigureCollider)
        {
            col.size = colliderSize;
            col.offset = new Vector2(0f, colliderSize.y * 0.5f);
        }
        // Zero friction so the player never sticks to walls / tile seams
        col.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        visual = GetComponentInChildren<SpriteRenderer>();
        if (visual == null)
        {
            var v = new GameObject("Visual");
            v.transform.SetParent(transform, false);
            visual = v.AddComponent<SpriteRenderer>();
            visual.sortingOrder = 10;
        }

        filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = false;
        filter.useDepth = false;
        filter.useNormalAngle = false;
    }

    /// <summary>Set the standing / sitting sprites (called by LevelManager).</summary>
    public void SetLook(Sprite stand, Sprite sit)
    {
        standSprite = stand;
        sitSprite = sit;
        if (visual != null && stand != null) visual.sprite = stand;
    }

    // ------------------------------------------------------------------ input + timers

    void Update()
    {
        if (IsDead) return;

        float x = 0f;
        bool pressed = false;
        if (ControlEnabled)
        {
            x = (TouchInput.Right ? 1f : 0f) - (TouchInput.Left ? 1f : 0f);
            pressed = TouchInput.ConsumeJumpPressed();
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
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            x += Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump")) pressed = true;
            jumpHeld |= Input.GetButton("Jump");
#endif
        }
        else
        {
            TouchInput.ConsumeJumpPressed();
            jumpHeld = false;
        }

        inputX = Mathf.Clamp(x, -1f, 1f);

        if (pressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            visual.enabled = invulnTimer <= 0f || ((int)(Time.time * 20f) % 2 == 0);
        }

        if (transform.position.y < killY) Die();

        Animate();
    }

    // ------------------------------------------------------------------ physics

    void FixedUpdate()
    {
        if (IsDead) return;

        bool grounded = CheckGround();
        IsGrounded = grounded;
        Vector2 v = Vel;

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

        Vel = v;
    }

    /// <summary>Three downward linecasts from the feet (left, centre, right).</summary>
    bool CheckGround()
    {
        if (Vel.y > 0.1f) return false; // moving up (also avoids "grounding" inside one-way platforms)

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

    // ------------------------------------------------------------------ visuals

    void Animate()
    {
        var t = visual.transform;
        if (sitting) return;

        visual.flipX = facing < 0;
        float sx = 1f, sy = 1f;
        if (!IsGrounded)
        {
            sy = 1f + Mathf.Clamp(Vel.y * 0.012f, -0.08f, 0.12f);
            sx = 2f - sy;
        }
        else if (Mathf.Abs(Vel.x) > 0.1f)
        {
            sy = 1f + 0.05f * Mathf.Sin(Time.time * 22f);
            sx = 2f - sy;
        }
        t.localScale = new Vector3(sx, sy, 1f);
    }

    // ------------------------------------------------------------------ gameplay API

    public void Bounce()
    {
        Vel = new Vector2(Vel.x, 9f);
        jumping = false;
    }

    public void Die()
    {
        if (IsDead || Invincible || invulnTimer > 0f) return;
        IsDead = true;
        ControlEnabled = false;
        Vel = Vector2.zero;
        rb.simulated = false;
        deathRoutine = StartCoroutine(DeathAnim());
        if (LevelManager.Instance != null) LevelManager.Instance.OnPlayerDied();
    }

    IEnumerator DeathAnim()
    {
        visual.enabled = true;
        visual.color = new Color(1f, 0.55f, 0.55f);
        Vector3 baseLocal = visual.transform.localPosition;
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            visual.transform.localPosition = baseLocal + new Vector3(0f, 7f * t - 12f * t * t, 0f);
            visual.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
            yield return null;
        }
    }

    public void Respawn(Vector3 pos)
    {
        if (deathRoutine != null) StopCoroutine(deathRoutine);
        transform.position = pos;
        rb.position = pos;
        rb.simulated = true;
        Vel = Vector2.zero;
        var t = visual.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        visual.color = Color.white;
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
        Vel = new Vector2(0f, Vel.y);
    }

    public void Face(int dir) { facing = dir >= 0 ? 1 : -1; visual.flipX = facing < 0; }

    /// <summary>Boy at the end of level 10: stop and sit down next to the flag.</summary>
    public void SitDown()
    {
        ControlEnabled = false;
        Invincible = true;
        externalMove = 0f;
        Vel = Vector2.zero;
        sitting = true;
        if (sitSprite != null) visual.sprite = sitSprite;
        visual.transform.localScale = Vector3.one;
        visual.flipX = false;
    }

    /// <summary>Freeze physics so the cutscene can move the transform directly.</summary>
    public void FreezeForCutscene()
    {
        ControlEnabled = false;
        Invincible = true;
        externalMove = 0f;
        Vel = Vector2.zero;
        rb.simulated = false;
        visual.transform.localScale = Vector3.one;
    }
}
