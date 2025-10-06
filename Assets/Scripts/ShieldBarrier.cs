using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Component that identifies a shield barrier and handles bullet blocking logic.
/// This component should be added to shield barrier colliders.
/// </summary>
public class ShieldBarrier : MonoBehaviour
{
    private ShieldGenerator _shieldGenerator;
    private HashSet<GameObject> _bulletsToIgnore = new HashSet<GameObject>();
    
    public void Initialize(ShieldGenerator shieldGenerator)
    {
        _shieldGenerator = shieldGenerator;
    }
    
    /// <summary>
    /// Called by bullets when they hit this barrier.
    /// Returns true if the bullet should be blocked, false if it should pass through.
    /// </summary>
    public bool ShouldBlockBullet(GameObject bullet)
    {
        if (!_shieldGenerator || !_shieldGenerator.IsShieldActive)
            return false;
        
        // Get bullet component to access its effects
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent == null)
            return false;
        
        // Check if this bullet should be ignored (fired from inside)
        if (_bulletsToIgnore.Contains(bullet))
        {
            Debug.Log("[ShieldBarrier] Ignoring bullet fired from inside shield");
            _bulletsToIgnore.Remove(bullet); // Clean up
            return false;
        }
        
        // Check if we should allow outgoing bullets
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb && _shieldGenerator.AllowOutgoingBullets)
        {
            Vector3 bulletVelocity = bulletRb.linearVelocity;
            Vector3 toShieldCenter = _shieldGenerator.transform.position - bullet.transform.position;
            
            // Calculate distance from shield center
            float distanceFromCenter = toShieldCenter.magnitude;
            float shieldRadius = _shieldGenerator.ShieldRadius;
            
            // If bullet is moving away from shield center, let it pass (outgoing)
            float dotProduct = Vector3.Dot(bulletVelocity.normalized, toShieldCenter.normalized);
            
            Debug.Log($"[ShieldBarrier] Bullet at distance {distanceFromCenter:F2} from center (radius: {shieldRadius:F2})");
            Debug.Log($"[ShieldBarrier] Bullet velocity: {bulletVelocity}, ToCenter: {toShieldCenter}, DotProduct: {dotProduct:F2}");
            
            // If bullet is close to center (fired from inside) and moving away, let it pass
            if (distanceFromCenter < shieldRadius * 0.8f && dotProduct < 0.3f)
            {
                Debug.Log("[ShieldBarrier] Allowing bullet fired from inside to pass through shield");
                return false;
            }
            
            if (dotProduct < 0) // Moving away from center
            {
                Debug.Log("[ShieldBarrier] Allowing outgoing bullet to pass through shield");
                return false; // Don't block outgoing bullets
            }
            else
            {
                Debug.Log("[ShieldBarrier] Blocking incoming bullet");
            }
        }
        
        // Block incoming bullets - trigger impact effects first
        OnBulletBlocked(bullet, bulletComponent);
        return true;
    }
    
    /// <summary>
    /// Mark a bullet to be ignored (for bullets fired from inside the shield)
    /// </summary>
    public void IgnoreBullet(GameObject bullet)
    {
        _bulletsToIgnore.Add(bullet);
        // Clean up after a short time in case the bullet doesn't hit the barrier
        StartCoroutine(CleanupIgnoredBullet(bullet));
    }
    
    private System.Collections.IEnumerator CleanupIgnoredBullet(GameObject bullet)
    {
        yield return new WaitForSeconds(1f); // Clean up after 1 second
        if (bullet != null && _bulletsToIgnore.Contains(bullet))
        {
            _bulletsToIgnore.Remove(bullet);
        }
    }
    
    private void OnBulletBlocked(GameObject bullet, Bullet bulletComponent)
    {
        if (_shieldGenerator)
        {
            // Create bullet impact effects using the bullet's own impact effect
            Vector3 hitPoint = bullet.transform.position;
            Vector3 hitNormal = (_shieldGenerator.transform.position - hitPoint).normalized;
            
            // Spawn the bullet's own impact effect if it has one
            if (bulletComponent.impactEffect)
            {
                GameObject fx = Instantiate(bulletComponent.impactEffect, hitPoint, Quaternion.LookRotation(hitNormal));
                if (bulletComponent.autodestroyByParticleDuration)
                {
                    var ps = fx.GetComponent<ParticleSystem>();
                    if (ps != null)
                        Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
                    else
                        Destroy(fx, bulletComponent.impactEffectLifetime);
                }
                else
                {
                    Destroy(fx, bulletComponent.impactEffectLifetime);
                }
            }
            
            // Detach and fade the bullet's glow light (same as bullet's CleanupAndDestroy)
            if (bulletComponent.glowLight)
            {
                bulletComponent.glowLight.transform.parent = null;
                Destroy(bulletComponent.glowLight.gameObject, 0.2f);
            }
            
            // Notify shield generator for additional effects
            _shieldGenerator.OnBulletBlocked(hitPoint);
        }
    }
}
