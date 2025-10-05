using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Energy system component for player abilities like flying and sprinting.
/// Handles energy consumption, regeneration, and provides API for ability systems.
/// </summary>
public class EnergySystem : MonoBehaviour
{
    [Header("Energy Configuration")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy;
    
    [Header("Regeneration")]
    [SerializeField] private float regenRate = 20f; // Energy per second when idle
    [SerializeField] private float regenDelay = 1f; // Seconds after using energy before regen starts
    
    [Header("Consumption Rates")]
    [SerializeField] private float flyDrainRate = 10f; // Energy per second when flying
    [SerializeField] private float sprintFlyDrainRate = 20f; // Energy per second when sprint-flying
    
    [Header("Energy Thresholds")]
    [SerializeField] private float minimumFlyEnergy = 10f; // Minimum energy needed to start flying
    [SerializeField] private float lowEnergyThreshold = 25f; // Percentage for low energy warning
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private string entityName = "Player"; // For debug identification
    
    // Events for UI and gameplay systems
    [System.Serializable]
    public class EnergyEvent : UnityEvent<float, float> { } // current, max
    
    public EnergyEvent OnEnergyChanged = new EnergyEvent();
    public UnityEvent OnEnergyDepleted = new UnityEvent();
    public UnityEvent OnEnergyFull = new UnityEvent();
    public UnityEvent OnLowEnergy = new UnityEvent();
    
    // Properties
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public float EnergyPercentage => maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
    public bool CanFly => currentEnergy >= minimumFlyEnergy;
    public bool IsFullEnergy => Mathf.Approximately(currentEnergy, maxEnergy);
    public bool IsEnergyDepleted => currentEnergy <= 0f;
    
    // Private variables
    private float lastConsumptionTime;
    private bool wasLowEnergy = false;
    
    void Start()
    {
        InitializeEnergy();
    }
    
    void Update()
    {
        HandleRegeneration();
        CheckEnergyThresholds();
    }
    
    /// <summary>
    /// Initialize energy to maximum value
    /// </summary>
    public void InitializeEnergy()
    {
        currentEnergy = maxEnergy;
        lastConsumptionTime = -regenDelay; // Allow immediate regen
        
        //if (enableDebugLogs)
            // Debug.Log($"[EnergySystem] {entityName} initialized with {maxEnergy} energy");
        
        OnEnergyChanged.Invoke(currentEnergy, maxEnergy);
    }
    
    /// <summary>
    /// Consume energy for flying (normal speed)
    /// </summary>
    /// <returns>True if energy was consumed, false if insufficient energy</returns>
    public bool ConsumeFlyEnergy()
    {
        return ConsumeEnergy(flyDrainRate * Time.deltaTime, "Flying");
    }
    
    /// <summary>
    /// Consume energy for sprint-flying (faster speed, more consumption)
    /// </summary>
    /// <returns>True if energy was consumed, false if insufficient energy</returns>
    public bool ConsumeSprintFlyEnergy()
    {
        return ConsumeEnergy(sprintFlyDrainRate * Time.deltaTime, "Sprint Flying");
    }
    
    /// <summary>
    /// Generic method to consume energy
    /// </summary>
    /// <param name="amount">Amount of energy to consume</param>
    /// <param name="source">Source of consumption for logging</param>
    /// <returns>True if energy was consumed, false if insufficient</returns>
    public bool ConsumeEnergy(float amount, string source = "Unknown")
    {
        if (amount <= 0f) return true;
        
        // If no energy left, can't consume anything
        if (currentEnergy <= 0f)
        {
            return false;
        }
        
        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        lastConsumptionTime = Time.time;
        
        // Log if we couldn't consume the full amount
        if (enableDebugLogs && amount > 0.1f && previousEnergy < amount)
            Debug.LogWarning($"[EnergySystem] {entityName} partial energy consumption for {source}. Needed {amount:F1}, had {previousEnergy:F1}");
        
        if (enableDebugLogs && amount > 0.1f) // Only log significant consumption to avoid spam
            // Debug.Log($"[EnergySystem] {entityName} consumed {amount:F1} energy for {source}. Energy: {previousEnergy:F1} → {currentEnergy:F1}");
        
        OnEnergyChanged.Invoke(currentEnergy, maxEnergy);
        
        // Check for energy depletion
        if (currentEnergy <= 0f && previousEnergy > 0f)
        {
            OnEnergyDepleted.Invoke();
            //if (enableDebugLogs)
                // Debug.LogWarning($"[EnergySystem] {entityName} energy depleted!");
        }
        
        return true;
    }
    
    /// <summary>
    /// Restore energy
    /// </summary>
    /// <param name="amount">Amount of energy to restore</param>
    /// <param name="source">Source of restoration for logging</param>
    public void RestoreEnergy(float amount, string source = "Unknown")
    {
        if (amount <= 0f) return;
        
        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        
        if (enableDebugLogs && amount > 0.1f) // Only log significant restoration
            // Debug.Log($"[EnergySystem] {entityName} restored {amount:F1} energy from {source}. Energy: {previousEnergy:F1} → {currentEnergy:F1}");
        
        OnEnergyChanged.Invoke(currentEnergy, maxEnergy);
        
        // Check if fully restored
        if (IsFullEnergy && previousEnergy < maxEnergy)
        {
            OnEnergyFull.Invoke();
            //if (enableDebugLogs)
                // Debug.Log($"[EnergySystem] {entityName} energy is full!");
        }
    }
    
    /// <summary>
    /// Set energy to a specific value
    /// </summary>
    /// <param name="newEnergy">New energy value</param>
    public void SetEnergy(float newEnergy)
    {
        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Clamp(newEnergy, 0f, maxEnergy);
        
        //if (enableDebugLogs)
            // Debug.Log($"[EnergySystem] {entityName} energy set to {currentEnergy:F1} (was {previousEnergy:F1})");
        
        OnEnergyChanged.Invoke(currentEnergy, maxEnergy);
    }
    
    /// <summary>
    /// Check if enough energy is available for an action
    /// </summary>
    /// <param name="requiredEnergy">Energy required for the action</param>
    /// <returns>True if enough energy is available</returns>
    public bool HasEnoughEnergy(float requiredEnergy)
    {
        return currentEnergy >= requiredEnergy;
    }
    
    private void HandleRegeneration()
    {
        if (IsFullEnergy) return;
        if (Time.time - lastConsumptionTime < regenDelay) return;
        
        float regenAmount = regenRate * Time.deltaTime;
        RestoreEnergy(regenAmount, "Regeneration");
    }
    
    private void CheckEnergyThresholds()
    {
        bool isLowEnergy = EnergyPercentage <= (lowEnergyThreshold / 100f);
        
        // Trigger low energy event when crossing threshold
        if (isLowEnergy && !wasLowEnergy)
        {
            OnLowEnergy.Invoke();
            //if (enableDebugLogs)
                // Debug.LogWarning($"[EnergySystem] {entityName} is at low energy! ({EnergyPercentage:P0})");
        }
        
        wasLowEnergy = isLowEnergy;
    }
    
    // Debug methods for testing
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugConsumeEnergy(float amount)
    {
        ConsumeEnergy(amount, "Debug Command");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugRestoreEnergy(float amount)
    {
        RestoreEnergy(amount, "Debug Command");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugDepleteEnergy()
    {
        SetEnergy(0f);
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugFillEnergy()
    {
        SetEnergy(maxEnergy);
    }
}
