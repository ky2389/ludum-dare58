using UnityEngine;
using UnityEngine.UI;

public class DroneHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 200f;
    public Slider hpSlider;                  // 世界空间Canvas里的 Slider
    public bool hideBarWhenFull = true;

    [Header("Death")]
    public GameObject deathEffect;           // 爆炸特效（可选）
    public int burstCount = 6;               // 连环爆炸个数
    public float burstRadius = 2.5f;         // 爆炸点半径
    public float destroyDelay = 2f;          // 死亡后销毁延迟（秒）

    private float _hp;
    private bool _dead;

    void Awake()
    {
        _hp = maxHP;
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHP;
            hpSlider.value    = _hp;
            hpSlider.gameObject.SetActive(!hideBarWhenFull); // 满血时可隐藏
        }
    }

    public void TakeDamage(float amount)
    {
        if (_dead) return;
        _hp = Mathf.Clamp(_hp - amount, 0f, maxHP);

        if (hpSlider)
        {
            hpSlider.value = _hp;
            if (hideBarWhenFull) hpSlider.gameObject.SetActive(_hp < maxHP);
        }

        if (_hp <= 0f) Die();
    }

    private void Die()
    {
        _dead = true;

        // 关掉炮塔逻辑与激光
        var d = GetComponent<drone>();
        if (d) d.enabled = false;

        foreach (var lr in GetComponentsInChildren<LineRenderer>())
            lr.enabled = false;

        // 爆炸效果
        if (deathEffect)
        {
            for (int i = 0; i < burstCount; i++)
            {
                var pos = transform.position + Random.insideUnitSphere * burstRadius;
                pos.y = transform.position.y;
                Instantiate(deathEffect, pos, Quaternion.identity);
            }
        }

        // 关碰撞，避免多次触发
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 延迟销毁（或改成 SetActive(false)）
        Destroy(gameObject, destroyDelay);
    }
}
