using System;
using UnityEngine;

/// <summary>
/// Small pixel-font name that floats above a character. The text is drawn with the built-in
/// 5x7 pixel font (PixelFont) so it stays crisp and small instead of using a big smooth font.
/// </summary>
public class NameTag : MonoBehaviour
{
    public Transform target;
    public Func<float> height;     // distance from the target's feet to the tag

    static readonly Color Fill = Color.white;
    static readonly Color Outline = new Color(0.17f, 0.15f, 0.25f, 1f);

    public static NameTag Create(Transform target, string text, Func<float> heightFn)
    {
        var go = new GameObject("NameTag");
        go.transform.SetParent(target.parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PixelFont.MakeSprite(text, Fill, Outline, 28f);   // 28 px per unit: small and neat
        sr.sortingOrder = 40;
        var tag = go.AddComponent<NameTag>();
        tag.target = target;
        tag.height = heightFn;
        return tag;
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        float h = height != null ? height() : 2f;
        transform.position = target.position + new Vector3(0f, h, 0f);
    }

    void OnDestroy()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Destroy(sr.sprite.texture);
            Destroy(sr.sprite);
        }
    }
}
