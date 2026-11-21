using UnityEngine;

/// <summary>Short-lived sprite that flies up (and falls) and fades: coin pops, sparkles, hearts.</summary>
public class PopFx : MonoBehaviour
{
    public Vector2 velocity = new Vector2(0f, 6f);
    public float gravity = 18f;
    public float life = 0.7f;
    public bool fade = true;

    SpriteRenderer sr;
    float t;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        t += Time.deltaTime;
        velocity.y -= gravity * Time.deltaTime;
        transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.deltaTime;
        if (fade && sr != null)
        {
            float a = 1f - Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f));
            sr.color = new Color(1f, 1f, 1f, a);
        }
        if (t >= life) Destroy(gameObject);
    }

    public static PopFx Spawn(Transform parent, Sprite sprite, Vector3 pos, Vector2 velocity, float gravity, float life, int order, float scale = 1f)
    {
        var go = new GameObject("Fx");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        var fx = go.AddComponent<PopFx>();
        fx.velocity = velocity;
        fx.gravity = gravity;
        fx.life = life;
        return fx;
    }
}

/// <summary>
/// Banded (pixel-style) sky gradient glued to the camera. It is a 1-pixel-wide texture
/// that is stretched to fill the view, so there are no extra assets.
/// </summary>
public class SkyGradient : MonoBehaviour
{
    const int Bands = 20;
    Camera cam;

    public static GameObject Create(Camera cam, Color[] stops)
    {
        var tex = new Texture2D(1, Bands, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < Bands; i++)
        {
            float k = i / (float)(Bands - 1);                 // 0 = bottom, 1 = top
            float f = (1f - k) * (stops.Length - 1);          // stops[0] = top colour
            int a = Mathf.Min((int)f, stops.Length - 2);
            Color c = Color.Lerp(stops[a], stops[a + 1], f - a);
            tex.SetPixel(0, i, c);
        }
        tex.Apply();

        var go = new GameObject("SkyGradient");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 20f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, Bands), new Vector2(0.5f, 0.5f), Bands);   // 1 unit tall
        sr.sortingOrder = -100;
        var sky = go.AddComponent<SkyGradient>();
        sky.cam = cam;
        sky.Fit();
        return go;
    }

    void Fit()
    {
        if (cam == null) return;
        float h = cam.orthographicSize * 2f + 0.5f;
        float w = h * cam.aspect + 1f;
        transform.localScale = new Vector3(w * Bands, h, 1f);   // sprite is 1/Bands wide
    }

    void LateUpdate() { Fit(); }

    void OnDestroy()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) { Destroy(sr.sprite.texture); Destroy(sr.sprite); }
    }
}
