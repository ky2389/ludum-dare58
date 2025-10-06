using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShieldGenerator : MonoBehaviour
{
    [Header("Shield Configuration")]
    [SerializeField] private float shieldRadius = 15f;
    [SerializeField] private GameObject shieldSpherePrefab;      // Sphere with forceshield material
    [SerializeField] private bool startShieldActive = true;
    [SerializeField] private float shieldActivationDelay = 1f;  // Delay before shield activates on start
    
    [Header("Shield Damage Zone")]
    [SerializeField] private float damagePerSecond = 8f;
    [SerializeField] private float damageInterval = 0.5f;       // How often to apply damage (seconds)
    [SerializeField] private LayerMask playerLayerMask = -1;    // What layers count as "player"
    [SerializeField] private string playerTag = "Player";       // Player tag for identification
    [SerializeField] private bool enableDamageEffects = true;   // Enable damage visual/audio effects
    
    [Header("Bullet Barrier System")]
    [SerializeField] private LayerMask bulletLayerMask = -1;    // What layers count as "bullets"
    [SerializeField] private string bulletTag = "Bullet";       // Bullet tag for identification
    [SerializeField] private GameObject bulletDestroyEffect;    // Effect when bullet hits shield
    [SerializeField] private bool destroyIncomingBullets = true;
    [SerializeField] private bool allowOutgoingBullets = true;  // Allow bullets fired from inside shield
    
    [Header("Health System (Copy from DroneHealth)")]
    [SerializeField] private float maxHP = 300f;
    [SerializeField] private Slider hpSlider;                   // World space health bar
    [SerializeField] private bool hideBarWhenFull = true;
    
    [Header("Death Effects (Copy from DroneHealth)")]
    [SerializeField] private GameObject deathEffect;            // Explosion effect
    [SerializeField] private int burstCount = 8;                // Number of explosions
    [SerializeField] private float burstRadius = 3f;           // Explosion spread radius
    [SerializeField] private float destroyDelay = 3f;          // Delay before destroying generator
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shieldHitSound;         // When bullet hits shield
    [SerializeField] private AudioClip playerDamageSound;      // When player takes damage in shield
    [SerializeField] private AudioClip shieldDeathSound;       // When shield generator dies
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool debugDrawShieldRange = true;
    [SerializeField] private Color debugShieldColor = Color.cyan;
    
    // Private variables
    private float _currentHP;
    private bool _isDead = false;
    private bool _shieldActive = false;
    private GameObject _shieldSphereInstance;
    private SphereCollider _damageZoneCollider;
    private SphereCollider _bulletBarrierCollider;
    
    // Player damage tracking (similar to TrackDamage.cs)
    private bool _playerInDamageZone = false;
    private Coroutine _damageCoroutine;
    private PlayerDamageManager _playerDamageManager;
    
    void Start()
    {
        InitializeShieldGenerator();
    }
    
    void Update()
    {
        // Health bar updates
        UpdateHealthBar();
    }
    
    #region Initialization
    
    private void InitializeShieldGenerator()
    {
        // Initialize health
        _currentHP = maxHP;
        
        // Setup health bar
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHP;
            hpSlider.value = _currentHP;
            hpSlider.gameObject.SetActive(!hideBarWhenFull);
        }
        
        // Setup audio
        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
        
        // Create colliders for damage zone and bullet barrier
        SetupColliders();
        
        // Activate shield after delay
        if (startShieldActive)
        {
            StartCoroutine(ActivateShieldAfterDelay());
        }
        
        if (enableDebugLogs)
            Debug.Log($"[ShieldGenerator] Initialized with {maxHP} HP, radius: {shieldRadius}");
    }
    
    private void SetupColliders()
    {
        // Create damage zone collider (smaller, for player damage)
        GameObject damageZone = new GameObject("ShieldDamageZone");
        damageZone.transform.SetParent(transform);
        damageZone.transform.localPosition = Vector3.zero;
        damageZone.layer = gameObject.layer;
        
        _damageZoneCollider = damageZone.AddComponent<SphereCollider>();
        _damageZoneCollider.isTrigger = true;
        _damageZoneCollider.radius = shieldRadius * 0.9f; // Slightly smaller than visual shield
        
        // Create bullet barrier collider (larger, for bullet blocking)
        GameObject bulletBarrier = new GameObject("ShieldBulletBarrier");
        bulletBarrier.transform.SetParent(transform);
        bulletBarrier.transform.localPosition = Vector3.zero;
        bulletBarrier.layer = gameObject.layer;
        
        _bulletBarrierCollider = bulletBarrier.AddComponent<SphereCollider>();
        _bulletBarrierCollider.isTrigger = true;
        _bulletBarrierCollider.radius = shieldRadius; // Same size as visual shield
        
        if (enableDebugLogs)
            Debug.Log($"[ShieldGenerator] Created colliders - Damage: {_damageZoneCollider.radius}, Barrier: {_bulletBarrierCollider.radius}");
    }
    
    private IEnumerator ActivateShieldAfterDelay()
    {
        yield return new WaitForSeconds(shieldActivationDelay);
        ActivateShield();
    }
    
    #endregion
    
    #region Shield Management
    
    public void ActivateShield()
    {
        if (_isDead) return;
        
        _shieldActive = true;
        
        // Create shield visual
        if (shieldSpherePrefab && !_shieldSphereInstance)
        {
            _shieldSphereInstance = Instantiate(shieldSpherePrefab, transform.position, Quaternion.identity, transform);
            _shieldSphereInstance.transform.localScale = Vector3.one * (shieldRadius * 2f); // Diameter
        }
        
        // Enable colliders
        if (_damageZoneCollider) _damageZoneCollider.enabled = true;
        if (_bulletBarrierCollider) _bulletBarrierCollider.enabled = true;
        
        if (enableDebugLogs)
            Debug.Log("[ShieldGenerator] Shield activated!");
    }
    
    public void DeactivateShield()
    {
        _shieldActive = false;
        
        // Destroy shield visual
        if (_shieldSphereInstance)
        {
            Destroy(_shieldSphereInstance);
            _shieldSphereInstance = null;
        }
        
        // Disable colliders
        if (_damageZoneCollider) _damageZoneCollider.enabled = false;
        if (_bulletBarrierCollider) _bulletBarrierCollider.enabled = false;
        
        // Stop player damage
        if (_damageCoroutine != null)
        {
            StopCoroutine(_damageCoroutine);
            _damageCoroutine = null;
        }
        _playerInDamageZone = false;
        
        if (enableDebugLogs)
            Debug.Log("[ShieldGenerator] Shield deactivated!");
    }
    
    #endregion
    
    #region Health System (Copied from DroneHealth.cs)
    
    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        
        float previousHP = _currentHP;
        _currentHP = Mathf.Clamp(_currentHP - amount, 0f, maxHP);
        
        if (enableDebugLogs)
            Debug.Log($"[ShieldGenerator] Took {amount} damage. HP: {previousHP:F1} → {_currentHP:F1}");
        
        if (_currentHP <= 0f)
        {
            Die();
        }
    }
    
    private void UpdateHealthBar()
    {
        if (hpSlider)
        {
            hpSlider.value = _currentHP;
            if (hideBarWhenFull)
                hpSlider.gameObject.SetActive(_currentHP < maxHP);
        }
    }
    
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        
        if (enableDebugLogs)
            Debug.Log("[ShieldGenerator] Shield generator destroyed!");
        
        // Deactivate shield
        DeactivateShield();
        
        // Play death sound
        if (audioSource && shieldDeathSound)
            audioSource.PlayOneShot(shieldDeathSound);
        
        // Death explosion effects (copied from DroneHealth.cs)
        if (deathEffect)
        {
            for (int i = 0; i < burstCount; i++)
            {
                Vector3 pos = transform.position + Random.insideUnitSphere * burstRadius;
                pos.y = transform.position.y; // Keep explosions at ground level
                Instantiate(deathEffect, pos, Quaternion.identity);
            }
        }
        
        // Disable colliders to prevent further interactions
        if (_damageZoneCollider) _damageZoneCollider.enabled = false;
        if (_bulletBarrierCollider) _bulletBarrierCollider.enabled = false;
        
        // Destroy after delay
        Destroy(gameObject, destroyDelay);
    }
    
    #endregion
    
    #region Player Damage System (Similar to TrackDamage.cs)
    
    void OnTriggerEnter(Collider other)
    {
        // Handle player entering damage zone
        if (IsPlayerObject(other.gameObject) && _shieldActive && !_isDead)
        {
            _playerInDamageZone = true;
            
            // Get player damage manager
            _playerDamageManager = other.GetComponent<PlayerDamageManager>();
            if (!_playerDamageManager)
                _playerDamageManager = other.GetComponentInParent<PlayerDamageManager>();
            
            // Start continuous damage
            if (_damageCoroutine == null)
                _damageCoroutine = StartCoroutine(DamagePlayerContinuously());
            
            if (enableDebugLogs)
                Debug.Log("[ShieldGenerator] Player entered damage zone!");
        }
        
        // Handle bullet collision (bullet barrier system)
        if (IsBulletObject(other.gameObject) && _shieldActive && !_isDead)
        {
            HandleBulletCollision(other);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Handle player leaving damage zone
        if (IsPlayerObject(other.gameObject))
        {
            _playerInDamageZone = false;
            
            if (_damageCoroutine != null)
            {
                StopCoroutine(_damageCoroutine);
                _damageCoroutine = null;
            }
            
            if (enableDebugLogs)
                Debug.Log("[ShieldGenerator] Player left damage zone!");
        }
    }
    
    private IEnumerator DamagePlayerContinuously()
    {
        while (_playerInDamageZone && _shieldActive && !_isDead && _playerDamageManager)
        {
            // Apply damage using existing player damage system
            bool damageApplied = _playerDamageManager.TakeDamageNoKnockback(
                damagePerSecond * damageInterval, 
                "Shield Generator"
            );
            
            if (damageApplied && enableDamageEffects)
            {
                // Play damage sound
                if (audioSource && playerDamageSound)
                    audioSource.PlayOneShot(playerDamageSound);
            }
            
            yield return new WaitForSeconds(damageInterval);
        }
    }
    
    private bool IsPlayerObject(GameObject obj)
    {
        // Check layer mask
        if (((1 << obj.layer) & playerLayerMask) == 0)
            return false;
        
        // Check tag
        if (!string.IsNullOrEmpty(playerTag) && !obj.CompareTag(playerTag))
            return false;
        
        return true;
    }
    
    #endregion
    
    #region Bullet Barrier System
    
    private void HandleBulletCollision(Collider bulletCollider)
    {
        if (!destroyIncomingBullets) return;
        
        GameObject bullet = bulletCollider.gameObject;
        
        // Check if bullet is moving towards or away from shield center
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb && allowOutgoingBullets)
        {
            Vector3 bulletVelocity = bulletRb.linearVelocity;
            Vector3 toShieldCenter = transform.position - bullet.transform.position;
            
            // If bullet is moving away from shield center, let it pass (outgoing)
            float dotProduct = Vector3.Dot(bulletVelocity.normalized, toShieldCenter.normalized);
            if (dotProduct < 0) // Moving away from center
            {
                if (enableDebugLogs)
                    Debug.Log("[ShieldGenerator] Allowing outgoing bullet to pass");
                return;
            }
        }
        
        // Destroy incoming bullet
        DestroyBullet(bullet, bulletCollider.transform.position);
    }
    
    private void DestroyBullet(GameObject bullet, Vector3 hitPoint)
    {
        if (enableDebugLogs)
            Debug.Log("[ShieldGenerator] Destroying incoming bullet");
        
        // Play shield hit sound
        if (audioSource && shieldHitSound)
            audioSource.PlayOneShot(shieldHitSound);
        
        // Spawn bullet destroy effect
        if (bulletDestroyEffect)
        {
            GameObject fx = Instantiate(bulletDestroyEffect, hitPoint, Quaternion.identity);
            
            // Auto-destroy effect (copied from existing bullet system)
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
            else
                Destroy(fx, 2f);
        }
        
        // Destroy the bullet
        Destroy(bullet);
    }
    
    private bool IsBulletObject(GameObject obj)
    {
        // Check layer mask
        if (((1 << obj.layer) & bulletLayerMask) == 0)
            return false;
        
        // Check tag
        if (!string.IsNullOrEmpty(bulletTag) && !obj.CompareTag(bulletTag))
            return false;
        
        return true;
    }
    
    #endregion
    
    #region Debug & Gizmos
    
    void OnDrawGizmosSelected()
    {
        if (!debugDrawShieldRange) return;
        
        Gizmos.color = debugShieldColor;
        Gizmos.DrawWireSphere(transform.position, shieldRadius);
        
        // Draw damage zone (slightly smaller)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shieldRadius * 0.9f);
    }
    
    #endregion
    
    #region Public Interface
    
    public bool IsShieldActive => _shieldActive && !_isDead;
    public float CurrentHealth => _currentHP;
    public float MaxHealth => maxHP;
    public float ShieldRadius => shieldRadius;
    
    public void SetShieldRadius(float newRadius)
    {
        shieldRadius = newRadius;
        
        // Update colliders
        if (_damageZoneCollider)
            _damageZoneCollider.radius = shieldRadius * 0.9f;
        if (_bulletBarrierCollider)
            _bulletBarrierCollider.radius = shieldRadius;
        
        // Update visual
        if (_shieldSphereInstance)
            _shieldSphereInstance.transform.localScale = Vector3.one * (shieldRadius * 2f);
    }
    
    #endregion
}
