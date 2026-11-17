using UnityEngine;

/// <summary>Smooth side-scrolling camera clamped to the level width. Added automatically to the Main Camera.</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float smooth = 8f;
    public Vector2 offset = new Vector2(1.5f, 1.8f);
    public float minX = 0f;
    public float maxX = 100f;
    public float minY = 2.5f;

    Camera cam;

    void Awake() { cam = GetComponent<Camera>(); }

    Vector3 Desired()
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float x = target.position.x + offset.x;
        float lo = minX + halfW, hi = maxX - halfW;
        x = hi < lo ? (minX + maxX) * 0.5f : Mathf.Clamp(x, lo, hi);
        float y = Mathf.Max(minY, target.position.y + offset.y);
        return new Vector3(x, y, -10f);
    }

    /// <summary>Jump straight to the target (used when a level loads).</summary>
    public void Snap()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (target != null) transform.position = Desired();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, Desired(), t);
    }
}
