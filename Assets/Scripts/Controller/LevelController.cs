using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSignal : ASignal {}

public enum WeaponType
{
    Arrow = 0,
    Multi_Arrow = 1,
    Rapid_Arrow = 2,
    High_Speed_Arrow = 3,
    Explode_Arrow = 4
}
public class LevelController : MMPersistentSingleton<LevelController>
{
    [SerializeField] private GameObject loadingScreen;
    public LevelInfo levels;
    public LevelInfo.LevelCondition CurrentLevelCondition;
    public int CurrentLevel = 1;
    public WeaponType CurrentWeapon;
    private Coroutine coroutine;
    
    public void NextLevel()
    {
        var nextLevel = CurrentLevel + 1;
        CurrentLevel = nextLevel;
        if (PlayerPrefs.GetInt("CurrentLevel") < nextLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", nextLevel);
        }
        CurrentLevelCondition = levels.Get(nextLevel - 1);
        ReloadCurrentScene();
    }

    private void Start()
    {
        Signals.Get<LoadingSignal>().AddOnlyListener(b =>
        {
            loadingScreen.gameObject.SetActive(b);
        });
    }

    protected override void Awake()
    {
        if (!PlayerPrefs.HasKey("CurrentLevel"))
        {
            PlayerPrefs.SetInt("CurrentLevel", 1);
        }
    }

    public void MainMenu()
    {
        SceneManager.LoadScene("ChooseLevel");
    }
    
    public void ReloadCurrentScene()
    {
        // Get the name of the currently active scene
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Load the scene using its name
        LoadLevel(currentSceneName);
    }

    public void LoadLevel(string levelName)
    {
        Signals.Get<StartGameSignal>().Dispatch();
        if (coroutine != null) return;
        coroutine = StartCoroutine(LoadMySceneAsync(levelName));
    }

    public IEnumerator LoadMySceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad;
        try
        {
            asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LoadMySceneAsync: failed to start loading scene '{sceneName}': {ex.Message}");
            yield break;
        }

        if (asyncLoad == null)
        {
            Debug.LogError($"LoadMySceneAsync: scene '{sceneName}' not found or cannot be loaded.");
            yield break;
        }

        Signals.Get<LoadingSignal>().Dispatch(true);

        // Prevent immediate activation so we can observe progress (progress stops at ~0.9 until activation)
        asyncLoad.allowSceneActivation = false;

        // Wait for load to reach the "almost done" state (progress >= 0.9f)
        float timeout = 30f; // seconds — avoid infinite wait
        float timer = 0f;
        while (asyncLoad.progress < 0.9f && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (timer >= timeout)
        {
            Debug.LogWarning(
                $"LoadMySceneAsync: loading scene '{sceneName}' timed out after {timeout} seconds. Allowing activation.");
        }

        // Allow the scene to activate and finish the load
        asyncLoad.allowSceneActivation = true;
        // Wait until the operation is fully done (scene activated)
        yield return new WaitUntil(() => asyncLoad.isDone);
        coroutine = null;
        // Tắt loading screen sau khi scene đã được kích hoạt hoàn toàn
        Signals.Get<LoadingSignal>().Dispatch(false);
    }
    
}
