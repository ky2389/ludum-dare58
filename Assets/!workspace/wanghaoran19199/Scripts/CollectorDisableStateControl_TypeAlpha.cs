using System;
using UnityEngine;

public class CollectorDisableStateControl_TypeAlpha : MonoBehaviour
{
    [SerializeField] private CollectorDestroyableComponent[] locomotionObjects;
    [SerializeField] private CollectorDestroyableComponent[] fuelTankObjects;
    [SerializeField] private CollectorDestroyableComponent computerRoomObject;
    [SerializeField] private CollectorDestroyableComponent cargoHoldObject;

    private bool _isDisabled, _isFullyDestroyed, _canReceiveRewards = true;
    

    private void Update()
    {
        
    }

    private void CheckDestructionState()
    {
        if (!_isFullyDestroyed)
        {
            foreach (var fuelTank in fuelTankObjects)
            {
                if (fuelTank.GetHasBeenDisabled())
                {
                    _isFullyDestroyed = true;
                    _canReceiveRewards=false;
                }
            }

            if (cargoHoldObject.GetHasBeenDisabled())
            {
                _canReceiveRewards = false;
            }

            if (computerRoomObject.GetHasBeenDisabled())
            {
                _isDisabled = true;
            }
        }
    }
}
