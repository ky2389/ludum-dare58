using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class Rewards_CollectorDisabled : MonoBehaviour
{
    [SerializeField] private UnityEvent[] eventsToInvokeAfterRewardsAreReceived;
    [SerializeField] private TextMeshProUGUI _UIHint;
    [SerializeField] private float interactionRadius = 3.5f;
    [SerializeField] private string collectRewardsBehavior = "Place Charge";

    private GameObject _mainPLayerAvatar;
    private bool _UIHintIsActive = false, _canReceiveRewards = true;


    private void Start()
    {
        _mainPLayerAvatar=GameObject.FindWithTag("Player");
        if (!_mainPLayerAvatar)
        {
            Debug.LogError("No player object found");
        }
        
        _UIHint.gameObject.SetActive(false);
    }

    private void Update()
    {
        DetectPlayerNearby();
    }

    private void DetectPlayerNearby()
    {
        if (Vector3.Distance(transform.position, _mainPLayerAvatar.transform.position) <= interactionRadius)
        {
            if (!_UIHintIsActive)
            {
                _UIHint.gameObject.SetActive(true);
                _UIHintIsActive = true;
            }

            if (_canReceiveRewards && Input.GetButtonDown(collectRewardsBehavior))
            {
                ReceiveAwards();
                _canReceiveRewards = false;
                Destroy(gameObject);
            }
        }
        else
        {
            if (_UIHintIsActive)
            {
                _UIHint.gameObject.SetActive(false);
                _UIHintIsActive = false;
            }
        }
    }

    private void ReceiveAwards()
    {
        foreach (UnityEvent unityEvent in eventsToInvokeAfterRewardsAreReceived)
        {
            unityEvent.Invoke();
        }
    }
}
