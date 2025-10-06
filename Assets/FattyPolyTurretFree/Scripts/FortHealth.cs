using UnityEngine;
using UnityEngine.UI;

public class FortHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 500f;                 // Fort 通常更耐打
    public Slider hpSlider;                    // 世界空间 Canvas 的 Slider
    public bool hideBarWhenFull = true;        // 满血时隐藏

    [Header("Bar Visuals")]
    public bool smoothSlider = true;           // 是否平滑过渡
    public float sliderSmoothSpeed = 10f;      // 平滑速度（越大越快）
    public bool useGradient = true;            // 是否使用渐变色
    public Gradient hpGradient;                // 0=低血, 1=满血
    public Image fillImage;                    // Slider Fill 图像（用于上色）

    [Header("Billboard (World-Space HP Bar)")]
    public bool billboard = true;              // 让血条朝向相机
    public Camera targetCamera;                // 为空则 Camera.main
    public bool onlyYaw = true;                // 只水平旋转，避免上下翻转
    public Transform barRootOverride;          // 指定要朝向的 Transform（默认用 hpSlider.transform）

    [Header("Death")]
    public GameObject deathEffect;             // 爆炸特效（可选）
    public int burstCount = 8;                 // 爆炸次数
    public float burstRadius = 3f;             // 爆炸散布半径
    public float destroyDelay = 3f;            // 销毁延时

    private float _hp;
    private float _visualHP;                   // 用于平滑展示的值
    private bool _dead;

    void Awake()
    {
        _hp = maxHP;
        _visualHP = _hp;

        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHP;
            hpSlider.value = _hp;
            hpSlider.gameObject.SetActive(!hideBarWhenFull);

            if (useGradient && fillImage && hpGradient != null)
                fillImage.color = hpGradient.Evaluate(1f);
        }
    }

    void Update()
    {
        // 平滑血条
        if (smoothSlider && hpSlider && Mathf.Abs(_visualHP - _hp) > 0.01f)
        {
            _visualHP = Mathf.Lerp(_visualHP, _hp, Time.deltaTime * sliderSmoothSpeed);
            hpSlider.value = _visualHP;

            if (useGradient && fillImage && hpGradient != null)
            {
                float t = Mathf.InverseLerp(0f, maxHP, _visualHP);
                fillImage.color = hpGradient.Evaluate(t);
            }
        }
    }

    void LateUpdate()
    {
        if (!billboard) return;

        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) return;

        Transform tgt = barRootOverride ? barRootOverride : (hpSlider ? hpSlider.transform : null);
        if (!tgt) return;

        if (onlyYaw)
        {
            Vector3 dir = tgt.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            tgt.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            tgt.LookAt(cam.transform);
        }
    }

    public void TakeDamage(float amount)
    {
        if (_dead) return;

        _hp = Mathf.Clamp(_hp - amount, 0f, maxHP);

        if (hpSlider)
        {
            if (!smoothSlider) // 不平滑则立即刷新
            {
                hpSlider.value = _hp;
                if (useGradient && fillImage && hpGradient != null)
                {
                    float t = Mathf.InverseLerp(0f, maxHP, _hp);
                    fillImage.color = hpGradient.Evaluate(t);
                }
            }
            if (hideBarWhenFull) hpSlider.gameObject.SetActive(_hp < maxHP);
        }

        if (_hp <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (_dead) return;

        _hp = Mathf.Clamp(_hp + amount, 0f, maxHP);

        if (hpSlider)
        {
            if (!smoothSlider)
            {
                hpSlider.value = _hp;
                if (useGradient && fillImage && hpGradient != null)
                {
                    float t = Mathf.InverseLerp(0f, maxHP, _hp);
                    fillImage.color = hpGradient.Evaluate(t);
                }
            }
            if (hideBarWhenFull) hpSlider.gameObject.SetActive(_hp < maxHP);
        }
    }

    public void SetMaxHP(float newMax, bool keepRatio = true)
    {
        float ratio = (_hp <= 0f) ? 0f : _hp / maxHP;
        maxHP = Mathf.Max(1f, newMax);
        _hp = keepRatio ? Mathf.Clamp(maxHP * ratio, 0f, maxHP) : maxHP;
        _visualHP = _hp;

        if (hpSlider)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = _hp;
            if (useGradient && fillImage && hpGradient != null)
            {
                float t = Mathf.InverseLerp(0f, maxHP, _hp);
                fillImage.color = hpGradient.Evaluate(t);
            }
            if (hideBarWhenFull) hpSlider.gameObject.SetActive(_hp < maxHP);
        }
    }

    private void Die()
    {
        _dead = true;

        // 关闭 Fort 行为逻辑
        var fort = GetComponent<Fort>();
        if (fort) fort.enabled = false;

        // 爆炸效果（散布在水平面）
        if (deathEffect)
        {
            for (int i = 0; i < burstCount; i++)
            {
                var pos = transform.position + Random.insideUnitSphere * burstRadius;
                pos.y = transform.position.y;
                Instantiate(deathEffect, pos, Quaternion.identity);
            }
        }

        // 禁用碰撞器，避免后续触发
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 延迟销毁
        Destroy(gameObject, destroyDelay);
    }

#if UNITY_EDITOR
    // 编辑器里改数值时自动校准 Slider
    void OnValidate()
    {
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = Mathf.Max(1f, maxHP);
        }
    }
#endif

    // 读属性
    public float CurrentHP => _hp;
    public float NormalizedHP => maxHP > 0f ? _hp / maxHP : 0f;
    public bool IsDead => _dead;
}
