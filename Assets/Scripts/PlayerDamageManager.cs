using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Handles damage reception for the player with immunity intervals and knockback.
/// Attach this to the player GameObject alongside HealthSystem.
/// Provides immunity periods to prevent damage spam and enhance gameplay.
/// </summary>
public class PlayerDamageManager : MonoBehaviour
{
    [Header("Immunity System")]
    [SerializeField] private float immunityDuration = 1.5f; // Seconds of immunity after taking damage
    [SerializeField] private bool showImmunityVisuals = true; // Flash player during immunity
    [SerializeField] private float flashInterval = 0.1f; // How fast to flash during immunity
    
    [Header("Knockback System")]
    [SerializeField] private bool enableKnockback = true;
    [SerializeField] private float knockbackMultiplier = 1f; // Multiplier for incoming knockback forces
    [SerializeField] private float maxKnockbackForce = 1000f; // Maximum knockback force allowed
    [SerializeField] private float knockbackDuration = 0.3f; // How long knockback affects movement
    [SerializeField] private AnimationCurve knockbackCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // Smooth knockback curve
    
    [Header("Damage Filtering")]
    [SerializeField] private float minimumDamage = 1f; // Ignore damage below this threshold
    [SerializeField] private float maximumDamage = 100f; // Cap damage at this amount
    
    [Header("Visual Feedback")]
    [SerializeField] private Renderer[] renderersToFlash; // Renderers to flash during immunity
    [SerializeField] private Color immunityFlashColor = Color.red; // Color to flash during immunity
    [SerializeField] private GameObject damageEffectPrefab; // VFX when taking damage
    [SerializeField] private AudioClip damageSound; // Sound when taking damage
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool drawDebugInfo = true;
    
    // Events for external systems
    [System.Serializable]
    public class PlayerDamageEvent : UnityEvent<float, Vector3, string> { } // damage, knockback, source
    
    public PlayerDamageEvent OnDamageTaken = new PlayerDamageEvent();
    public UnityEvent OnImmunityStarted = new UnityEvent();
    public UnityEvent OnImmunityEnded = new UnityEvent();
    
    // Properties
    public bool IsImmune { get; private set; } = false;
    public float RemainingImmunityTime { get; private set; } = 0f;
    public bool IsKnockedBack { get; private set; } = false;
    
    // Private components and state
    private HealthSystem healthSystem;
    private Rigidbody playerRigidbody;
    private AudioSource audioSource;
    private BasicBehaviour playerBehaviour; // For movement control during knockback
    
    // Immunity visual system
    private Material[] originalMaterials;
    private Material[] flashMaterials;
    private Coroutine immunityCoroutine;
    private Coroutine knockbackCoroutine;
    
    void Start()
    {
        InitializeComponents();
        SetupImmunityVisuals();
    }
    
    void Update()
    {
        UpdateImmunityTimer();
    }
    
    /// <summary>
    /// Initialize required components
    /// </summary>
    private void InitializeComponents()
    {
        // Get health system
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem == null)
        {
            Debug.LogError($"[PlayerDamageReceiver] No HealthSystem found on {gameObject.name}! This component requires a HealthSystem.");
            enabled = false;
            return;
        }
        
        // Get player rigidbody for knockback
        playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody == null && enableKnockback)
        {
            Debug.LogWarning($"[PlayerDamageReceiver] No Rigidbody found on {gameObject.name}. Knockback disabled.");
            enableKnockback = false;
        }
        
        // Get player behaviour for movement control
        playerBehaviour = GetComponent<BasicBehaviour>();
        
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && damageSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound for player
        }
        
        // Auto-populate renderers if not set
        if (renderersToFlash == null || renderersToFlash.Length == 0)
        {
            renderersToFlash = GetComponentsInChildren<Renderer>();
        }
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Initialized. Immunity: {immunityDuration}s, Knockback: {(enableKnockback ? "Enabled" : "Disabled")}");
    }
    
    /// <summary>
    /// Setup materials for immunity flashing
    /// </summary>
    private void SetupImmunityVisuals()
    {
        if (!showImmunityVisuals || renderersToFlash == null) return;
        
        // Store original materials
        originalMaterials = new Material[renderersToFlash.Length];
        flashMaterials = new Material[renderersToFlash.Length];
        
        for (int i = 0; i < renderersToFlash.Length; i++)
        {
            if (renderersToFlash[i] != null)
            {
                originalMaterials[i] = renderersToFlash[i].material;
                
                // Create flash material
                flashMaterials[i] = new Material(originalMaterials[i]);
                flashMaterials[i].color = immunityFlashColor;
            }
        }
    }
    
    /// <summary>
    /// Update immunity timer
    /// </summary>
    private void UpdateImmunityTimer()
    {
        if (IsImmune)
        {
            RemainingImmunityTime -= Time.deltaTime;
            if (RemainingImmunityTime <= 0f)
            {
                EndImmunity();
            }
        }
    }
    
    /// <summary>
    /// Attempt to damage the player
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    /// <param name="knockbackForce">Knockback force vector</param>
    /// <param name="damageSource">Source of the damage for logging</param>
    /// <returns>True if damage was applied, false if immune or invalid</returns>
    public bool TakeDamage(float damage, Vector3 knockbackForce, string damageSource = "Unknown")
    {
        // Check immunity
        if (IsImmune)
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerDamageReceiver] Damage blocked - player is immune ({RemainingImmunityTime:F1}s remaining)");
            return false;
        }
        
        // Validate damage amount
        if (damage < minimumDamage)
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerDamageReceiver] Damage {damage} below minimum threshold {minimumDamage}");
            return false;
        }
        
        // Cap damage at maximum
        float finalDamage = Mathf.Min(damage, maximumDamage);
        
        // Apply damage to health system
        healthSystem.TakeDamage(finalDamage, damageSource);
        
        // Apply knockback if enabled
        if (enableKnockback && knockbackForce.magnitude > 0f)
        {
            ApplyKnockback(knockbackForce);
        }
        
        // Start immunity period
        StartImmunity();
        
        // Play effects
        PlayDamageEffects();
        
        // Trigger events
        OnDamageTaken.Invoke(finalDamage, knockbackForce, damageSource);
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Took {finalDamage} damage from {damageSource}. Health: {healthSystem.CurrentHealth:F1}/{healthSystem.MaxHealth:F1}");
        
        return true;
    }
    
    /// <summary>
    /// Take damage without knockback (for bullets, projectiles, etc.)
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    /// <param name="damageSource">Source of the damage for logging</param>
    /// <returns>True if damage was applied, false if immune or invalid</returns>
    public bool TakeDamageNoKnockback(float damage, string damageSource = "Unknown")
    {
        return TakeDamage(damage, Vector3.zero, damageSource);
    }
    
    /// <summary>
    /// Take damage with optional knockback control
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    /// <param name="knockbackDirection">Direction of knockback (will be normalized)</param>
    /// <param name="knockbackStrength">Strength of knockback force</param>
    /// <param name="useKnockback">Whether to apply knockback</param>
    /// <param name="damageSource">Source of the damage for logging</param>
    /// <returns>True if damage was applied, false if immune or invalid</returns>
    public bool TakeDamageAdvanced(float damage, Vector3 knockbackDirection, float knockbackStrength, bool useKnockback, string damageSource = "Unknown")
    {
        Vector3 knockbackForce = useKnockback ? knockbackDirection.normalized * knockbackStrength : Vector3.zero;
        return TakeDamage(damage, knockbackForce, damageSource);
    }
    
    /// <summary>
    /// Apply smooth knockback force to the player
    /// </summary>
    /// <param name="knockbackForce">Force vector to apply</param>
    private void ApplyKnockback(Vector3 knockbackForce)
    {
        if (playerRigidbody == null) return;
        
        // Clamp knockback force
        Vector3 clampedForce = Vector3.ClampMagnitude(knockbackForce * knockbackMultiplier, maxKnockbackForce);
        
        // Start smooth knockback coroutine instead of instant force
        if (knockbackCoroutine != null)
            StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(SmoothKnockbackCoroutine(clampedForce));
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Applied smooth knockback force: {clampedForce.magnitude:F1}");
    }
    
    /// <summary>
    /// Apply smooth knockback over time using animation curve
    /// </summary>
    /// <param name="totalForce">Total force to apply over duration</param>
    private IEnumerator SmoothKnockbackCoroutine(Vector3 totalForce)
    {
        IsKnockedBack = true;
        float elapsedTime = 0f;
        
        // Apply force gradually over time using the curve
        while (elapsedTime < knockbackDuration)
        {
            float normalizedTime = elapsedTime / knockbackDuration;
            float curveValue = knockbackCurve.Evaluate(normalizedTime);
            
            // Apply force this frame
            Vector3 frameForce = totalForce * curveValue * Time.fixedDeltaTime / knockbackDuration;
            playerRigidbody.AddForce(frameForce, ForceMode.VelocityChange);
            
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        
        IsKnockedBack = false;
    }
    
    /// <summary>
    /// Handle knockback duration
    /// </summary>
    private IEnumerator KnockbackDurationCoroutine()
    {
        IsKnockedBack = true;
        
        // Could disable player movement here if needed
        // if (playerBehaviour != null) playerBehaviour.enabled = false;
        
        yield return new WaitForSeconds(knockbackDuration);
        
        IsKnockedBack = false;
        
        // Re-enable player movement if it was disabled
        // if (playerBehaviour != null) playerBehaviour.enabled = true;
    }
    
    /// <summary>
    /// Start immunity period
    /// </summary>
    private void StartImmunity()
    {
        IsImmune = true;
        RemainingImmunityTime = immunityDuration;
        
        // Start visual effects
        if (showImmunityVisuals && immunityCoroutine == null)
        {
            immunityCoroutine = StartCoroutine(ImmunityFlashCoroutine());
        }
        
        OnImmunityStarted.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Immunity started for {immunityDuration} seconds");
    }
    
    /// <summary>
    /// End immunity period
    /// </summary>
    private void EndImmunity()
    {
        IsImmune = false;
        RemainingImmunityTime = 0f;
        
        // Stop visual effects
        if (immunityCoroutine != null)
        {
            StopCoroutine(immunityCoroutine);
            immunityCoroutine = null;
            RestoreOriginalMaterials();
        }
        
        OnImmunityEnded.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Immunity ended");
    }
    
    /// <summary>
    /// Handle immunity flashing visuals
    /// </summary>
    private IEnumerator ImmunityFlashCoroutine()
    {
        bool useFlashMaterial = false;
        
        while (IsImmune)
        {
            // Toggle between original and flash materials
            useFlashMaterial = !useFlashMaterial;
            
            for (int i = 0; i < renderersToFlash.Length; i++)
            {
                if (renderersToFlash[i] != null)
                {
                    renderersToFlash[i].material = useFlashMaterial ? flashMaterials[i] : originalMaterials[i];
                }
            }
            
            yield return new WaitForSeconds(flashInterval);
        }
        
        // Ensure we end with original materials
        RestoreOriginalMaterials();
    }
    
    /// <summary>
    /// Restore original materials
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        if (originalMaterials == null) return;
        
        for (int i = 0; i < renderersToFlash.Length; i++)
        {
            if (renderersToFlash[i] != null && originalMaterials[i] != null)
            {
                renderersToFlash[i].material = originalMaterials[i];
            }
        }
    }
    
    /// <summary>
    /// Play damage visual and audio effects
    /// </summary>
    private void PlayDamageEffects()
    {
        // Spawn damage VFX
        if (damageEffectPrefab != null)
        {
            GameObject effect = Instantiate(damageEffectPrefab, transform.position, Quaternion.identity);
            
            // Auto-destroy effect
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(effect, ps.main.duration + ps.main.startLifetime.constantMax);
            else
                Destroy(effect, 2f);
        }
        
        // Play damage sound
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
    }
    
    /// <summary>
    /// Force end immunity (for testing or special cases)
    /// </summary>
    public void ForceEndImmunity()
    {
        if (IsImmune)
        {
            EndImmunity();
            if (enableDebugLogs)
                Debug.Log($"[PlayerDamageReceiver] Immunity forcibly ended");
        }
    }
    
    /// <summary>
    /// Update immunity duration at runtime
    /// </summary>
    /// <param name="newDuration">New immunity duration in seconds</param>
    public void SetImmunityDuration(float newDuration)
    {
        immunityDuration = Mathf.Max(0f, newDuration);
        if (enableDebugLogs)
            Debug.Log($"[PlayerDamageReceiver] Immunity duration updated to {immunityDuration} seconds");
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!drawDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(1, 550, 300, 100));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("PLAYER DAMAGE RECEIVER", "boldlabel");
        GUILayout.Label($"Immune: {(IsImmune ? $"YES ({RemainingImmunityTime:F1}s)" : "NO")}");
        GUILayout.Label($"Knocked Back: {(IsKnockedBack ? "YES" : "NO")}");
        GUILayout.Label($"Health: {healthSystem.CurrentHealth:F1}/{healthSystem.MaxHealth:F1}");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    
    void OnDestroy()
    {
        // Clean up materials
        if (flashMaterials != null)
        {
            for (int i = 0; i < flashMaterials.Length; i++)
            {
                if (flashMaterials[i] != null)
                    DestroyImmediate(flashMaterials[i]);
            }
        }
    }
}
