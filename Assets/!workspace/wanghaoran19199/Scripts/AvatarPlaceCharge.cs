//attached to the avatar, enables placement of charges
//also manages which type of charge is being equipped and will be placed
//also manages charge inventory & display of this inventory

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public enum ChargeTypes
{
    Regular = 0,
    EMP = 1
}

public class AvatarPlaceCharge : MonoBehaviour
{
    //charge placement
    //note: assumes placed on avatar, so transform is avatar's transform
    [Header("Input Action Names")]
    [SerializeField] private string switchChargeTypeBehavior = "Switch Charge Type";
    [SerializeField] private string placeChargeBehavior = "Place Charge"; //input name for placing charge behavior
    [SerializeField] private string detonationChargeBehavior = "Detonate Charge";
    [SerializeField] private string chargePlacePointTagName = "Charge_PlacePoint";
    [Header("Values for Interaction")]
    [SerializeField] private float placeRadius = 2f;
    //[SerializeField] private float lookAtPLacePointMaxAngle = 45f; //max angle between player and looked at object to make it considered looked at
    [SerializeField] private float detonationTriggerHoldButtonTimeCap = 2f;

    private GameObject _nearestPlacePoint;
    private ChargeTypes _currentlyEquippedChargeType = ChargeTypes.Regular;
    private List<GameObject> _placePointsWithAPlacedCharge=new List<GameObject>();
    private float _detonateButtonHoldTime = 0f;
    
    
    //inventory display
    private int[] _chargeNumbers = {10, 0}; //number at index refers to number of charges of each type in inventory, defined in the enum, so the 0th element is the number of regular charges
    private bool _EMPIsUnlocked = false;
    [Header("TMP Displays")]
    [SerializeField] private TextMeshProUGUI currentChargeTypeText;
    [SerializeField] private TextMeshProUGUI regularChargeNumberText;
    [SerializeField] private TextMeshProUGUI EMPChargeNameText, EMPChargeNumberText;
    [SerializeField] private TextMeshProUGUI placeChargePrompt, detonateChargePrompt;
    
    
    

    // Update is called once per frame
    void Update()
    {
        SwitchChargeType();
        ChargeDisplayControl();
        HighlightNearestPlacePointInRange();
        PlaceCharge();
        DetonateAllCharges();
    }

    #region UI Display Control

    //controls UI display & charge type switching
    private void ChargeDisplayControl()
    {
        currentChargeTypeText.text = _currentlyEquippedChargeType.ToString();
        
        if (_EMPIsUnlocked)
        {
            EMPChargeNameText.enabled = true;
            EMPChargeNumberText.enabled = true;
        }
        else
        {
            EMPChargeNameText.enabled = false;
            EMPChargeNumberText.enabled = false;
        }

        placeChargePrompt.enabled = _nearestPlacePoint;

        detonateChargePrompt.enabled = _placePointsWithAPlacedCharge.Count > 0;
        
        regularChargeNumberText.text = _chargeNumbers[(int)ChargeTypes.Regular].ToString();
        EMPChargeNumberText.text = _chargeNumbers[(int)ChargeTypes.EMP].ToString();
    }
    
    //switch charge type
    private void SwitchChargeType()
    {
        if (Input.GetButtonDown(switchChargeTypeBehavior))
        {
            int count = System.Enum.GetValues(typeof(ChargeTypes)).Length;
            int next = ((int)_currentlyEquippedChargeType+1) % count;
            _currentlyEquippedChargeType = (ChargeTypes)next;
        }

        if (!_EMPIsUnlocked && _currentlyEquippedChargeType == ChargeTypes.EMP)
        {
            _currentlyEquippedChargeType=ChargeTypes.Regular;
        }
    }
    
    #endregion


    #region Highlight nearest place point in range
    
    private void HighlightNearestPlacePointInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, placeRadius);

        if (hits.Length <= 0)
        {
            if (_nearestPlacePoint)
            {
                TurnOffPlacePointOutline(_nearestPlacePoint);
            }
        }
        else
        {
            Transform nearest = null;
            float minDist = Mathf.Infinity;

            foreach (var hit in hits)
            {
                if (hit.CompareTag(chargePlacePointTagName)) //see if the object is a place point
                {
                    if (!hit.gameObject.GetComponent<ExplosivePlacePoint>() || !hit.gameObject.GetComponent<ManualToggleOutline>()) //see if script is properly attached to this place point
                    {
                        Debug.LogError("Script not properly attached to place point");
                    }
                    else
                    {
                        float dist = (hit.transform.position - transform.position).sqrMagnitude;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearest = hit.transform;
                        }
                    }
                }
            }

            if (!nearest)
            {
                if (_nearestPlacePoint)
                {
                    TurnOffPlacePointOutline(_nearestPlacePoint);   
                }
                return;
            }
            
            _nearestPlacePoint= nearest.gameObject;
            _nearestPlacePoint.GetComponent<ManualToggleOutline>().ToggleOutline();
        }
    }

    private void TurnOffPlacePointOutline(GameObject placePoint)
    {
        if (!placePoint.GetComponent<ManualToggleOutline>())
        {
            Debug.LogError("Script not properly attached to place point");
        }
        else
        {
            placePoint.GetComponent<ManualToggleOutline>().ToggleOutline();
            placePoint.GetComponent<ManualToggleOutline>().SetCannotOutline();
        }
        
        _nearestPlacePoint = null;
    }
    #endregion


    #region Charge placement and detonation
    
    private void PlaceCharge()
    {
        if (_nearestPlacePoint)
        {
            if (Input.GetButtonDown(placeChargeBehavior))
            {
                if (_chargeNumbers[(int)_currentlyEquippedChargeType] > 0)
                {
                    _nearestPlacePoint.GetComponent<ExplosivePlacePoint>()
                        .PlaceCharge(_currentlyEquippedChargeType.ToString());
                    _placePointsWithAPlacedCharge.Add(_nearestPlacePoint);
                    _chargeNumbers[(int)_currentlyEquippedChargeType] -= 1;
                    
                    TurnOffPlacePointOutline(_nearestPlacePoint);
                }
                else
                {
                    Debug.Log("Insufficient charge");
                    //TODO: add visual hint
                }
                
            }
        }
    }

    private void DetonateAllCharges()
    {
        if (_placePointsWithAPlacedCharge.Count > 0)
        {
            if (Input.GetButton(detonationChargeBehavior))
            {
                _detonateButtonHoldTime += Time.deltaTime;

                if (_detonateButtonHoldTime >= detonationTriggerHoldButtonTimeCap)
                {
                    _detonateButtonHoldTime = 0f;

                    foreach (GameObject placePointObject in _placePointsWithAPlacedCharge)
                    {
                        placePointObject.GetComponent<ExplosivePlacePoint>().DetonateCharge();
                    }
                    
                    _placePointsWithAPlacedCharge.Clear();
                }
            }

            if (Input.GetButtonUp(detonationChargeBehavior))
            {
                _detonateButtonHoldTime = 0f;
            }
        }
    }
    
    #endregion

    
    
    #region externally called functions

    public void UnlockEMP()
    {
        _EMPIsUnlocked = true;
    }

    #endregion
}
