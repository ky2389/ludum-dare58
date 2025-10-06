using System;
using TMPro;
using UnityEngine;

public class FullscreenInfoControl : MonoBehaviour
{
    [SerializeField] private string closeInfoBehavior = "UI Confirm";
    [SerializeField] private GameObject infoObject;
    [SerializeField] private TextMeshProUGUI textDisplay;

    private bool _isActive;

    private void Update()
    {
        if (_isActive)
        {
            if (Input.GetButtonDown(closeInfoBehavior))
            {
                DeactivateInfo();
            }
        }
    }


    public void ActivateAndSetText(string info)
    {
        infoObject.SetActive(true);
        textDisplay.text = info;
        _isActive = true;
    }

    private void DeactivateInfo()
    {
        infoObject.SetActive(false);
        _isActive = false;
    }

    public bool GetIsActive()
    {
        return _isActive;
    }
}
