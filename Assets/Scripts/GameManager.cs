using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Scenes")] 
    [SerializeField] private string startMenuScene = "Start Scene";
    [SerializeField] private string loseScene = "Start Scene";
    [SerializeField] private string winScene = "Start Scene";
    [SerializeField] private bool goToNextBuildIndexOnWin = true;
    [SerializeField] private float sceneChangeDelay = 1.0f;

    [Header("Player / Ammo")] 
    [SerializeField] private bool loseOnPlayerDeath = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private HealthSystem playerHealthOverride;
    [SerializeField] private AvatarPlaceCharge_NewVisualsV2 playerCharges;
    [SerializeField] private float outOfAmmoLoseDelaySeconds = 10f;

    [Header("Collectors")] 
    [SerializeField] private CollectorDisableStateControl_TypeAlpha winWhenThisCollectorDisabled;
    [SerializeField] private CollectorDisableStateControl_TypeAlpha[] collectorsLoseWhenTwoFullyDestroyed;

    public UnityEvent OnWin = new UnityEvent();
    public UnityEvent OnLose = new UnityEvent();

    private HealthSystem cachedPlayerHealth;
    private bool isTransitioning;
    private float outOfAmmoStartTime = -1f;
    private int destroyedCollectorsCount;

    void Start()
    {
        TryBindPlayerHealth();
        TryBindPlayerCharges();
        TryBindCollectors();
    }

    void OnDestroy()
    {
        UnsubscribeFromPlayer();
        UnsubscribeFromCharges();
        UnsubscribeFromCollectors();
    }

    void Update()
    {
        EvaluateOutOfAmmoTimer();
    }

    private void TryBindPlayerHealth()
    {
        if (!loseOnPlayerDeath) return;
        if (cachedPlayerHealth != null) return;

        HealthSystem hs = playerHealthOverride;
        if (hs == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) hs = playerObj.GetComponent<HealthSystem>();
        }

        if (hs == null) return;
        cachedPlayerHealth = hs;
        cachedPlayerHealth.OnDeath.AddListener(HandlePlayerDeath);
    }

    private void UnsubscribeFromPlayer()
    {
        if (cachedPlayerHealth == null) return;
        cachedPlayerHealth.OnDeath.RemoveListener(HandlePlayerDeath);
        cachedPlayerHealth = null;
    }

    private void TryBindPlayerCharges()
    {
        if (playerCharges == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) playerCharges = playerObj.GetComponentInChildren<AvatarPlaceCharge_NewVisualsV2>();
        }
        if (playerCharges == null) return;
        playerCharges.OnInventoryChanged.AddListener(HandleInventoryChanged);
    }

    private void UnsubscribeFromCharges()
    {
        if (playerCharges == null) return;
        playerCharges.OnInventoryChanged.RemoveListener(HandleInventoryChanged);
    }

    private void TryBindCollectors()
    {
        if (winWhenThisCollectorDisabled != null)
        {
            winWhenThisCollectorDisabled.OnDisabled.AddListener(HandleSpecificCollectorDisabled);
        }

        if (collectorsLoseWhenTwoFullyDestroyed != null)
        {
            foreach (var c in collectorsLoseWhenTwoFullyDestroyed)
            {
                if (c == null) continue;
                c.OnFullyDestroyed.AddListener(HandleAnyCollectorDestroyed);
            }
        }
    }

    private void UnsubscribeFromCollectors()
    {
        if (winWhenThisCollectorDisabled != null)
        {
            winWhenThisCollectorDisabled.OnDisabled.RemoveListener(HandleSpecificCollectorDisabled);
        }
        if (collectorsLoseWhenTwoFullyDestroyed != null)
        {
            foreach (var c in collectorsLoseWhenTwoFullyDestroyed)
            {
                if (c == null) continue;
                c.OnFullyDestroyed.RemoveListener(HandleAnyCollectorDestroyed);
            }
        }
    }

    private void HandlePlayerDeath()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        OnLose.Invoke();
        Invoke(nameof(LoadLoseScene), sceneChangeDelay);
    }

    private void HandleSpecificCollectorDisabled()
    {
        if (isTransitioning) return;
        WinLevel();
    }

    private void HandleAnyCollectorDestroyed()
    {
        if (isTransitioning) return;
        destroyedCollectorsCount++;
        if (destroyedCollectorsCount >= 2)
        {
            LoseLevel();
        }
    }

    private void HandleInventoryChanged()
    {
        if (playerCharges == null) return;
        if (!playerCharges.HasAnyAmmo)
        {
            if (outOfAmmoStartTime < 0f)
                outOfAmmoStartTime = Time.time;
        }
        else
        {
            outOfAmmoStartTime = -1f; // ammo replenished, cancel timer
        }
    }

    private void EvaluateOutOfAmmoTimer()
    {
        if (isTransitioning) return;
        if (playerCharges == null) return;
        if (outOfAmmoStartTime < 0f) return;

        if (Time.time - outOfAmmoStartTime >= outOfAmmoLoseDelaySeconds)
        {
            LoseLevel();
        }
    }

    public void WinLevel()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        OnWin.Invoke();
        if (goToNextBuildIndexOnWin)
        {
            Invoke(nameof(LoadNextBuildIndex), sceneChangeDelay);
        }
        else
        {
            Invoke(nameof(LoadWinScene), sceneChangeDelay);
        }
    }

    public void LoseLevel()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        OnLose.Invoke();
        Invoke(nameof(LoadLoseScene), sceneChangeDelay);
    }

    public void LoadStartMenu()
    {
        if (string.IsNullOrEmpty(startMenuScene)) return;
        SceneManager.LoadScene(startMenuScene);
    }

    // Public UI hook: Start button
    public void StartGame()
    {
        // From the start menu, load the next scene in Build Settings (first playable scene)
        LoadNextBuildIndex();
    }

    // Public UI hook: Quit button
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadWinScene()
    {
        if (string.IsNullOrEmpty(winScene))
        {
            LoadNextBuildIndex();
            return;
        }
        // Cursor.lockState = CursorLockMode.None;
        // Cursor.visible = true;
        SceneManager.LoadScene(winScene);
    }

    private void LoadLoseScene()
    {
        if (string.IsNullOrEmpty(loseScene))
        {
            if (!string.IsNullOrEmpty(startMenuScene))
            {
                SceneManager.LoadScene(startMenuScene);
            }
            return;
        }
        SceneManager.LoadScene(loseScene);
    }

    private void LoadNextBuildIndex()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;
        if (next >= SceneManager.sceneCountInBuildSettings)
        {
            if (!string.IsNullOrEmpty(winScene))
            {
                SceneManager.LoadScene(winScene);
            }
            else if (!string.IsNullOrEmpty(startMenuScene))
            {
                SceneManager.LoadScene(startMenuScene);
            }
            return;
        }
        SceneManager.LoadScene(next);
    }
}


