using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSignal : ASignal {}
public class LevelController : MMPersistentSingleton<LevelController>
{
    
    private Coroutine coroutine;
    public int CurrentLevel = 1;
    /*public void NextLevel()
    {
        var currentLevel = GameController.GameInstance.levelInfo.LevelName;
        var nextLevel = int.Parse(currentLevel) + 1;
        var nextLevelInfo = levels.GetByName(nextLevel.ToString());
        GameController.GameInstance.levelInfo = nextLevelInfo;
        GameController.GameInstance.CurrentLevel++;
        PlayerPrefs.SetInt("CurrentLevel", GameController.GameInstance.CurrentLevel);
        ReloadCurrentScene();
    }
        
    rivate void Start()
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
        /*Signals.Get<ResetCashSignal>().Dispatch();*/
    }

    public void LoadLevel(string levelName)
    {
        if (coroutine != null) return;
        coroutine = StartCoroutine(LoadMySceneAsync(levelName));
        /*Signals.Get<ResetCashSignal>().Dispatch();*/
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
