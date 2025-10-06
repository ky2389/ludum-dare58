//attached to the avatar, enables placement of charges
//also manages which type of charge is being equipped and will be placed
//also manages charge inventory & display of this inventory

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.Events;
using UnityEngine;



public class AvatarPlaceCharge_NewVisualsV2 : MonoBehaviour
{
    // enum ChargeTypes
    // {
    //     Regular = 0,
    //     EMP = 1
    // }
    
    //charge placement
    //note: assumes placed on avatar, so transform is avatar's transform
    [Header("Input Action Names")]
    [SerializeField] private string switchChargeTypeBehavior = "Switch Charge Type";
    [SerializeField] private string placeChargeBehavior = "Place Charge"; //input name for placing charge behavior
    [SerializeField] private string detonationChargeBehavior = "Detonate Charge";
    //[SerializeField] private string chargePlacePointTagName = "Charge_PlacePoint";
    [Header("Values for Interaction")]
    [SerializeField] private float placeRadius = 3f;
    //[SerializeField] private float lookAtPLacePointMaxAngle = 45f; //max angle between player and looked at object to make it considered looked at
    [SerializeField] private float detonationTriggerHoldButtonTimeCap = 2f;
    [SerializeField] private float chargeDamageSelfMaxRadius = 7f;
    [Header("Prefabs for Explosive Charges")]
    [SerializeField] private GameObject[] chargeObjectPrefabs; //number at index refers to number of charges of each type in inventory, defined in the enum, so the 0th element is the prefab for regular charges
    
    private PlayerDamageManager _playerDamageManager;
    private GameObject _nearestPlacePoint;
    private ChargeTypes _currentlyEquippedChargeType = ChargeTypes.Regular;
    private List<GameObject> _placePointsWithAPlacedCharge=new List<GameObject>();
    private float _detonateButtonHoldTime = 0f;
    
    
    //inventory display
    private int[] _chargeNumbers = {10, 0}; //number at index refers to number of charges of each type in inventory, defined in the enum, so the 0th element is the number of regular charges
    public int RegularChargeCount => _chargeNumbers[(int)ChargeTypes.Regular];
    public int EMPChargeCount => _chargeNumbers[(int)ChargeTypes.EMP];
    public bool HasAnyAmmo => (RegularChargeCount > 0) || (EMPChargeCount > 0);

    [Header("Events")]
    public UnityEvent OnInventoryChanged = new UnityEvent();
    public bool _EMPIsUnlocked = false;

    [Header("TMP Displays")]
    //[SerializeField] private TextMeshProUGUI currentChargeTypeText;
    [SerializeField]
    private GameObject[] currentlyEquippedChargesIcons;
    [SerializeField] private TextMeshProUGUI regularChargeNumberText;
    [SerializeField] private GameObject EMPDisplay;
    [SerializeField] private TextMeshProUGUI EMPChargeNumberText;
    [SerializeField] private TextMeshProUGUI placeChargePrompt, detonateChargePrompt;


    private void Start()
    {
        if (currentlyEquippedChargesIcons.Length != 2)
        {
            Debug.LogError("Incorrect number of charge display objects");
        }
        
        _playerDamageManager=GetComponentInParent<PlayerDamageManager>();
        if (!_playerDamageManager)
        {
            Debug.LogError("No player damage manager found");
        }
    }


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
        if (_currentlyEquippedChargeType == ChargeTypes.Regular)
        {
            currentlyEquippedChargesIcons[0].SetActive(true);
            currentlyEquippedChargesIcons[1].SetActive(false);
        }
        else if (_currentlyEquippedChargeType == ChargeTypes.EMP)
        {
            currentlyEquippedChargesIcons[0].SetActive(false);
            currentlyEquippedChargesIcons[1].SetActive(true);
        }
        
        if (_EMPIsUnlocked)
        {
            EMPDisplay.SetActive(true);
            // _chargeNumbers[(int)ChargeTypes.EMP]=1;
            // if (EMPChargeNumberText)
            //     EMPChargeNumberText.enabled = true;
        }
        else
        {
            EMPDisplay.SetActive(false);
            // if (EMPChargeNumberText)
            //     EMPChargeNumberText.enabled = false;
        }

        if (placeChargePrompt)
            placeChargePrompt.enabled = _nearestPlacePoint;

        if (detonateChargePrompt)
            detonateChargePrompt.enabled = _placePointsWithAPlacedCharge.Count > 0;
        
        if (regularChargeNumberText != null)
            regularChargeNumberText.text = _chargeNumbers[(int)ChargeTypes.Regular].ToString();
        if (EMPChargeNumberText != null)
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
        
        
        //change display of semi-transparent charge on place point
        if (_nearestPlacePoint)
        {
            if (_nearestPlacePoint.GetComponent<ExplosivePlacePoint>())
            {
                _nearestPlacePoint.GetComponent<ExplosivePlacePoint>()
                    .SwitchSemiTransparentChargeDisplayed(chargeObjectPrefabs[(int)_currentlyEquippedChargeType]);
            }
            else
            {
                Debug.LogError("Script not properly attached to place point!");
                return;
            }
        }
    }
    
    #endregion


    #region Highlight nearest place point in range
    
    //now actually displays a semi-transparent model of the charge to be placed
    private void HighlightNearestPlacePointInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, placeRadius);

        if (hits.Length <= 0)
        {
            DisableSemiTransparentChargeDisplayOnPlacePoint();
            return;
        }
        else
        {
            Transform nearest = null;
            float minDist = Mathf.Infinity;

            foreach (var hit in hits)
            {
                if (hit.gameObject.GetComponent<ExplosivePlacePoint>()) //see if script is properly attached to this place point
                {
                    if (hit.gameObject.GetComponent<ExplosivePlacePoint>().CheckCanBePlaced())
                    {
                        float dist = (hit.transform.position - transform.position).sqrMagnitude;
                        if (dist < minDist - 0.1f)
                        {
                            minDist = dist;
                            nearest = hit.transform;
                        }
                    }
                }
            }

            if (!nearest) //no place point in range
            {
                DisableSemiTransparentChargeDisplayOnPlacePoint();
                return;
            }
            
            //else, there is a place point in range
            if (_nearestPlacePoint && nearest != _nearestPlacePoint)
            {
                DisableSemiTransparentChargeDisplayOnPlacePoint();
            }
            
            _nearestPlacePoint= nearest.gameObject;
            _nearestPlacePoint.GetComponent<ExplosivePlacePoint>()
                .DisplaySemiTransparentChargeBeforePlacement(chargeObjectPrefabs[(int)_currentlyEquippedChargeType]);
        }
    }

    private void DisableSemiTransparentChargeDisplayOnPlacePoint()
    {
        if (!_nearestPlacePoint)
        {
            //Debug.LogError("Place point not found!");
            return;
        }
        else
        {
            _nearestPlacePoint.GetComponent<ExplosivePlacePoint>().StopDisplaySemiTransparentCharge();
            _nearestPlacePoint = null;
        }
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
                        .PlaceCharge(_currentlyEquippedChargeType, chargeObjectPrefabs[(int)_currentlyEquippedChargeType]);
                    
                    _placePointsWithAPlacedCharge.Add(_nearestPlacePoint);
                    _chargeNumbers[(int)_currentlyEquippedChargeType] -= 1;
                    OnInventoryChanged.Invoke();
                    
                    // Material mat = _nearestPlacePoint.gameObject.GetComponent<MeshRenderer>().material;
                    // Color c = mat.color;
                    // c.a = 0;
                    // mat.color = c;
                    _nearestPlacePoint.GetComponent<MeshRenderer>().enabled = false;
                    DisableSemiTransparentChargeDisplayOnPlacePoint(); //visuals
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

                if (_detonateButtonHoldTime >= detonationTriggerHoldButtonTimeCap) //held long enough, detonate
                {
                    _detonateButtonHoldTime = 0f;

                    foreach (GameObject placePointObject in _placePointsWithAPlacedCharge)
                    {
                        placePointObject.GetComponent<ExplosivePlacePoint>().DetonateCharge();
                        
                        //see if player is too close to the charge to be damaged
                        if (Vector3.Distance(transform.position, placePointObject.transform.position) <=
                            chargeDamageSelfMaxRadius)
                        {
                            _playerDamageManager.TakeDamageNoKnockback(5f, "Explosive Placed by Self");
                        }
                    }
                    
                    _placePointsWithAPlacedCharge.Clear();
                    
                    //shake camera
                    if (GetComponent<CameraShake0>())
                    {
                        GetComponent<CameraShake0>().ResetShakeState();
                    }
                    else
                    {
                        Debug.LogError("ShakeCamera script not attached to avatar!");
                    }
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
