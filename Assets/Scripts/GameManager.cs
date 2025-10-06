using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Scenes")] 
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField] private string loseScene = "Lose";
    [SerializeField] private string winScene = "Win";
    [SerializeField] private bool goToNextBuildIndexOnWin = false;
    [SerializeField] private float sceneChangeDelay = 1.0f;

    [Header("Player Binding")] 
    [SerializeField] private string playerTag = "Player";

    public UnityEvent OnWin = new UnityEvent();
    public UnityEvent OnLose = new UnityEvent();

    public static GameManager Instance { get; private set; }

    private HealthSystem cachedPlayerHealth;
    private bool isTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        TryBindPlayerHealth();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeFromPlayer();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isTransitioning = false;
        TryBindPlayerHealth();
    }

    private void TryBindPlayerHealth()
    {
        if (cachedPlayerHealth != null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null) return;

        HealthSystem hs = playerObj.GetComponent<HealthSystem>();
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

    private void HandlePlayerDeath()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        OnLose.Invoke();
        Invoke(nameof(LoadLoseScene), sceneChangeDelay);
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

    private void LoadWinScene()
    {
        if (string.IsNullOrEmpty(winScene))
        {
            LoadNextBuildIndex();
            return;
        }
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


