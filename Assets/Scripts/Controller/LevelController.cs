using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSignal : ASignal {}

public enum WeaponType
{
    Pistol = 0,
    Shotgun = 1,
    AR = 2,
    Sniper = 3,
    Rocket = 4
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
        CurrentLevel = CurrentLevelCondition.LevelName;
        var nextLevel = CurrentLevel + 1;
        if (PlayerPrefs.GetInt("CurrentLevel") < nextLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", CurrentLevel);
        }
        CurrentLevelCondition = levels.Get(nextLevel);
        ReloadCurrentScene();
    }
        
    /*private void Start()
    {
        _countdown = GameController.GameInstance.levelInfo.TimeToComplete;
        StartCoroutine(StartCount());
    }

    IEnumerator StartCount()
    {
        while (_countdown > 0f)
        {
            _countdown -= Time.deltaTime;
            countdownText.text = "Time: " + _countdown.ToString("0") + "s";
            yield return null;
        }
        if (_countdown <= 0f)
        {
            gameOver.SetActive(true);
            Signals.Get<HideSettingSignal>().Dispatch(true);
        }
    }*/

    private void Start()
    {
        Signals.Get<LoadingSignal>().AddOnlyListener(b =>
        {
            loadingScreen.gameObject.SetActive(b);
        });
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
