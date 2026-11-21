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
    [Tooltip("Snap the camera to the pixel grid so pixel art never shimmers.")]
    public bool pixelSnap = true;

    Camera cam;
    Vector3 smoothPos;      // unsnapped position, so snapping never stalls the smoothing

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
        if (target != null) { smoothPos = Desired(); transform.position = smoothPos; }
    }

    void LateUpdate()
    {
        if (target == null) return;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        smoothPos = Vector3.Lerp(smoothPos, Desired(), t);
        Vector3 p = smoothPos;
        if (pixelSnap)
        {
            p.x = Mathf.Round(p.x * 16f) / 16f;   // the art is 16 pixels per unit
            p.y = Mathf.Round(p.y * 16f) / 16f;
        }
        transform.position = p;
    }
}
