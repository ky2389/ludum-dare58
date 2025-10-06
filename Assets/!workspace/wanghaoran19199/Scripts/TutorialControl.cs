using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialControl : MonoBehaviour
{
    [SerializeField] private FullscreenInfoControl infoComponent;
    [SerializeField] private string sceneName;

    public GameObject demoComponent;

    private int _stage = 0;
    private bool _isSeeingInfo=false;


    private void Update()
    {
        //Debug.Log(_stage);
        Step0();
        Step1();
        Step2();
    }

    private async void Step0()
    {
        bool infoDismissed = !infoComponent.GetIsActive();
       
        
        if (_stage == 0)
        {   
            
            if (!_isSeeingInfo)
            {
                
                infoComponent.ActivateAndSetText("Welcome to the Tutorial! If you wish to learn the game controls, please continue by pressing X, but if you wish to skip, please press J.");
                _isSeeingInfo=true;
            }
            else
            {
                
                if (!_isSeeingInfo)
                {
                    if (Input.GetKeyDown(KeyCode.J))
                    {
                        SceneManager.LoadScene(sceneName);
                    }
                }
                
                if (infoDismissed)
                {
                    await Task.Delay(1500);
                    _isSeeingInfo = false;
                    _stage++;
                }
            }
            
            
        }
    }

    private async void Step1()
    {
        bool infoDismissed = !infoComponent.GetIsActive();

        if (_stage == 1)
        {
            if (!_isSeeingInfo)
            {
                infoComponent.ActivateAndSetText("Use WASD to move, the right mouse button to aim, the left mouse button to shoot, the spacebar to jump, like any regular game. You can also use F to fly.");
                _isSeeingInfo=true;
            }
            
            if (infoDismissed)
            {
                await Task.Delay(1500);
                _isSeeingInfo = false;
                _stage++;
            }
        }
    }
    
    private async void Step2()
    {
        bool infoDismissed = !infoComponent.GetIsActive();

        if (_stage == 2)
        {
            if (!_isSeeingInfo)
            {
                infoComponent.ActivateAndSetText("Importantly, you must use explosive charges to destroy the gigantic collectors plundering resources in your homeland. Use Q to activate the scanner to see components destroyable by charges; use E to place charges, hold G to detonate all placed charges");
                _isSeeingInfo=true;
            }
            
            if (infoDismissed)
            {
                await Task.Delay(1500);
                _isSeeingInfo = false;
                _stage++;
            }
        }
    }
}
