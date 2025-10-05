using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Universal health system component for both players and enemies.
/// Supports both HUD-based (player) and world-space (enemy) health bars.
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [Header("Health Configuration")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Regeneration (Optional)")]
    [SerializeField] private bool canRegenerate = false;
    [SerializeField] private float regenRate = 5f; // HP per second
    [SerializeField] private float regenDelay = 3f; // Seconds after taking damage before regen starts
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private string entityName = "Entity"; // For debug identification
    
    [Header("Death Behavior")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;
    
    // Events for UI and gameplay systems
    [System.Serializable]
    public class HealthEvent : UnityEvent<float, float> { } // current, max
    
    public HealthEvent OnHealthChanged = new HealthEvent();
    public UnityEvent OnDeath = new UnityEvent();
    public UnityEvent OnHealthFull = new UnityEvent();
    public UnityEvent OnLowHealth = new UnityEvent(); // Triggered at 25% health
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsAlive => currentHealth > 0f;
    public bool IsFullHealth => Mathf.Approximately(currentHealth, maxHealth);
    
    // Private variables
    private float lastDamageTime;
    private bool isDead = false;
    
    void Start()
    {
        InitializeHealth();
    }
    
    void Update()
    {
        HandleRegeneration();
    }
    
    /// <summary>
    /// Initialize health to maximum value
    /// </summary>
    public void InitializeHealth()
    {
        currentHealth = maxHealth;
        lastDamageTime = -regenDelay; // Allow immediate regen if enabled
        isDead = false;
        
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} initialized with {maxHealth} HP");
        
        OnHealthChanged.Invoke(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Apply damage to this entity
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    /// <param name="damageSource">Optional source of damage for logging</param>
    public void TakeDamage(float damage, string damageSource = "Unknown")
    {
        if (isDead || damage <= 0f) return;
        
        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        lastDamageTime = Time.time;
        
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} took {damage:F1} damage from {damageSource}. Health: {previousHealth:F1} → {currentHealth:F1}");
        
        OnHealthChanged.Invoke(currentHealth, maxHealth);
        
        // Check for low health warning (25% threshold)
        if (!isDead && HealthPercentage <= 0.25f && previousHealth / maxHealth > 0.25f)
        {
            OnLowHealth.Invoke();
            if (enableDebugLogs)
                Debug.LogWarning($"[HealthSystem] {entityName} is at low health! ({HealthPercentage:P0})");
        }
        
        // Check for death
        if (currentHealth <= 0f && !isDead)
        {
            HandleDeath();
        }
    }
    
    /// <summary>
    /// Restore health to this entity
    /// </summary>
    /// <param name="healAmount">Amount of health to restore</param>
    /// <param name="healSource">Optional source of healing for logging</param>
    public void Heal(float healAmount, string healSource = "Unknown")
    {
        if (isDead || healAmount <= 0f) return;
        
        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        
        if (enableDebugLogs && healAmount > 0f)
            Debug.Log($"[HealthSystem] {entityName} healed {healAmount:F1} HP from {healSource}. Health: {previousHealth:F1} → {currentHealth:F1}");
        
        OnHealthChanged.Invoke(currentHealth, maxHealth);
        
        // Check if fully healed
        if (IsFullHealth && previousHealth < maxHealth)
        {
            OnHealthFull.Invoke();
            if (enableDebugLogs)
                Debug.Log($"[HealthSystem] {entityName} is at full health!");
        }
    }
    
    /// <summary>
    /// Set health to a specific value
    /// </summary>
    /// <param name="newHealth">New health value</param>
    public void SetHealth(float newHealth)
    {
        if (isDead) return;
        
        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} health set to {currentHealth:F1} (was {previousHealth:F1})");
        
        OnHealthChanged.Invoke(currentHealth, maxHealth);
        
        if (currentHealth <= 0f && !isDead)
        {
            HandleDeath();
        }
    }
    
    /// <summary>
    /// Instantly kill this entity
    /// </summary>
    public void Kill(string killSource = "Unknown")
    {
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} was killed by {killSource}");
        
        SetHealth(0f);
    }
    
    /// <summary>
    /// Reset to full health and revive if dead
    /// </summary>
    public void Revive()
    {
        isDead = false;
        InitializeHealth();
        
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} has been revived!");
    }
    
    private void HandleRegeneration()
    {
        if (!canRegenerate || isDead || IsFullHealth) return;
        if (Time.time - lastDamageTime < regenDelay) return;
        
        float regenAmount = regenRate * Time.deltaTime;
        Heal(regenAmount, "Regeneration");
    }
    
    private void HandleDeath()
    {
        isDead = true;
        
        if (enableDebugLogs)
            Debug.Log($"[HealthSystem] {entityName} has died!");
        
        OnDeath.Invoke();
        
        if (destroyOnDeath)
        {
            if (destroyDelay > 0f)
                Destroy(gameObject, destroyDelay);
            else
                Destroy(gameObject);
        }
    }
    
    // Debug methods for testing
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugTakeDamage(float damage)
    {
        TakeDamage(damage, "Debug Command");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugHeal(float healAmount)
    {
        Heal(healAmount, "Debug Command");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugKill()
    {
        Kill("Debug Command");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugRevive()
    {
        Revive();
    }
}
