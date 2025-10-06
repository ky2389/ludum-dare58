using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI energy bar that binds to an EnergySystem.
/// Works with Image(Filled) or width-scaling (no rounded ends).
/// </summary>
public class EnergyBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private EnergySystem target;        // 不填会在父级查找

    [Header("UI (Filled mode)")]
    [SerializeField] private Image fillImage;            // Image.Type = Filled, Method = Horizontal

    [Header("UI (Width mode)")]
    [Tooltip("若不想用 Filled（避免两端圆角），勾选此项并指定 fillRect。")]
    [SerializeField] private bool useWidthInsteadOfFill = false;
    [SerializeField] private RectTransform fillRect;     // 左锚点+左轴心，按宽度缩放

    [Header("Text (optional)")]
    [SerializeField] private Text valueText;             // 可选：显示数值/百分比
    [SerializeField] private bool showPercent = true;

    [Header("Look & Feel")]
    [SerializeField] private Gradient colorByPct;        // 可选：按能量渐变颜色（0=红, 1=绿）
    [SerializeField] private float smoothSpeed = 10f;    // 平滑插值速度
    [SerializeField] private bool hideWhenFull = false;  // 满能量时隐藏

    // runtime
    private float targetPct = 1f;
    private float currentPct = 1f;
    private float maxWidth;                              // width 模式下的满格宽度
    private Color baseFillColor;

    void Reset()
    {
        if (!target) target = GetComponentInParent<EnergySystem>();
        if (!fillImage) fillImage = GetComponentInChildren<Image>();
    }

    void Awake()
    {
        if (!target) target = GetComponentInParent<EnergySystem>();
        if (fillImage) baseFillColor = fillImage.color;
    }

    void OnEnable()
    {
        if (!target) return;

        target.OnEnergyChanged.AddListener(OnEnergyChanged);
        target.OnEnergyFull.AddListener(OnEnergyFull);
        target.OnEnergyDepleted.AddListener(OnEnergyDepleted);
        target.OnLowEnergy.AddListener(OnLowEnergy);

        if (useWidthInsteadOfFill && fillRect)
            maxWidth = fillRect.rect.width;

        ApplyInstant(target.CurrentEnergy, target.MaxEnergy);
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (!target) return;

        target.OnEnergyChanged.RemoveListener(OnEnergyChanged);
        target.OnEnergyFull.RemoveListener(OnEnergyFull);
        target.OnEnergyDepleted.RemoveListener(OnEnergyDepleted);
        target.OnLowEnergy.RemoveListener(OnLowEnergy);
    }

    void Update()
    {
        currentPct = Mathf.MoveTowards(currentPct, targetPct, smoothSpeed * Time.unscaledDeltaTime);
        ApplyToUI(currentPct);
    }

    // ---------- Event handlers ----------
    private void OnEnergyChanged(float current, float max)
    {
        targetPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (valueText)
        {
            if (showPercent) valueText.text = Mathf.RoundToInt(targetPct * 100f) + "%";
            else             valueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }

        UpdateVisibility();
    }

    private void OnEnergyFull()
    {
        UpdateVisibility();
    }

    private void OnEnergyDepleted()
    {
        // 这里可做个闪烁/抖动效果，当前保持简单
        UpdateVisibility();
    }

    private void OnLowEnergy()
    {
        // 低能量时给个颜色提示（若未设置渐变）
        if (colorByPct == null && fillImage)
            fillImage.color = new Color(1f, 0.35f, 0.35f, fillImage.color.a);
    }

    // ---------- Helpers ----------
    private void ApplyInstant(float current, float max)
    {
        currentPct = targetPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        ApplyToUI(currentPct);

        if (valueText)
        {
            if (showPercent) valueText.text = Mathf.RoundToInt(currentPct * 100f) + "%";
            else             valueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }
    }

    private void ApplyToUI(float pct)
    {
        // 颜色
        if (fillImage)
        {
            if (colorByPct != null) fillImage.color = colorByPct.Evaluate(pct);
            else                    fillImage.color = baseFillColor; // 没设渐变就还原
        }

        // 填充 or 宽度
        if (!useWidthInsteadOfFill)
        {
            if (fillImage) fillImage.fillAmount = pct;
        }
        else if (fillRect)
        {
            // 注意：fillRect 的 AnchorMin/Max 应为 (0,0.5)，Pivot 为 (0,0.5)
            var size = fillRect.sizeDelta;
            size.x = maxWidth * pct;
            fillRect.sizeDelta = size;
        }
    }

    private void UpdateVisibility()
    {
        if (!hideWhenFull) return;
        bool show = targetPct < 0.999f;
        if (fillImage) fillImage.enabled = show;
        if (valueText) valueText.enabled = show;
        // 若容器整体要隐藏，用 gameObject.SetActive(show)
    }
}
