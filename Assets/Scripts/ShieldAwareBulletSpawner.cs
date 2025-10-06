using UnityEngine;

/// <summary>
/// Component that makes bullet spawning aware of nearby shields.
/// Add this to the player or weapon to automatically mark bullets fired from inside shields.
/// </summary>
public class ShieldAwareBulletSpawner : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 50f;
    [SerializeField] private LayerMask shieldLayerMask = -1;
    
    /// <summary>
    /// Call this when a bullet is spawned to check if it should be marked as outgoing
    /// </summary>
    public void OnBulletSpawned(GameObject bullet, Vector3 spawnPosition)
    {
        // Find all shield generators in range
        Collider[] nearbyColliders = Physics.OverlapSphere(spawnPosition, detectionRadius, shieldLayerMask);
        
        foreach (var collider in nearbyColliders)
        {
            ShieldGenerator shield = collider.GetComponent<ShieldGenerator>();
            if (shield != null && shield.IsShieldActive)
            {
                // Check if the spawn position is inside this shield
                if (shield.IsPositionInsideShield(spawnPosition))
                {
                    Debug.Log($"[ShieldAwareBulletSpawner] Bullet spawned inside shield, marking as outgoing");
                    shield.MarkBulletAsOutgoing(bullet);
                    break; // Only need to mark once
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
