using System;
using System.Collections.Generic;
using INab.WorldScanFX.URP;
using UnityEngine;
using System.Linq;
using TMPro;

public class AvatarScan : MonoBehaviour
{
    [SerializeField] private string beginScanBehavior = "Start Scan";
    [SerializeField] private float intervalBetweenScans = 9f, scanEffectLastTime = 6.5f;
    [SerializeField] private float scanEffectVisibleRadius = 10f;

    private bool _scanActive = false;
    private float _timeSinceLastScan = 30f;
    private ScanFX _scanFXScript;
    
    //for scan effect display
    [SerializeField] private GameObject textPrefab;
    private List<GameObject> _scannedPlacePoints = new List<GameObject>();
    private List<GameObject> _scannedDestroyableComponents = new List<GameObject>();


    private void Start()
    {
        _scanFXScript = GetComponent<ScanFX>();
        if (_scanFXScript == null)
        {
            Debug.LogError("No ScanFX component found!");
        }

        if (!textPrefab.GetComponent<TextMeshPro>())
        {
            Debug.LogError("No TextMeshPro component found!");
        }
    }

    private void Update()
    {
        _timeSinceLastScan+=Time.deltaTime;
        if (_timeSinceLastScan >= intervalBetweenScans * 999f)
        {
            _timeSinceLastScan = intervalBetweenScans;
        }
        
        BeginScan();
        StopScanEffectDisplay();
    }
    
    private void BeginScan()
    {
        if (Input.GetButtonDown(beginScanBehavior))
        {
            if (_timeSinceLastScan >= intervalBetweenScans)
            {
                //Debug.Log("Begin Scan");
                _scanActive = true;
                _timeSinceLastScan = 0f;
                
                _scanFXScript.StartScan(1);
                
                
                Collider[] hits = Physics.OverlapSphere(transform.position, scanEffectVisibleRadius);
            
            List<GameObject> newScannedPlacePoints = new List<GameObject>();
            List<GameObject> newScannedDestroyableComponents = new List<GameObject>();

            foreach (Collider hit in hits)
            {
                GameObject hitGameobject = hit.gameObject;
                var placePointObject = hitGameobject.GetComponent<ExplosivePlacePoint>();
                if (placePointObject) //object is place point
                {
                    hitGameobject.GetComponent<MeshRenderer>().enabled = true;
                    newScannedPlacePoints.Add(hitGameobject);
                }
                else
                {
                    var destroyableComponentModule = hitGameobject.GetComponent<CollectorDestroyableComponent>();
                    if (destroyableComponentModule) //object is destroyable component
                    {
                        hitGameobject.GetComponent<MeshRenderer>().enabled = true;
                        newScannedDestroyableComponents.Add(hitGameobject);
                        
                        hitGameobject.GetComponent<CollectorDestroyableComponent>().SpawnTextHintForScan(textPrefab,transform.position.y);
                        
                        // if (!_scannedDestroyableComponents.Contains(hitGameobject))
                        // {
                        //     //spawn text hint
                        //     hitGameobject.GetComponent<CollectorDestroyableComponent>().SpawnTextHintForScan(textPrefab,transform.position.y);
                        // }
                    }
                }
            }
            
            // List<GameObject> noLongerScannedPlacePoints = _scannedPlacePoints.Except(newScannedPlacePoints).ToList();
            // List<GameObject> noLongerScannedDestroyableComponents = _scannedDestroyableComponents.Except(newScannedDestroyableComponents).ToList();
            //
            // foreach (var item in noLongerScannedPlacePoints)
            // {
            //     item.GetComponent<MeshRenderer>().enabled = false;
            // }
            //
            // foreach (var item in noLongerScannedDestroyableComponents)
            // {
            //     item.GetComponent<MeshRenderer>().enabled = false;
            //
            //     if (!_scannedDestroyableComponents.Contains(item))
            //     {
            //         item.GetComponent<CollectorDestroyableComponent>().SpawnTextHintForScan(textPrefab,transform.position.y);
            //     }
            // }


            _scannedPlacePoints = newScannedPlacePoints;
            _scannedDestroyableComponents = newScannedDestroyableComponents;
            }
        }
        
        
    }

    private void StopScanEffectDisplay()
    {
        if (_timeSinceLastScan >= scanEffectLastTime)
        {
            if (_scanActive)
            {
                foreach (var item in _scannedPlacePoints)
                {
                    item.GetComponent<MeshRenderer>().enabled = false;
                }

                foreach (var item in _scannedDestroyableComponents)
                {
                    item.GetComponent<MeshRenderer>().enabled = false;
                    item.GetComponent<CollectorDestroyableComponent>().DestroyTextHint();
                }
                
                _scanActive = false;
            }
            
            
        }
    }

    
}
