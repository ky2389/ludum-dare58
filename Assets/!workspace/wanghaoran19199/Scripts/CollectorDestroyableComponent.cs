using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CollectorDestroyableComponent : MonoBehaviour
{
    [SerializeField] private string componentName = "ERR";
    [SerializeField] private int numberOfChargePowersNeeded = 8; //how many charge powers needed to disable this component?
    private int _remainingChargePowers;
    private bool _hasBeenDisabled;
    private List<GameObject> _placePointsObjects = new List<GameObject>();
    private GameObject _spawnedTextHint = null;
   
    private void Start()
    {
        _remainingChargePowers = numberOfChargePowersNeeded;
        
        gameObject.GetComponent<MeshRenderer>().enabled = false;
        
        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            if (child.gameObject.GetComponent<ExplosivePlacePoint>())
            {
                _placePointsObjects.Add(child.gameObject);
            }
        }
        
        //StopAllDestroyedFX();
    }

    private void Update()
    {
        if (_remainingChargePowers <= 0)
        {
            if (!_hasBeenDisabled)
            {
                DestroyThisComponent();
                Debug.Log("Component has been disabled");
            }
        }
    }

    private IEnumerator DestroyPlacePoints()
    {
        yield return new WaitForSeconds(2.2f);
        foreach (GameObject placePointObject in _placePointsObjects)
        {
            Destroy(placePointObject);
        }
    }
    
    private void DestroyAllPlacePoints()
    {
        StartCoroutine(DestroyPlacePoints());
    }
    
    private void DisplayAllDestroyedFX()
    {
        Debug.Log("All VFX displayed");
        foreach (Transform child in transform)
        {
            //Debug.Log(child.name);
            foreach (Transform child1 in child.transform)
            {
                //Debug.Log(child1.name);
                if (child1.name.Contains("FX"))
                {
                    child1.gameObject.GetComponent<ParticleSystem>().Play();
                }
            }
        }
    }
    
    private void StopAllDestroyedFX()
    {
        foreach (Transform child in transform)
        {
            //Debug.Log(child.name);
            foreach (Transform child1 in child.transform)
            {
                //Debug.Log(child1.name);
                if (child1.name.Contains("FX"))
                {
                    child1.gameObject.GetComponent<ParticleSystem>().Stop();
                }
            }
        }
    }
    

    #region publically accessible functions

    public void DestroyThisComponent()
    {
        if (!_hasBeenDisabled)
        {
            _hasBeenDisabled = true;
            DestroyAllPlacePoints();
            DestroyTextHint();
            DisplayAllDestroyedFX();   
        }
    }
    
    public void DisableThisComponent()
    {
        if (!_hasBeenDisabled)
        {
            _hasBeenDisabled = true;
            DestroyAllPlacePoints();
            DestroyTextHint();   
        }
    }

    public void DealDamageToComponent(int powerOfCharge)
    {
        _remainingChargePowers -= powerOfCharge;
        //Debug.Log(powerOfCharge);
    }

    public void SpawnTextHintForScan(GameObject textObjectPrefab, float yPos)
    {
        Vector3 spawnPosition = new Vector3(transform.position.x, yPos+2f , transform.position.z);
        
        _spawnedTextHint = Instantiate(textObjectPrefab, spawnPosition, Quaternion.identity);
        _spawnedTextHint.GetComponent<TextMeshPro>().text=componentName;
        _spawnedTextHint.transform.SetParent(transform);
    }

    public void DestroyTextHint()
    {
        if (_spawnedTextHint)
        {
            Destroy(_spawnedTextHint);
        }

        _spawnedTextHint = null;
    }

    public bool GetHasBeenDisabled()
    {
        return _hasBeenDisabled;
    }
    
    #endregion
}
