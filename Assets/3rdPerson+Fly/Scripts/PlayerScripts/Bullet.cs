using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Lifetime / Damage")]
    public float lifeTime = 5f;
    public float damage = 10f;

    [Header("Impact VFX (optional)")]
    public GameObject impactEffect;
    public bool autodestroyByParticleDuration = true;
    public float impactEffectLifetime = 2f;

    [Header("Visuals")]
    public TrailRenderer trail;
    public Light glowLight;

    private Rigidbody rb;
    private Vector3 lastVelocity; // 缓存上一个物理步的速度

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
        // 用速度对齐子弹朝向，便于拖尾跟随
        if (rb && rb.linearVelocity.sqrMagnitude > 0.0001f)
            transform.forward = rb.linearVelocity.normalized;
    }

    // --- Physics: solid colliders ---
    void OnCollisionEnter(Collision col)
    {
        var contact = col.GetContact(0);
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        // 冲击方向：沿弹道（命中瞬间的飞行方向）
        Vector3 impactDir = (lastVelocity.sqrMagnitude > 0.0001f ? lastVelocity : transform.forward).normalized;

        ApplyDamageIfAny(col.collider, hitPoint, hitNormal, impactDir);
        CleanupAndDestroy();
    }

    // --- Physics: trigger colliders ---
    void OnTriggerEnter(Collider other)
    {
        // 盾牌屏蔽判定
        ShieldBarrier shieldBarrier = other.GetComponent<ShieldBarrier>();
        if (shieldBarrier != null)
        {
            if (shieldBarrier.ShouldBlockBullet(gameObject))
            {
                ApplyDamageIfAny(other, transform.position, -transform.forward, -transform.forward);
                CleanupAndDestroy();
                return;
            }
            else
            {
                // 放行——不要对屏蔽体本身结算伤害
                return;
            }
        }

        // 伤害区忽略
        ShieldDamageZone damageZone = other.GetComponent<ShieldDamageZone>();
        if (damageZone != null)
        {
            return;
        }

        // 触发体没有接触面法线，使用子弹反向近似为“命中法线”，但朝向仍沿弹道
        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = -transform.forward;
        Vector3 impactDir = (lastVelocity.sqrMagnitude > 0.0001f ? lastVelocity : transform.forward).normalized;

        ApplyDamageIfAny(other, hitPoint, hitNormal, impactDir);
        CleanupAndDestroy();
    }

    // 增加 impactDir 参数：用它决定特效朝向
    private void ApplyDamageIfAny(Collider hitCol, Vector3 hitPoint, Vector3 hitNormal, Vector3 impactDir)
    {
        var health = hitCol.GetComponentInParent<DroneHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }

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
            {
                Destroy(fx, impactEffectLifetime);
            }
        }
    }

    // Detach visuals and destroy bullet body
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
