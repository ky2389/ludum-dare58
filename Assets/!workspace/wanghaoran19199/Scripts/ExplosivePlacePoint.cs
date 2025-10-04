using UnityEngine;

public class ExplosivePlacePoint : MonoBehaviour
{

    [SerializeField] private GameObject regularChargePrefab;
    [SerializeField] private GameObject EMPChargePrefab;

    private bool _chargePlaced = false;
    private GameObject _chargeObject;


    public void PlaceCharge(string chargeType)
    {
        if (!_chargePlaced)
        {
            if (chargeType == "Regular")
            {
                _chargeObject = Instantiate(regularChargePrefab, transform.position, Quaternion.identity);  
                _chargePlaced=true;
                return;
            }
            else if (chargeType == "EMP")
            {
                _chargeObject = Instantiate(EMPChargePrefab, transform.position, Quaternion.identity);
                _chargePlaced=true;
                return;
            }
        
            Debug.LogError("Unknown charge type!");
            return;
        }
        
    }

    public void DetonateCharge()
    {
        if (!_chargePlaced || _chargeObject == null)
        {
            Debug.LogError("Charge not placed!");
            return;
        }
        else
        {
            Destroy(_chargeObject);
            Debug.Log("Charge detonated!");
            //TODO
        }
    }

}
