using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Lifetime / Damage")]
    public float lifeTime = 5f;           // Auto-destroy after this time
    public float damage = 10f;

    [Header("Visuals")]
    public TrailRenderer trail;           // Assign your TrailRenderer component
    public Light glowLight;               // Optional small point light

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // Safety: destroy bullet body after lifeTime. The trail may keep living shortly after detach.
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Align bullet forward with its velocity so the trail follows the motion nicely
        if (rb && rb.velocity.sqrMagnitude > 0.0001f)
            transform.forward = rb.velocity.normalized;
    }

    void OnCollisionEnter(Collision col)
    {
        // TODO: apply damage to hit target here

        // Detach the trail so it can fade out instead of being cut off instantly
        if (trail)
        {
            trail.transform.parent = null;

#if UNITY_2019_1_OR_NEWER
            // If your Unity supports it, let trail auto-destroy after it has fully faded
            trail.autodestruct = true;
#else
            // Fallback: destroy the trail after its time (plus a small buffer)
            Destroy(trail.gameObject, trail.time * 1.25f);
#endif
        }

        // Optionally detach and fade the glow light
        if (glowLight)
        {
            glowLight.transform.parent = null;
            Destroy(glowLight.gameObject, 0.2f);
        }

        Destroy(gameObject); // Destroy the bullet body immediately on impact
    }
}
