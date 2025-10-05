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
        
        // Ensure we have a collider for collision detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Add a small sphere collider if none exists
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.radius = 0.05f;
        }
        
        // Ensure proper collision detection for fast-moving bullets
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    void Start()
    {
        // Safety: destroy bullet body after lifeTime. The trail may keep living shortly after detach.
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Align bullet forward with its velocity so the trail follows the motion nicely
        if (rb && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            transform.forward = rb.linearVelocity.normalized;
        }
    }

    void OnCollisionEnter(Collision col)
    {
        HandleHit(col.gameObject, col.contacts[0].point);
    }

    void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject, transform.position);
    }

    private void HandleHit(GameObject hitObject, Vector3 hitPoint)
    {
        // Apply damage to hit target if it has a HealthSystem
        HealthSystem targetHealth = hitObject.GetComponent<HealthSystem>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage, "Player Bullet");
        }

        DestroyBullet();
    }

    private void DestroyBullet()
    {
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
