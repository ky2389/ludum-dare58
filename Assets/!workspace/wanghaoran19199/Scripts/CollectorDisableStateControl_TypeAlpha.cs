using System;
using UnityEngine;

public class CollectorDisableStateControl_TypeAlpha : MonoBehaviour
{
    [SerializeField] private CollectorDestroyableComponent[] locomotionObjects;
    [SerializeField] private CollectorDestroyableComponent[] fuelTankObjects;
    [SerializeField] private CollectorDestroyableComponent computerRoomObject;
    [SerializeField] private CollectorDestroyableComponent cargoHoldObject;

    [SerializeField] private GameObject rewardObject;

    private bool _isDisabled = false, _isFullyDestroyed = false, _canReceiveRewards = true;


    private void Start()
    {
        rewardObject.SetActive(false);
        
        if (locomotionObjects.Length < 1 || locomotionObjects.Length > 2)
        {
            Debug.LogError("Wrong number of locomotion objects");
        }
    }


    private void Update()
    {
        CheckDestructionState();
    }

    private void CheckDestructionState()
    {
        if (!_isFullyDestroyed)
        {
            
            if (!_isDisabled)
            {
                if (computerRoomObject.GetHasBeenDisabled())
                {
                    _isDisabled = true;
                    DestroyAllComponents();
                    EnableRewards();
                }
                else //locomotion components
                {
                    if (locomotionObjects[0].GetHasBeenDisabled() && locomotionObjects[1].GetHasBeenDisabled())
                    {
                        _isDisabled = true;
                        DestroyAllComponents();
                        EnableRewards();
                    }
                }
                
                foreach (var fuelTank in fuelTankObjects)
                {
                    if (fuelTank.GetHasBeenDisabled())
                    {
                        _isFullyDestroyed = true;
                        _canReceiveRewards=false;
                        DestroyAllComponents();
                        break;
                    }
                }

                if (cargoHoldObject.GetHasBeenDisabled())
                {
                    _canReceiveRewards = false;
                }
            }
            
        }
    }
    
    private void EnableRewards()
    {
        if (_isDisabled && !_isFullyDestroyed && _canReceiveRewards)
        {
            rewardObject.SetActive(true);
            Debug.Log("Reward unlocked!");
        }
    }


    private void DestroyAllComponents()
    {
        foreach (var fuelTank in fuelTankObjects)
        {
            fuelTank.DestroyThisComponent();
        }

        foreach (var locomotionObject in locomotionObjects)
        {
            locomotionObject.DestroyThisComponent();
        }
        
        computerRoomObject.DestroyThisComponent();
        cargoHoldObject.DestroyThisComponent();
    }
    
    private void DsiableAllComponents()
    {
        foreach (var fuelTank in fuelTankObjects)
        {
            fuelTank.DisableThisComponent();
        }

        foreach (var locomotionObject in locomotionObjects)
        {
            locomotionObject.DisableThisComponent();
        }

        computerRoomObject.DisableThisComponent();
        cargoHoldObject.DisableThisComponent();
    }
}
