using UnityEngine;

// Small runtime-added components used by LevelManager. They live in one file for tidiness.
// (They are created with AddComponent, so they don't need to be dragged in the Editor.)

/// <summary>Collectible coin.</summary>
public class Coin : MonoBehaviour
{
    float baseY;
    float phase;

    void Start()
    {
        baseY = transform.position.y;
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3f + phase) * 0.08f;
        transform.position = p;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() == null) return;
        if (LevelManager.Instance != null) LevelManager.Instance.AddCoin();
        Destroy(gameObject);
    }
}

/// <summary>Spikes etc.: touching kills the player (respawn at level start).</summary>
public class Hazard : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other) { Touch(other); }
    void OnTriggerStay2D(Collider2D other) { Touch(other); }

    void Touch(Collider2D other)
    {
        var p = other.GetComponent<PlayerController2D>();
        if (p != null) p.Die();
    }
}

/// <summary>Patrolling walker. Stomp it from above, otherwise it hurts.</summary>
public class Enemy : MonoBehaviour
{
    public float minX, maxX, speed = 2f;

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    int dir = 1;
    bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void FixedUpdate()
    {
        if (dead) return;
        Vector2 p = rb.position;
        p.x += dir * speed * Time.fixedDeltaTime;
        if (p.x >= maxX) { p.x = maxX; dir = -1; }
        else if (p.x <= minX) { p.x = minX; dir = 1; }
        rb.MovePosition(p);
        sr.flipX = dir > 0;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (dead) return;
        var p = other.GetComponent<PlayerController2D>();
        if (p == null || p.IsDead) return;

        bool stomp = other.bounds.min.y > col.bounds.center.y && p.VerticalVelocity <= 0.5f;
        if (stomp)
        {
            dead = true;
            col.enabled = false;
            transform.localScale = new Vector3(1f, 0.3f, 1f);
            Destroy(gameObject, 0.4f);
            p.Bounce();
        }
        else
        {
            p.Die();
        }
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

/// <summary>Level 10, Player 2 only: walking close to the waiting boy starts the ending cutscene.</summary>
public class ReunionTrigger : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController2D>() == null) return;
        if (LevelManager.Instance != null) LevelManager.Instance.OnReunionTriggered();
    }
}
