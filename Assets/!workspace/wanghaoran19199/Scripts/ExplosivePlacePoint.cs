using System;
using System.Threading.Tasks;
using UnityEngine;


public enum ChargeTypes
{
    Regular = 0,
    EMP = 1
}


public class ExplosivePlacePoint : MonoBehaviour
{
    private int[] _chargePowerValues = {2,1}; //power of each type of charge
    
    
    [SerializeField] private Material chargeSemiTransparentMaterial;
    //[SerializeField] [Range(0f,1f)] private float semiTransparentChargeTransparency = 0.45f;
    [Range(0f,1f)] private float semiTransparentChargeTransparency = 0.2f;
    
    [NonSerialized] public bool SemiTransparentChargeDisplayed = false;
    private bool _chargePlaced = false;
    private ChargeTypes _chargeType;
    private GameObject _semiTransparentTemporaryChargeObject, _chargeObject;
    private CollectorDestroyableComponent _parentComponentScript; //script of the parent


    private void Start()
    {
        GetComponent<MeshRenderer>().enabled = false;
        
        if (semiTransparentChargeTransparency > 1f || semiTransparentChargeTransparency < 0f)
        {
            Debug.LogError("Transparency value invalid!");
        }

        _parentComponentScript = transform.parent.gameObject.GetComponent<CollectorDestroyableComponent>();
        if (!_parentComponentScript)
        {
            Debug.LogError("Script not properly attached to parent!");
        }
    }


    #region Display semi-transparent charge object for guidance before it is actually placed
    
    public void DisplaySemiTransparentChargeBeforePlacement(GameObject chargePrefab)
    {
        if (!SemiTransparentChargeDisplayed)
        {
            _semiTransparentTemporaryChargeObject = Instantiate(chargePrefab, transform.position, Quaternion.identity);
            SetTemporaryChargeMaterialSemiTransparent();
            SemiTransparentChargeDisplayed = true;
        }
    }

    public void StopDisplaySemiTransparentCharge()
    {
        if (!SemiTransparentChargeDisplayed || !_semiTransparentTemporaryChargeObject)
        {
            Debug.LogError("Stop display semi-transparent charge: error occurred!");
            return;
        }
        else
        {
            Destroy(_semiTransparentTemporaryChargeObject);
            _semiTransparentTemporaryChargeObject = null;
            SemiTransparentChargeDisplayed=false;
        }
    }

    public void SwitchSemiTransparentChargeDisplayed(GameObject chargePrefab)
    {
        if (!SemiTransparentChargeDisplayed || !_semiTransparentTemporaryChargeObject)
        {
            Debug.LogError("Switch display semi-transparent charge: error occurred!");
            return;
        }
        else
        {
            Destroy(_semiTransparentTemporaryChargeObject);
            _semiTransparentTemporaryChargeObject = Instantiate(chargePrefab, transform.position, Quaternion.identity);
            SetTemporaryChargeMaterialSemiTransparent();
        }
    }

    private void SetTemporaryChargeMaterialSemiTransparent()
    {
        if (_semiTransparentTemporaryChargeObject != null)
        {
            try
            {
                Renderer rend =  _semiTransparentTemporaryChargeObject.GetComponent<Renderer>();


                rend.material = chargeSemiTransparentMaterial;
                // Material mat = rend.material; // unique instance
                //
                // mat.name = "NewSemiTransparent";
                //
                // // Set to Transparent Surface Type (usually only _Surface is not enough)
                // mat.SetFloat("_Surface", 1f); // 1 = Transparent
                // mat.SetOverrideTag("RenderType", "Transparent");
                // mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                // mat.DisableKeyword("_SURFACE_TYPE_OPAQUE"); // Disable Opaque keyword
                // mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); // Enable Transparent keyword
                //
                // // Set to Alpha Blend Mode (your _Blend=0 is correct for Alpha)
                // mat.SetFloat("_Blend", 0f); // 0 = Alpha (or 1 for Premultiply, 2 for Additive, etc.)
                // mat.SetInt("_ZWrite", 0); // Disable ZWrite for blending
                // mat.SetInt("_ZTest", 4); // ZTest Less Equal (common for transparent)
                // mat.SetFloat("_Cull", 2f); // Cull Back (default)
                //
                // // Apply alpha to color
                // Color c = mat.color;
                // c.a = semiTransparentChargeTransparency;
                // mat.color = c;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return;
            }
        }
        else
        {
            Debug.LogError("Semi-transparent temporary charge object is null!");
            return;
        }
    }
    
    #endregion
    
    public void PlaceCharge(ChargeTypes chargeType, GameObject chargePrefab)
    {
        if (!_chargePlaced)
        {
            if ((int)chargeType<System.Enum.GetValues(typeof(ChargeTypes)).Length)
            {
                _chargeObject = Instantiate(chargePrefab, transform.position, Quaternion.identity);  
                _chargeType=chargeType;
                _chargePlaced=true;
                return;
            }
            
            Debug.LogError("Unknown charge type!");
            return;
        }
        
    }

    public async void DetonateCharge()
    {
        if (!_chargePlaced || _chargeObject == null)
        {
            Debug.LogError("Charge not placed!");
            return;
        }
        else
        {
            _chargeObject.GetComponent<MeshRenderer>().enabled = false;
            
            foreach (Transform child in _chargeObject.transform)
            {
                if (child.name.Contains("FX"))
                {
                    child.GetComponent<ParticleSystem>().Play();
                }
            }
            
            _parentComponentScript.DealDamageToComponent(_chargePowerValues[(int)_chargeType]);

            await Task.Delay(2000);
            
            Destroy(_chargeObject);

            await Task.Delay(100);
            Destroy(gameObject);
        }
    }

    public bool CheckCanBePlaced()
    {
        return !_chargePlaced;
    }

    private void OnDestroy()
    {
        Destroy(_semiTransparentTemporaryChargeObject);
        Destroy(_chargeObject);
    }
}
