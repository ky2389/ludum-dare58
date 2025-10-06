using UnityEngine;

/// <summary>
/// Component that handles player damage when entering shield area
/// </summary>
public class ShieldDamageZone : MonoBehaviour
{
    private ShieldGenerator _shieldGenerator;
    
    public void Initialize(ShieldGenerator shieldGenerator)
    {
        _shieldGenerator = shieldGenerator;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_shieldGenerator)
            _shieldGenerator.OnPlayerEnterDamageZone(other);
    }
    
    void OnTriggerExit(Collider other)
    {
        if (_shieldGenerator)
            _shieldGenerator.OnPlayerExitDamageZone(other);
    }
}
