using System;
using TMPro;
using UnityEngine;

public class TutorialReopenControl : MonoBehaviour
{
    [SerializeField] private string closeInfoBehavior = "UI Confirm";
    [SerializeField] private GameObject infoObject;
    [SerializeField] private TextMeshProUGUI textDisplay;

    private string info;
    private bool _isActive;

    private void Start()
    {
        info = textDisplay.text;
    }

    private void Update()
    {
        if (_isActive)
        {
            if (Input.GetButtonDown(closeInfoBehavior))
            {
                DeactivateInfo();
            }
        }
        else
        {
            if (Input.GetButtonDown(closeInfoBehavior))
            {
                ActivateAndSetText(info);
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
