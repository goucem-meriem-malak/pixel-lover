using UnityEngine;

/// <summary>Rigidbody2D velocity that works in Unity 2021, 2022 and Unity 6.</summary>
public static class Rb2D
{
    public static Vector2 GetVel(this Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    public static void SetVel(this Rigidbody2D rb, Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}

/// <summary>Plays a looping (or one-shot) sprite animation on the same GameObject.</summary>
public class SpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 6f;
    public bool loop = true;

    SpriteRenderer sr;
    float t0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        t0 = Time.time - Random.value;
    }

    public void SetFrames(Sprite[] f, float framesPerSecond)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        frames = f;
        fps = framesPerSecond;
        if (f != null && f.Length > 0) sr.sprite = f[0];
    }

    public void Restart() { t0 = Time.time; }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)((Time.time - t0) * fps);
        i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
        sr.sprite = frames[i];
    }
}

public enum PickupKind { Coin, Flower }

/// <summary>Coin (boy) or flower (girl). Touching it collects it.</summary>
public class Pickup : MonoBehaviour
{
    public PickupKind kind;
    float baseY, phase;

    void Start()
    {
        baseY = transform.position.y;
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3f + phase) * 0.07f;
        transform.position = p;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() == null) return;
        if (LevelManager.Instance != null) LevelManager.Instance.Collect(kind, transform.position);
        Destroy(gameObject);
    }
}

/// <summary>Invisible trigger at the flag: level complete.</summary>
public class LevelGoal : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() == null) return;
        if (LevelManager.Instance != null) LevelManager.Instance.OnGoalReached();
    }
}

/// <summary>Level 10, Player 2 only: walking close to the waiting boy starts the proposal cutscene.</summary>
public class ReunionTrigger : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() == null) return;
        if (LevelManager.Instance != null) LevelManager.Instance.OnReunionTriggered();
    }
}

/// <summary>Sparkle that pulses in size (girl world decoration).</summary>
public class Twinkle : MonoBehaviour
{
    public float speed = 3f;
    public float baseScale = 1f;
    float phase;

    void Start() { phase = Random.value * 6.28f; }

    void Update()
    {
        float s = baseScale * (0.65f + 0.45f * Mathf.Sin(Time.time * speed + phase));
        transform.localScale = new Vector3(s, s, 1f);
    }
}

/// <summary>
/// Background layer that scrolls slower than the camera. Its children repeat every 'period' world units,
/// so the layer can jump back by one period without anybody noticing.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public Transform cam;
    public float scroll = 0.3f;     // 0 = glued to the camera, 1 = moves with the world
    public float period = 20f;
    public float yOffset = -5f;     // relative to the camera (when lockWorldY is off)
    public bool lockWorldY = true;  // keep the layer at a fixed height in the world
    public float worldY = 0f;

    void LateUpdate()
    {
        if (cam == null) return;
        float cx = cam.position.x;
        float x = cx - Mathf.Repeat(cx * scroll, period);
        float y = lockWorldY ? worldY : cam.position.y + yOffset;
        transform.position = new Vector3(x, y, 0f);
    }
}
