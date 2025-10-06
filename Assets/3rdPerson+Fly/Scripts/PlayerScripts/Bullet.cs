using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Lifetime / Damage")]
    public float lifeTime = 5f;
    public float damage = 10f;

    [Header("Impact VFX / SFX")]
    public GameObject impactEffect;
    public AudioClip impactSound;                 // 🔊 命中音效
    public float impactVolume = 1f;
    public bool autodestroyByParticleDuration = true;
    public float impactEffectLifetime = 2f;

    [Header("Visuals")]
    public TrailRenderer trail;
    public Light glowLight;

    private Rigidbody rb;
    private Vector3 lastVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        if (rb) lastVelocity = rb.linearVelocity;
    }

    void Update()
    {
        if (rb && rb.linearVelocity.sqrMagnitude > 0.0001f)
            transform.forward = rb.linearVelocity.normalized;
    }

    void OnCollisionEnter(Collision col)
    {
        var contact = col.GetContact(0);
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        Vector3 impactDir = (lastVelocity.sqrMagnitude > 0.0001f ? lastVelocity : transform.forward).normalized;

        ApplyDamageIfAny(col.collider, hitPoint, hitNormal, impactDir);
        CleanupAndDestroy();
    }

    void OnTriggerEnter(Collider other)
    {
        ShieldBarrier shieldBarrier = other.GetComponent<ShieldBarrier>();
        if (shieldBarrier != null)
        {
            if (shieldBarrier.ShouldBlockBullet(gameObject))
            {
                ApplyDamageIfAny(other, transform.position, -transform.forward, transform.forward);
                CleanupAndDestroy();
                return;
            }
            else
                return;
        }

        ShieldDamageZone damageZone = other.GetComponent<ShieldDamageZone>();
        if (damageZone != null)
            return;

        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = -transform.forward;
        Vector3 impactDir = (lastVelocity.sqrMagnitude > 0.0001f ? lastVelocity : transform.forward).normalized;

        ApplyDamageIfAny(other, hitPoint, hitNormal, impactDir);
        CleanupAndDestroy();
    }

    private void ApplyDamageIfAny(Collider hitCol, Vector3 hitPoint, Vector3 hitNormal, Vector3 impactDir)
    {
        // Drone
        var drone = hitCol.GetComponentInParent<DroneHealth>();
        if (drone != null) drone.TakeDamage(damage);

        // Turret
        var turret = hitCol.GetComponentInParent<FortHealth>();
        if (turret != null) turret.TakeDamage(damage);

        // Fort
        var fort = hitCol.GetComponentInParent<FortHealth>();
        if (fort != null) fort.TakeDamage(damage);

        // 命中特效（沿弹道方向）
        if (impactEffect)
        {
            var rot = Quaternion.LookRotation(impactDir, hitNormal);
            var fx = Instantiate(impactEffect, hitPoint, rot);

            if (autodestroyByParticleDuration)
            {
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps != null)
                    Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
                else
                    Destroy(fx, impactEffectLifetime);
            }
            else
                Destroy(fx, impactEffectLifetime);
        }

        // ✅ 命中音效（在命中点播放一次）
        if (impactSound)
        {
            AudioSource.PlayClipAtPoint(impactSound, hitPoint, impactVolume);
        }
    }

    private void CleanupAndDestroy()
    {
        if (trail)
        {
            trail.transform.parent = null;
#if UNITY_2019_1_OR_NEWER
            trail.autodestruct = true;
#else
            Destroy(trail.gameObject, trail.time * 1.25f);
#endif
        }

        if (glowLight)
        {
            glowLight.transform.parent = null;
            Destroy(glowLight.gameObject, 0.2f);
        }

        Destroy(gameObject);
    }
}
