using System;
using UnityEngine;
using Random = UnityEngine.Random;


public class CollectorDisableStateControl_TypeAlpha : MonoBehaviour
{
    [SerializeField] private CollectorDestroyableComponent[] locomotionObjects;
    [SerializeField] private CollectorDestroyableComponent[] fuelTankObjects;
    [SerializeField] private CollectorDestroyableComponent computerRoomObject;
    [SerializeField] private CollectorDestroyableComponent cargoHoldObject;

    [SerializeField] private GameObject rewardObject;

    private bool _isDisabled = false, _isFullyDestroyed = false, _canReceiveRewards = true;
    
    //control collector movement
    private CollectorController  _collectorController;
    private bool _collectorNeedStop;
    private float _collectorCurrentSpeed;


    private void Start()
    {
        rewardObject.SetActive(false);
        
        if (locomotionObjects.Length < 1 || locomotionObjects.Length > 2)
        {
            Debug.LogError("Wrong number of locomotion objects");
        }

        _collectorController = transform.parent.GetComponent<CollectorController>();
        if (!_collectorController)
        {
            Debug.LogError("No collectorController found on parent collector!");
        }
        _collectorCurrentSpeed=_collectorController.speed;
    }


    private void Update()
    {
        CheckDestructionState();
        MakeCollectorStop();
    }

    private void CheckDestructionState()
    {
        if (!_isFullyDestroyed)
        {
            
            if (!_isDisabled)
            {
                if (computerRoomObject.GetHasBeenDisabled()) //main computer disabled
                {
                    _isDisabled = true;
                    DisableAllComponents();
                    EnableRewards();
                    _collectorNeedStop = true;
                }
                else //locomotion components
                {
                    if (locomotionObjects[0].GetHasBeenDisabled() && locomotionObjects[1].GetHasBeenDisabled())
                    {
                        _isDisabled = true;
                        DisableAllComponents();
                        EnableRewards();
                        _collectorNeedStop = true;
                    }
                }
                
                foreach (var fuelTank in fuelTankObjects)
                {
                    if (fuelTank.GetHasBeenDisabled())
                    {
                        _isFullyDestroyed = true;
                        _canReceiveRewards=false;
                        DestroyAllComponents();
                        _collectorNeedStop = true;
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

    #region collector movement control

    private void MakeCollectorStop()
    {
        if (_collectorNeedStop)
        {
            // if (_collectorCurrentSpeed > 0)
            // {
            //     _collectorCurrentSpeed -= Random.Range(0.1f, 1f)*Time.deltaTime;
            //     _collectorController.speed=_collectorCurrentSpeed;
            // }
            _collectorController.StartGradualStop();
            
        }
    }

    #endregion

    #region destroy or disable components
    
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
    
    private void DisableAllComponents()
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
    
    #endregion
}
