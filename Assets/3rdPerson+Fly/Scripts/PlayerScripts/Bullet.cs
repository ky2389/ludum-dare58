using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Lifetime / Damage")]
    public float lifeTime = 5f;           // Auto-destroy after this time
    public float damage = 10f;

    [Header("Impact VFX (optional)")]
    public GameObject impactEffect;       // Optional impact effect prefab
    public bool autodestroyByParticleDuration = true;
    public float impactEffectLifetime = 2f; // Fallback seconds if no ParticleSystem

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
        if (rb && rb.linearVelocity.sqrMagnitude > 0.0001f)
            transform.forward = rb.linearVelocity.normalized;
    }

    // --- Physics: solid colliders ---
    void OnCollisionEnter(Collision col)
    {
        ApplyDamageIfAny(col.collider, col.GetContact(0).point, col.GetContact(0).normal);
        CleanupAndDestroy();
    }

    // --- Physics: trigger colliders (in case your target uses triggers) ---
    void OnTriggerEnter(Collider other)
    {
        // Check for shield barrier first
        ShieldBarrier shieldBarrier = other.GetComponent<ShieldBarrier>();
        if (shieldBarrier != null)
        {
            // Let the shield barrier decide if this bullet should be blocked
            if (shieldBarrier.ShouldBlockBullet(gameObject))
            {
                // Bullet is blocked by shield - destroy it
                Debug.Log("[Bullet] Blocked by shield barrier - destroying");
                CleanupAndDestroy();
                return;
            }
            else
            {
                // Bullet passes through shield - don't destroy it, don't apply damage
                Debug.Log("[Bullet] Passing through shield barrier - continuing");
                return;
            }
        }
        
        // Check for shield damage zone (we don't want bullets to be destroyed by damage zones)
        ShieldDamageZone damageZone = other.GetComponent<ShieldDamageZone>();
        if (damageZone != null)
        {
            // Ignore damage zones - bullets should pass through them
            Debug.Log("[Bullet] Passing through shield damage zone - ignoring");
            return;
        }
        
        // Use bullet position as fallback impact point for trigger hits
        Debug.Log($"[Bullet] Hit trigger: {other.name} - applying damage and destroying");
        ApplyDamageIfAny(other, transform.position, -transform.forward);
        CleanupAndDestroy();
    }

    // Try to apply damage to a DroneHealth (or any Health-like component you add later)
    private void ApplyDamageIfAny(Collider hitCol, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Look up a health component on the hit object or its parents
        var health = hitCol.GetComponentInParent<DroneHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        // Optional impact effect
        if (impactEffect)
        {
            var fx = Instantiate(impactEffect, hitPoint, Quaternion.LookRotation(hitNormal));
            if (autodestroyByParticleDuration)
            {
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps != null)
                    Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
                else
                    Destroy(fx, impactEffectLifetime);
            }
            else
            {
                Destroy(fx, impactEffectLifetime);
            }
        }
    }

    // Detach visuals and destroy bullet body
    private void CleanupAndDestroy()
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

        // Destroy the bullet body immediately on impact
        Destroy(gameObject);
    }
}
