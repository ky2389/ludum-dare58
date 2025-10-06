using UnityEngine;
using UnityEngine.UI;

public class FortHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 500f;               // Fort typically has more HP
    public Slider hpSlider;                  // World space Canvas Slider
    public bool hideBarWhenFull = true;

    [Header("Death")]
    public GameObject deathEffect;           // Explosion effect (optional)
    public int burstCount = 8;               // Number of explosion bursts
    public float burstRadius = 3f;           // Radius for explosion points
    public float destroyDelay = 3f;          // Delay before destroying (seconds)

    private float _hp;
    private bool _dead;

    void Awake()
    {
        _hp = maxHP;
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHP;
            hpSlider.value = _hp;
            hpSlider.gameObject.SetActive(!hideBarWhenFull); // Hide if full
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

        // Disable Fort logic
        var fort = GetComponent<Fort>();
        if (fort) fort.enabled = false;

        // Explosion effect
        if (deathEffect)
        {
            for (int i = 0; i < burstCount; i++)
            {
                var pos = transform.position + Random.insideUnitSphere * burstRadius;
                pos.y = transform.position.y;
                Instantiate(deathEffect, pos, Quaternion.identity);
            }
        }

        // Disable colliders to prevent further triggers
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Destroy after delay
        Destroy(gameObject, destroyDelay);
    }
}