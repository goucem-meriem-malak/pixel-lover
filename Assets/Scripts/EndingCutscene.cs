using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Level-10 proposal (Player 2 / girl). Started automatically by LevelManager when she walks up to the
/// waiting knight:
///   she walks to him -> he takes off... stands up (helmet off) and hops -> steps toward her ->
///   KNEELS ON ONE KNEE and holds out an open ring box with the ring he bought ->
///   "HAPPY BIRTHDAY LOVE!" -> she hops and says THANK YOU -> the ring flies to her, hearts burst ->
///   they step together, lean in for the kiss, big heart -> "TOGETHER FOREVER" with both names.
/// Added to the LevelManager object automatically. Everything is sprite + coroutine based.
/// </summary>
public class EndingCutscene : MonoBehaviour
{
    [Header("Text")]
    public string endingMessage = "TOGETHER FOREVER";

    [Header("Timing")]
    public float standDistance = 2.4f;      // how far from the boy the girl stops
    public float endHoldSeconds = 4f;

    public Action onFinished;
    public bool IsPlaying { get; private set; }

    Transform root;

    public void Abort()
    {
        StopAllCoroutines();
        IsPlaying = false;
    }

    public void Play(PlayerController2D girl, GameObject boyNpc, string boyName, string girlName, int ringId)
    {
        if (IsPlaying) return;
        StartCoroutine(Run(girl, boyNpc, boyName, girlName, ringId));
    }

    IEnumerator Run(PlayerController2D girl, GameObject boy, string boyName, string girlName, int ringId)
    {
        IsPlaying = true;
        var lm = LevelManager.Instance;
        var ui = UIManager.Instance;
        root = lm.LevelRoot;
        var boySr = boy.GetComponent<SpriteRenderer>();
        Transform girlVisual = girl.transform.Find("Visual");
        const float girlScale = 1f;

        TouchInput.Reset();
        ui.HideTouchControls();
        girl.Invincible = true;

        // 1) She walks up to him
        yield return girl.WalkTo(boy.transform.position.x - standDistance);
        girl.FreezeForCutscene();
        girl.Face(1);

        Camera cam = Camera.main;
        if (cam != null) StartCoroutine(Zoom(cam, cam.orthographicSize, cam.orthographicSize * 0.8f, 1.5f));
        yield return new WaitForSeconds(0.6f);

        // 2) He jumps up (helmet off), then steps toward her
        boySr.sprite = SpriteFactory.Get("knight_bare_idle");
        boySr.flipX = true;
        Hearts(girl.transform.position + new Vector3(0f, 2.2f * girlScale, 0f), 1);
        yield return Hop(boy.transform, 0.5f, 0.35f);
        yield return new WaitForSeconds(0.3f);
        yield return MoveX(boy.transform, boy.transform.position.x - 0.7f, 0.6f);

        // 3) He kneels on one knee and holds out the open ring box
        boySr.sprite = SpriteFactory.Get("knight_bare_kneel");
        yield return new WaitForSeconds(0.4f);

        Vector3 palm = boy.transform.position + new Vector3(-0.5f, 0.69f, 0f);   // the open hand of the kneeling sprite
        GameObject box = MakeSprite("RingBox", SpriteFactory.Get("ringbox_open"), palm, 21);
        GameObject ring = MakeSprite("Ring", Rings.Icon(ringId), palm + new Vector3(0f, 0.45f, 0f), 22);
        box.transform.localScale = Vector3.zero;
        ring.transform.localScale = Vector3.zero;
        StartCoroutine(Sparkles(ring.transform));
        yield return Scale(box.transform, 0f, 1f, 0.3f);
        yield return Scale(ring.transform, 0f, 1f, 0.3f);

        // 4) The question
        GameObject ask = Bubble(boy.transform.position + new Vector3(-0.8f, 2.6f, 0f),
                                "HAPPY BIRTHDAY LOVE!");
        yield return new WaitForSeconds(2.8f);
        DestroyBubble(ask);

        // 5) She says yes
        Vector3 girlHead = girl.transform.position + new Vector3(0f, 2.4f * girlScale, 0f);
        GameObject yes = Bubble(girlHead + new Vector3(0f, 0.4f, 0f), "THANK YOU!");
        yield return Hop(girl.transform, 0.45f, 0.3f);
        Hearts(girl.transform.position + new Vector3(0f, 1.2f, 0f), 8);
        yield return Hop(girl.transform, 0.35f, 0.25f);
        yield return new WaitForSeconds(0.9f);
        DestroyBubble(yes);

        // 6) The ring flies to her, he stands up
        Vector3 fromRing = ring.transform.position;
        Vector3 toRing = girl.transform.position + new Vector3(0.25f, 1.0f * girlScale, 0f);
        yield return Move(ring.transform, fromRing, toRing, 0.7f);
        Destroy(box);
        yield return new WaitForSeconds(0.3f);
        Hearts(toRing, 6);
        boySr.sprite = SpriteFactory.Get("knight_bare_idle");
        yield return Hop(boy.transform, 0.4f, 0.3f);
        Destroy(ring);

        // 7) They step together, lean in for the kiss, big heart
        float midX = (girl.transform.position.x + boy.transform.position.x) * 0.5f;
        float gy = girl.transform.position.y, by = boy.transform.position.y;
        yield return MoveBoth(girl.transform, new Vector3(midX - 0.45f, gy, 0f),
                              boy.transform, new Vector3(midX + 0.45f, by, 0f), 0.8f);
        if (girlVisual != null) girlVisual.localRotation = Quaternion.Euler(0f, 0f, -8f);
        boy.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        Vector3 kissPos = new Vector3(midX, Mathf.Max(gy, by) + 2.4f * girlScale, 0f);
        GameObject big = MakeSprite("KissHeart", SpriteFactory.Get("heart"), kissPos, 22);
        big.transform.localScale = Vector3.zero;
        yield return Scale(big.transform, 0f, 3f, 0.45f);
        Hearts(kissPos, 12);
        ui.ShowEndingMessage(endingMessage, NameUtil.Clean(boyName, "KNIGHT").ToUpperInvariant() + " ^ " + NameUtil.Clean(girlName, "PRINCESS").ToUpperInvariant());

        float t = 0f;
        while (t < endHoldSeconds)
        {
            t += Time.deltaTime;
            float s = 3f + 0.3f * Mathf.Sin(t * 6f);
            big.transform.localScale = new Vector3(s, s, 1f);
            if (UnityEngine.Random.value < 0.05f) Hearts(kissPos + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, 0f), 1);
            yield return null;
        }

        IsPlaying = false;
        lm.OnEndingFinished();
        if (onFinished != null) onFinished();
    }

    // ------------------------------------------------------------------ helpers

    GameObject MakeSprite(string name, Sprite sprite, Vector3 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    /// <summary>Little hearts that float up and fade.</summary>
    void Hearts(Vector3 centre, int count)
    {
        Sprite heart = SpriteFactory.Get("heart");
        for (int i = 0; i < count; i++)
        {
            Vector3 p = centre + new Vector3(UnityEngine.Random.Range(-0.7f, 0.7f), UnityEngine.Random.Range(-0.3f, 0.3f), 0f);
            PopFx.Spawn(root, heart, p, new Vector2(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(1.2f, 2.4f)),
                        0f, UnityEngine.Random.Range(1.0f, 1.6f), 23, UnityEngine.Random.Range(0.9f, 1.6f));
        }
    }

    IEnumerator Sparkles(Transform around)
    {
        while (around != null)
        {
            Sprite s = SpriteFactory.Get(UnityEngine.Random.value < 0.5f ? "sparkle_1" : "sparkle_2");
            Vector3 p = around.position + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.3f, 0.5f), 0f);
            PopFx.Spawn(root, s, p, new Vector2(0f, 0.6f), 0f, 0.45f, 24);
            yield return new WaitForSeconds(0.18f);
        }
    }

    /// <summary>Speech bubble drawn with the small pixel font: dark text on a white box with a dark border and a tail.</summary>
    GameObject Bubble(Vector3 pos, string text)
    {
        const float ppu = 26f;
        Color ink = new Color(0.17f, 0.15f, 0.25f, 1f);
        Sprite txt = PixelFont.MakeSprite(text, ink, new Color(0f, 0f, 0f, 0f), ppu);
        float w = txt.rect.width / ppu, h = txt.rect.height / ppu;

        var holder = new GameObject("Bubble");
        holder.transform.SetParent(root, false);
        holder.transform.position = pos;

        Child(holder.transform, SpriteFactory.Square(ink), Vector3.zero, new Vector2(w + 0.6f, h + 0.6f), 30, 0f);
        Child(holder.transform, SpriteFactory.Square(Color.white), Vector3.zero, new Vector2(w + 0.4f, h + 0.4f), 31, 0f);
        float ty = -(h + 0.4f) * 0.5f;
        Child(holder.transform, SpriteFactory.Square(ink), new Vector3(0f, ty, 0f), new Vector2(0.32f, 0.32f), 30, 45f);
        Child(holder.transform, SpriteFactory.Square(Color.white), new Vector3(0f, ty + 0.05f, 0f), new Vector2(0.24f, 0.24f), 31, 45f);
        Child(holder.transform, txt, Vector3.zero, Vector2.one, 32, 0f);
        return holder;
    }

    static void Child(Transform parent, Sprite sprite, Vector3 localPos, Vector2 scale, int order, float rotZ)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
    }

    static void DestroyBubble(GameObject bubble)
    {
        if (bubble == null) return;
        foreach (var sr in bubble.GetComponentsInChildren<SpriteRenderer>())
        {
            // only the text sprite owns a texture of its own (the rest are cached squares)
            if (sr.sprite != null && sr.sprite.texture != null && sr.sprite.texture.width > 1)
            {
                Destroy(sr.sprite.texture);
                Destroy(sr.sprite);
            }
        }
        Destroy(bubble);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    static IEnumerator Hop(Transform tr, float height, float duration)
    {
        Vector3 p0 = tr.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            tr.position = p0 + new Vector3(0f, height * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI), 0f);
            yield return null;
        }
        tr.position = p0;
    }

    static IEnumerator Scale(Transform tr, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.LerpUnclamped(from, to, EaseOutBack(Mathf.Clamp01(t / duration)));
            tr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        tr.localScale = new Vector3(to, to, 1f);
    }

    static IEnumerator Move(Transform tr, Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            tr.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        tr.position = to;
    }

    static IEnumerator MoveX(Transform tr, float toX, float duration)
    {
        Vector3 from = tr.position;
        Vector3 to = new Vector3(toX, from.y, from.z);
        yield return Move(tr, from, to, duration);
    }

    static IEnumerator MoveBoth(Transform a, Vector3 aTo, Transform b, Vector3 bTo, float duration)
    {
        Vector3 aFrom = a.position, bFrom = b.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            a.position = Vector3.Lerp(aFrom, aTo, k);
            b.position = Vector3.Lerp(bFrom, bTo, k);
            yield return null;
        }
        a.position = aTo;
        b.position = bTo;
    }

    static IEnumerator Zoom(Camera cam, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cam.orthographicSize = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
    }
}
