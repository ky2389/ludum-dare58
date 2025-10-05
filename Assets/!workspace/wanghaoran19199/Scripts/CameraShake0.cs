using UnityEngine;
using System.Collections;

public class CameraShake0 : MonoBehaviour
{
    // Transform of the camera to shake. Grabs the gameObject's transform
    // if null.
    public Transform camTransform;
	
    // How long the object should shake for.
    public float shakeDuration = 2.5f;
    private float _shakeTimer = 100f;
	
    // Amplitude of the shake. A larger value shakes the camera harder.
    public float originalShakeAmount = 0.45f;
    private float _shakeAmount;
    public float decreaseFactor = 1.0f;
	
    Vector3 originalPos;

    private Vector3 _shakeOffset;
	
    void Awake()
    {
        if (camTransform == null)
        {
            camTransform = GetComponent(typeof(Transform)) as Transform;
        }

        _shakeAmount = originalShakeAmount;
    }
	
    void OnEnable()
    {
        originalPos = camTransform.localPosition;
    }

    void Update()
    {
        if (_shakeTimer < shakeDuration)
        {
            //camTransform.localPosition = originalPos + Random.insideUnitSphere * _shakeAmount;
            _shakeOffset = Random.insideUnitSphere * _shakeAmount;
			
            _shakeTimer += Time.deltaTime * decreaseFactor;
            
            float t = Mathf.Clamp01(_shakeTimer / shakeDuration);
            // Ease-out curve (fast → slow)
            float easedT = 1f - Mathf.Pow(1f - t, 3f); 
            // Interpolate from startValue → 0
            _shakeAmount = Mathf.Lerp(originalShakeAmount, 0f, easedT);
            //Debug.Log(_shakeAmount);
        }
        else
        {
            //camTransform.localPosition = originalPos;
            _shakeOffset = Vector3.zero;
        }
    }

    public void ResetShakeState()
    {
        _shakeTimer = 0f;
        _shakeAmount = originalShakeAmount;

    }

    public Vector3 GetShakeOffset()
    {
        return _shakeOffset;
    }
}