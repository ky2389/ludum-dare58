using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;               // turret root or head
    public float height = 2.0f;            // how high above the target
    public Vector3 extraOffset;            // optional extra world offset

    [Header("Orientation")]
    public bool faceCamera = true;         // billboard toward camera
    public bool keepUpright = true;        // keep world-up (no roll)

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (!target) return;
        if (!cam) cam = Camera.main;

        // 1) World-space position above the target (not affected by target rotation)
        transform.position = target.position + Vector3.up * height + extraOffset;

        // 2) Orientation
        if (faceCamera && cam)
        {
            // Look at camera; keep world up to avoid banking
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir, keepUpright ? Vector3.up : cam.transform.up);
        }
        else if (keepUpright)
        {
            // Stay upright in world (zero rotation)
            transform.rotation = Quaternion.identity;
        }
        // else: keep current rotation if you prefer
    }
}
