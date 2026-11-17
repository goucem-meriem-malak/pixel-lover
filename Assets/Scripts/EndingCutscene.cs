using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Level-10 reunion (Player 2 / girl). Started automatically by LevelManager when she walks up
/// to the sitting boy:
///   girl walks to him -> boy stands up (hop) -> bouquet pops up and is offered ->
///   hearts float up -> they step together and "kiss" (lean + big heart pop + heart burst) ->
///   "Together Forever" text -> "The End" screen.
/// Added to the LevelManager object automatically. Everything is sprite/coroutine based.
/// </summary>
public class EndingCutscene : MonoBehaviour
{
    [Header("Text")]
    public string endingMessage = "Together Forever";
    [Tooltip("Append the heart emoji glyph to the text. Off by default because Unity's default font may not " +
             "contain it on every device; a heart SPRITE is shown beside the text instead.")]
    public bool useEmojiGlyph = false;

    [Header("Timing")]
    public float standDistance = 1.6f;      // how far from the boy the girl stops
    public float endHoldSeconds = 3f;

    public Action onFinished;
    public bool IsPlaying { get; private set; }

    public void Abort()
    {
        StopAllCoroutines();
        IsPlaying = false;
    }

    public void Play(PlayerController2D girl, GameObject boyNpc)
    {
        if (IsPlaying) return;
        StartCoroutine(Run(girl, boyNpc));
    }

    IEnumerator Run(PlayerController2D girl, GameObject boy)
    {
        IsPlaying = true;
        var lm = LevelManager.Instance;
        var ui = UIManager.Instance;
        Transform root = lm.LevelRoot;
        var boySr = boy.GetComponentInChildren<SpriteRenderer>();
        Transform girlVisual = girl.VisualTransform;

        TouchInput.Reset();
        ui.HideTouchControls();
        girl.Invincible = true;

        // 1) She walks up to him
        yield return girl.WalkTo(boy.transform.position.x - standDistance);
        girl.FreezeForCutscene();
        girl.Face(1);
        yield return new WaitForSeconds(0.5f);

        // 2) He stands up, turns to her, little happy hop
        boySr.sprite = lm.BoyStandingSprite;
        boySr.flipX = true;
        yield return Hop(boy.transform, 0.35f, 0.3f);
        yield return new WaitForSeconds(0.3f);

        // 3) Bouquet pops up in his hands and is offered to her
        GameObject bouquet = MakeSprite("Bouquet", lm.BouquetSprite, root, boy.transform.position + new Vector3(-0.45f, 0.5f, 0f), 21);
        bouquet.transform.localScale = Vector3.zero;
        yield return Scale(bouquet.transform, 0f, 1.1f, 0.35f);
        yield return new WaitForSeconds(0.3f);
        Vector3 offerPos = girl.transform.position + new Vector3(0.55f, 0.55f, 0f);
        yield return Move(bouquet.transform, bouquet.transform.position, offerPos, 0.6f);
        yield return Hop(girl.transform, 0.25f, 0.25f);

        // 4) Hearts rise between them
        Vector3 mid = (boy.transform.position + girl.transform.position) * 0.5f + new Vector3(0f, 1.2f, 0f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = mid + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.2f, 0.2f), 0f);
            StartCoroutine(FloatHeart(root, lm.HeartSprite, p, UnityEngine.Random.Range(0.35f, 0.6f), 1.6f));
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.4f);

        // 5) They step together; camera zooms in a little
        Camera cam = Camera.main;
        if (cam != null) StartCoroutine(Zoom(cam, cam.orthographicSize, 4.2f, 1.2f));

        float midX = (girl.transform.position.x + boy.transform.position.x) * 0.5f;
        float gy = girl.transform.position.y, by = boy.transform.position.y;
        yield return MoveBoth(girl.transform, new Vector3(midX - 0.42f, gy, 0f),
                              boy.transform, new Vector3(midX + 0.42f, by, 0f), 0.8f);
        bouquet.transform.position = girl.transform.position + new Vector3(0.1f, 0.5f, 0f);

        // 6) Kiss: lean toward each other, big heart pops, hearts burst
        girlVisual.localRotation = Quaternion.Euler(0f, 0f, -9f);
        boySr.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
        Vector3 kissPos = new Vector3(midX, Mathf.Max(gy, by) + 1.15f, 0f);
        GameObject big = MakeSprite("KissHeart", lm.HeartSprite, root, kissPos, 22);
        big.transform.localScale = Vector3.zero;
        yield return Scale(big.transform, 0f, 1.8f, 0.45f);
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            StartCoroutine(FlyHeart(root, lm.HeartSprite, kissPos, dir * UnityEngine.Random.Range(1.5f, 3f), 0.35f, 1.2f));
        }
        string msg = useEmojiGlyph ? endingMessage + " ❤️" : endingMessage;
        ui.ShowEndingMessage(msg, !useEmojiGlyph);

        // keep the big heart pulsing while the message shows
        float t = 0f;
        while (t < endHoldSeconds)
        {
            t += Time.deltaTime;
            float s = 1.8f + 0.15f * Mathf.Sin(t * 6f);
            big.transform.localScale = new Vector3(s, s, 1f);
            if (UnityEngine.Random.value < 0.06f)
                StartCoroutine(FloatHeart(root, lm.HeartSprite, kissPos + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0.3f, 0f), 0.4f, 1.4f));
            yield return null;
        }

        IsPlaying = false;
        lm.OnEndingFinished();
        if (onFinished != null) onFinished();
    }

    // ------------------------------------------------------------------ helpers

    static GameObject MakeSprite(string name, Sprite sprite, Transform parent, Vector3 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
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

    /// <summary>Heart that drifts upward, wobbles and fades out.</summary>
    static IEnumerator FloatHeart(Transform parent, Sprite sprite, Vector3 start, float scale, float life)
    {
        var go = MakeSprite("Heart", sprite, parent, start, 23);
        var sr = go.GetComponent<SpriteRenderer>();
        float t = 0f;
        float wobble = UnityEngine.Random.value * 6f;
        while (t < life && go != null)
        {
            t += Time.deltaTime;
            float k = t / life;
            go.transform.position = start + new Vector3(Mathf.Sin(t * 4f + wobble) * 0.25f, k * 1.8f, 0f);
            float s = scale * (0.6f + 0.4f * Mathf.Min(1f, k * 5f));
            go.transform.localScale = new Vector3(s, s, 1f);
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>Heart that flies out along a vector and fades (used for the kiss burst).</summary>
    static IEnumerator FlyHeart(Transform parent, Sprite sprite, Vector3 start, Vector3 velocity, float scale, float life)
    {
        var go = MakeSprite("Heart", sprite, parent, start, 23);
        var sr = go.GetComponent<SpriteRenderer>();
        go.transform.localScale = new Vector3(scale, scale, 1f);
        float t = 0f;
        while (t < life && go != null)
        {
            t += Time.deltaTime;
            float k = t / life;
            go.transform.position = start + velocity * (1f - (1f - k) * (1f - k));
            sr.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
