using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles collision damage from collector tank treads to the player.
/// Attach this to the tank tread GameObjects that should cause damage.
/// </summary>
public class TrackDamage : MonoBehaviour
{
    [Header("Damage Configuration")]
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private float damageForce = 500f; // Knockback force
    [SerializeField] private LayerMask playerLayer = -1; // What layers count as "player"
    
    [Header("Collision Settings")]
    [SerializeField] private string playerTag = "Player"; // Tag to identify player
    
    [Header("Visual/Audio Feedback")]
    [SerializeField] private GameObject damageEffectPrefab; // VFX on damage
    [SerializeField] private AudioClip damageSound; // Audio on damage
    [SerializeField] private float effectLifetime = 2f;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool drawDebugGizmos = true;
    
    // Events for external systems
    [System.Serializable]
    public class DamageEvent : UnityEvent<GameObject, float> { } // target, damage
    
    public DamageEvent OnPlayerDamaged = new DamageEvent();
    
    // Properties
    public float DamageAmount => damageAmount;
    public bool IsActive { get; private set; } = true;
    
    // Private components
    private Rigidbody collectorRigidbody;
    private AudioSource audioSource;
    private Collider treadCollider;
    
    void Start()
    {
        InitializeComponents();
    }
    
    /// <summary>
    /// Initialize required components and references
    /// </summary>
    private void InitializeComponents()
    {
        // Get collider on this GameObject
        treadCollider = GetComponent<Collider>();
        if (treadCollider == null)
        {
            Debug.LogError($"[CollectorTreadDamage] No Collider found on {gameObject.name}! Adding BoxCollider.");
            treadCollider = gameObject.AddComponent<BoxCollider>();
            treadCollider.isTrigger = true;
        }
        
        // Ensure collider is set as trigger for damage detection
        if (!treadCollider.isTrigger)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CollectorTreadDamage] Collider on {gameObject.name} is not a trigger. Setting as trigger for damage detection.");
            treadCollider.isTrigger = true;
        }
        
        // Get collector's rigidbody (kept for potential future use)
        collectorRigidbody = GetComponentInParent<Rigidbody>();
        
        // Setup audio source for damage sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && damageSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
        
        if (enableDebugLogs)
            Debug.Log($"[CollectorTreadDamage] Initialized on {gameObject.name}. Damage: {damageAmount}, Force: {damageForce}");
    }
    
    /// <summary>
    /// Handle trigger collision with player
    /// </summary>
    /// <param name="other">The colliding object</param>
    void OnTriggerEnter(Collider other)
    {
        // Check if this is a player
        if (!IsPlayerObject(other.gameObject))
            return;
        
        // Check if damage system is active
        if (!IsActive)
            return;
        
        // Attempt to damage the player
        AttemptDamagePlayer(other.gameObject);
    }
    
    /// <summary>
    /// Check if the colliding object is a player
    /// </summary>
    /// <param name="obj">Object to check</param>
    /// <returns>True if this is a player object</returns>
    private bool IsPlayerObject(GameObject obj)
    {
        // Check by tag
        if (!string.IsNullOrEmpty(playerTag) && !obj.CompareTag(playerTag))
            return false;
        
        // Check by layer
        if (playerLayer.value != -1 && (playerLayer.value & (1 << obj.layer)) == 0)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Attempt to damage the player
    /// </summary>
    /// <param name="playerObject">Player GameObject</param>
    private void AttemptDamagePlayer(GameObject playerObject)
    {
        // Get player's damage receiver component
        var damageReceiver = playerObject.GetComponent<PlayerDamageManager>();
        if (damageReceiver == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CollectorTreadDamage] Player {playerObject.name} has no PlayerDamageManager component!");
            return;
        }
        
        // Calculate damage direction for knockback
        Vector3 damageDirection = (playerObject.transform.position - transform.position).normalized;
        
        // Attempt to apply damage with smooth knockback
        bool damageApplied = damageReceiver.TakeDamageAdvanced(damageAmount, damageDirection, damageForce, true, "Collector Track");
        
        if (damageApplied)
        {
            // Trigger events and effects
            OnPlayerDamaged.Invoke(playerObject, damageAmount);
            PlayDamageEffects(playerObject.transform.position);
            
            if (enableDebugLogs)
                Debug.Log($"[TrackDamage] Damaged player {playerObject.name} for {damageAmount} HP.");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[CollectorTreadDamage] Player {playerObject.name} is immune to damage.");
        }
    }
    
    /// <summary>
    /// Play visual and audio effects for damage
    /// </summary>
    /// <param name="effectPosition">Position to spawn effects</param>
    private void PlayDamageEffects(Vector3 effectPosition)
    {
        // Spawn visual effect
        if (damageEffectPrefab != null)
        {
            GameObject effect = Instantiate(damageEffectPrefab, effectPosition, Quaternion.identity);
            
            if (effectLifetime > 0f)
                Destroy(effect, effectLifetime);
        }
        
        // Play damage sound
        if (audioSource != null && damageSound != null)
        {
            audioSource.clip = damageSound;
            audioSource.Play();
        }
    }
    
    /// <summary>
    /// Enable or disable the damage system
    /// </summary>
    /// <param name="active">Whether damage should be active</param>
    public void SetActive(bool active)
    {
        IsActive = active;
        
        if (enableDebugLogs)
            Debug.Log($"[CollectorTreadDamage] Damage system {(active ? "enabled" : "disabled")} on {gameObject.name}");
    }
    
    /// <summary>
    /// Update damage amount at runtime
    /// </summary>
    /// <param name="newDamage">New damage amount</param>
    public void SetDamageAmount(float newDamage)
    {
        damageAmount = Mathf.Max(0f, newDamage);
        
        if (enableDebugLogs)
            Debug.Log($"[CollectorTreadDamage] Damage amount updated to {damageAmount} on {gameObject.name}");
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!drawDebugGizmos) return;
        
        // Draw damage area
        Gizmos.color = IsActive ? Color.red : Color.gray;
        
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (collider is BoxCollider box)
                Gizmos.DrawWireCube(box.center, box.size);
            else if (collider is SphereCollider sphere)
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            else if (collider is CapsuleCollider capsule)
                Gizmos.DrawWireCube(capsule.center, new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));
        }
    }
}
