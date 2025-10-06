using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private EnergySystem target;     // 不填会在父级或场景里找

    [Header("UI")]
    [SerializeField] private Image fillImage;         // Image: Type = Filled, Method = Horizontal, Origin = Left
    [SerializeField] private Text valueText;          // 可选：显示百分比或 当前/最大
    [SerializeField] private Gradient colorByEnergy;  // 可选：按能量渐变颜色（0=红,1=青/绿）

    [Header("Behaviour")]
    [SerializeField] private float smoothSpeed = 8f;  // UI 平滑速度
    [SerializeField] private bool showPercent = true; // 文本显示为百分比还是“当前/最大”

    private float visualPct = 1f; // 当前UI显示的百分比（平滑用）

    void Reset()
    {
        if (!target) target = GetComponentInParent<EnergySystem>();
        if (!fillImage) fillImage = GetComponentInChildren<Image>();
    }

    void Awake()
    {
        if (!target) target = GetComponentInParent<EnergySystem>();
        if (!target) target = FindObjectOfType<EnergySystem>(true);
    }

    void OnEnable()
    {
        SnapToCurrent(); // 进场时先对齐一次
    }

    void Update()
    {
        if (!target || !fillImage) return;

        // 每帧从 EnergySystem 读取当前比例（不依赖事件）
        float pct = (target.MaxEnergy > 0f) ? target.CurrentEnergy / target.MaxEnergy : 0f;

        // 平滑过渡
        visualPct = Mathf.MoveTowards(visualPct, pct, smoothSpeed * Time.unscaledDeltaTime);

        // 应用到 UI
        fillImage.fillAmount = visualPct;
        if (colorByEnergy != null)
            fillImage.color = colorByEnergy.Evaluate(visualPct);

        if (valueText)
        {
            if (showPercent)
                valueText.text = Mathf.RoundToInt(visualPct * 100f) + "%";
            else
                valueText.text = $"{Mathf.CeilToInt(target.CurrentEnergy)}/{Mathf.CeilToInt(target.MaxEnergy)}";
        }
    }

    private void SnapToCurrent()
    {
        if (!target || !fillImage) return;
        visualPct = (target.MaxEnergy > 0f) ? target.CurrentEnergy / target.MaxEnergy : 0f;
        fillImage.fillAmount = visualPct;
        if (colorByEnergy != null)
            fillImage.color = colorByEnergy.Evaluate(visualPct);
        if (valueText)
            valueText.text = showPercent
                ? Mathf.RoundToInt(visualPct * 100f) + "%"
                : $"{Mathf.CeilToInt(target.CurrentEnergy)}/{Mathf.CeilToInt(target.MaxEnergy)}";
    }
}
