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
    }

    private void Update()
    {
        if (_remainingChargePowers <= 0)
        {
            if (!_hasBeenDisabled)
            {
                StartCoroutine(DestroyPlacePoints());
            
                DestroyTextHint();
                _hasBeenDisabled = true;
                Debug.Log("This component has been disabled!");
                //TODO   
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

    public void DealDamageToComponent(int powerOfCharge)
    {
        _remainingChargePowers -= powerOfCharge;
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
}
