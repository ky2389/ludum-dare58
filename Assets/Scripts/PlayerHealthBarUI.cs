using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private HealthSystem target;   // 不填会自动在父级找

    [Header("UI References")]
    [SerializeField] private Image fillImage;       // 设为 Image(Type=Filled, FillMethod=Horizontal)
    [SerializeField] private Text valueText;        // 可选：显示 87/100 或 87%
    [SerializeField] private Gradient colorByHealth;// 可选：按血量渐变颜色（0=红, 1=绿）

    [Header("Behaviour")]
    [SerializeField] private float smoothSpeed = 8f; // 血量变化的平滑速度
    [SerializeField] private bool hideOnDeath = false;

    float targetPct = 1f;
    float currentPct = 1f;

    void Reset()
    {
        // 尝试自动抓取
        if (!target) target = GetComponentInParent<HealthSystem>();
        if (!fillImage) fillImage = GetComponentInChildren<Image>();
    }

    void Awake()
    {
        if (!target) target = GetComponentInParent<HealthSystem>();
    }

    void OnEnable()
    {
        if (!target) return;
        target.OnHealthChanged.AddListener(OnHealthChanged);
        target.OnDeath.AddListener(OnDeath);
        // 初始化一次
        ApplyInstant(target.CurrentHealth, target.MaxHealth);
    }

    void OnDisable()
    {
        if (!target) return;
        target.OnHealthChanged.RemoveListener(OnHealthChanged);
        target.OnDeath.RemoveListener(OnDeath);
    }

    void Update()
    {
        // 平滑插值
        currentPct = Mathf.MoveTowards(currentPct, targetPct, Time.unscaledDeltaTime * smoothSpeed);
        ApplyToUI(currentPct);
    }

    void OnHealthChanged(float current, float max)
    {
        targetPct = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
        if (valueText)
        {
            // 你也可以改成百分比：valueText.text = Mathf.RoundToInt(targetPct * 100f) + "%";
            valueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }
    }

    void OnDeath()
    {
        if (hideOnDeath) gameObject.SetActive(false);
        // 也可以在这里播一个死亡闪烁动画之类
    }

    void ApplyInstant(float current, float max)
    {
        targetPct = currentPct = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
        ApplyToUI(currentPct);
        if (valueText)
            valueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
    }

    void ApplyToUI(float pct)
    {
        if (fillImage)
        {
            fillImage.fillAmount = pct;
            if (colorByHealth != null)
                fillImage.color = colorByHealth.Evaluate(pct);
        }
    }
}
