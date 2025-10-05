using UnityEngine;

/// <summary>
/// Test script for Health and Energy systems. 
/// Attach to any GameObject with HealthSystem and/or EnergySystem to test functionality.
/// Use keyboard inputs to trigger various test scenarios.
/// </summary>
public class HealthEnergyTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool enableKeyboardTesting = true;
    [SerializeField] private float testDamageAmount = 20f;
    [SerializeField] private float testHealAmount = 15f;
    [SerializeField] private float testEnergyDrain = 25f;
    [SerializeField] private float testEnergyRestore = 30f;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugGUI = true;
    
    private HealthSystem healthSystem;
    private EnergySystem energySystem;
    
    void Start()
    {
        // Get system references
        healthSystem = GetComponent<HealthSystem>();
        energySystem = GetComponent<EnergySystem>();
        
        if (healthSystem == null && energySystem == null)
        {
            Debug.LogWarning($"[HealthEnergyTester] No HealthSystem or EnergySystem found on {gameObject.name}. This tester won't do anything.");
        }
        else
        {
            Debug.Log($"[HealthEnergyTester] Test script initialized on {gameObject.name}. " +
                     $"Health: {(healthSystem != null ? "Y" : "N")}, Energy: {(energySystem != null ? "Y" : "N")}");
            
            if (enableKeyboardTesting)
            {
                Debug.Log("[HealthEnergyTester] Keyboard controls enabled:\n" +
                         "Health: [1] Damage, [2] Heal, [3] Kill, [4] Revive\n" +
                         "Energy: [Q] Drain, [E] Restore, [R] Deplete, [T] Fill");
            }
        }
    }
    
    void Update()
    {
        if (!enableKeyboardTesting) return;
        
        // Health system tests
        if (healthSystem != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                TestDamage();
            if (Input.GetKeyDown(KeyCode.Alpha2))
                TestHeal();
            if (Input.GetKeyDown(KeyCode.Alpha3))
                TestKill();
            if (Input.GetKeyDown(KeyCode.Alpha4))
                TestRevive();
        }
        
        // Energy system tests
        if (energySystem != null)
        {
            if (Input.GetKeyDown(KeyCode.Q))
                TestEnergyDrain();
            if (Input.GetKeyDown(KeyCode.E))
                TestEnergyRestore();
            if (Input.GetKeyDown(KeyCode.R))
                TestEnergyDeplete();
            if (Input.GetKeyDown(KeyCode.T))
                TestEnergyFill();
        }
    }
    
    // Health system test methods
    public void TestDamage()
    {
        if (healthSystem != null)
        {
            Debug.Log($"[HealthEnergyTester] Testing damage: {testDamageAmount}");
            healthSystem.TakeDamage(testDamageAmount, "Test Script");
        }
    }
    
    public void TestHeal()
    {
        if (healthSystem != null)
        {
            Debug.Log($"[HealthEnergyTester] Testing heal: {testHealAmount}");
            healthSystem.Heal(testHealAmount, "Test Script");
        }
    }
    
    public void TestKill()
    {
        if (healthSystem != null)
        {
            Debug.Log("[HealthEnergyTester] Testing kill");
            healthSystem.Kill("Test Script");
        }
    }
    
    public void TestRevive()
    {
        if (healthSystem != null)
        {
            Debug.Log("[HealthEnergyTester] Testing revive");
            healthSystem.Revive();
        }
    }
    
    // Energy system test methods
    public void TestEnergyDrain()
    {
        if (energySystem != null)
        {
            Debug.Log($"[HealthEnergyTester] Testing energy drain: {testEnergyDrain}");
            energySystem.ConsumeEnergy(testEnergyDrain, "Test Script");
        }
    }
    
    public void TestEnergyRestore()
    {
        if (energySystem != null)
        {
            Debug.Log($"[HealthEnergyTester] Testing energy restore: {testEnergyRestore}");
            energySystem.RestoreEnergy(testEnergyRestore, "Test Script");
        }
    }
    
    public void TestEnergyDeplete()
    {
        if (energySystem != null)
        {
            Debug.Log("[HealthEnergyTester] Testing energy depletion");
            energySystem.SetEnergy(0f);
        }
    }
    
    public void TestEnergyFill()
    {
        if (energySystem != null)
        {
            Debug.Log("[HealthEnergyTester] Testing energy fill");
            energySystem.SetEnergy(energySystem.MaxEnergy);
        }
    }
    
    // Debug GUI for runtime monitoring
    void OnGUI()
    {
        if (!showDebugGUI) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"Health & Energy Tester - {gameObject.name}", "boldlabel");
        
        if (healthSystem != null)
        {
            GUILayout.Space(5);
            GUILayout.Label("HEALTH SYSTEM", "boldlabel");
            GUILayout.Label($"Health: {healthSystem.CurrentHealth:F1} / {healthSystem.MaxHealth:F1}");
            GUILayout.Label($"Percentage: {healthSystem.HealthPercentage:P1}");
            GUILayout.Label($"Status: {(healthSystem.IsAlive ? "Alive" : "Dead")}");
        }
        
        if (energySystem != null)
        {
            GUILayout.Space(5);
            GUILayout.Label("ENERGY SYSTEM", "boldlabel");
            GUILayout.Label($"Energy: {energySystem.CurrentEnergy:F1} / {energySystem.MaxEnergy:F1}");
            GUILayout.Label($"Percentage: {energySystem.EnergyPercentage:P1}");
            GUILayout.Label($"Can Fly: {(energySystem.CanFly ? "Yes" : "No")}");
        }
        
        if (enableKeyboardTesting)
        {
            GUILayout.Space(5);
            GUILayout.Label("CONTROLS", "boldlabel");
            if (healthSystem != null)
                GUILayout.Label("Health: 1=Damage, 2=Heal, 3=Kill, 4=Revive");
            if (energySystem != null)
                GUILayout.Label("Energy: Q=Drain, E=Restore, R=Empty, T=Fill");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
