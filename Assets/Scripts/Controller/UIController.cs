using System;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingSignal : ASignal<bool>{}
public class ChangeWeaponSignal : ASignal<PowerUp>{}
public class StartGameSignal : ASignal{}
public class UIController : MonoBehaviour
{
    [SerializeField] GameObject PauseScreen;
    [SerializeField] GameObject WinScreen;
    [SerializeField] GameObject DeathScreen;
    [SerializeField] GameObject LoadScreen;
    [SerializeField] private Image gunImg;
    [SerializeField] private Image miniImg;
    [SerializeField] private TextMeshProUGUI gunText;
    [SerializeField] Sprite[] miniSprites;
    [SerializeField] private Sprite[] gunSprites;
    [SerializeField] private Image[] minionCount;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI gateText;
    [SerializeField] private Image[] imagesSkin;
    [SerializeField] private Sprite[] spritesSkin;
    [SerializeField] private GameObject expFill;
    public ChoosePowerUp powerUpPopUp;
    private int totalEnergy;
    private int currentEnergy;
    private float totalTime;
    private int totalWeapon = -1;

    // countdown coroutine handle
    protected Coroutine _countdownCoroutine = null;

    protected void Awake()
    {
        Signals.Get<ChangeWeaponSignal>().AddOnlyListener((type) =>
        {
            /*foreach (var img in minionCount) 
                img.gameObject.SetActive(false);*/
            totalWeapon++;
            minionCount[totalWeapon].gameObject.SetActive(true);
            minionCount[totalWeapon].sprite = type.icon;
            /*gunImg.sprite = gunSprites[(int)type];
            miniImg.sprite = miniSprites[(int)type];
            gunText.text = type.ToString().Replace("_", " ");*/
        });
        Signals.Get<KillEnemySignal>().AddOnlyListener(() =>
        {
            /*totalEnergy--;
            energyText.text = totalEnergy.ToString();
            if (totalEnergy == 0)
            {
                WinScreen.SetActive(true);
                StopCountdown();
            }*/
        });
        Signals.Get<StartGameSignal>().AddOnlyListener(() =>
        {
            
        });
        
        Signals.Get<EnergyPickupSignals>().AddListener(EnergyPickup);
    }

    private void OnDisable()
    {
        Signals.Get<EnergyPickupSignals>().RemoveListener(EnergyPickup);
    }

    private void EnergyPickup(int i)
    {
        currentEnergy += i;
        if (currentEnergy == totalEnergy)
        {
            gateText.gameObject.SetActive(true);
        }
        energyText.text = currentEnergy.ToString();
    }
    
    public void ReloadScene()
    {
        LevelController.Instance.ReloadCurrentScene();
        gateText.gameObject.SetActive(false);
        PauseScreen.SetActive(false);
        DeathScreen.SetActive(false);
        WinScreen.SetActive(false);
    }
    
    public void MainMenu()
    {
        LevelController.Instance.MainMenu();
        Time.timeScale = 1f;
    }

    public void NextLevel()
    {
        LevelController.Instance.NextLevel();
        PauseScreen.SetActive(false);
        DeathScreen.SetActive(false);
        WinScreen.SetActive(false);
    }

    public void PauseGame()
    {
        PauseScreen.SetActive(true);
        Time.timeScale = 0f;
    }
    
    public void ResumeGame()
    {
        PauseScreen.SetActive(false);
        Time.timeScale = 1f;
    }
    
    private void Start()
    {
        totalEnergy = LevelController.Instance.CurrentLevelCondition.TotalEnergy;
        totalTime = LevelController.Instance.CurrentLevelCondition.TimeToComplete;
        energyText.text = "0";
        levelText.text = "Level " + LevelController.Instance.CurrentLevelCondition.LevelName;
        // show initial time and start countdown
        UpdateTimeText(totalTime);
        StartCountdown();

        foreach (Image img in imagesSkin)
        {
            img.sprite = spritesSkin[LevelController.Instance.skinID];
        }
    }

    /// <summary>
    /// Starts the countdown using totalTime. If already running, restarts it.
    /// </summary>
    public void StartCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    /// <summary>
    /// Stops the countdown (pauses display).
    /// </summary>
    public void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    /// <summary>
    /// Reset total time and update UI. Does not start automatically unless startNow = true.
    /// </summary>
    public void ResetCountdown(float newTotalTime, bool startNow = false)
    {
        totalTime = newTotalTime;
        UpdateTimeText(totalTime);
        if (startNow) StartCountdown();
    }

    private System.Collections.IEnumerator CountdownRoutine()
    {
        float remaining = totalTime;
        while (remaining > 0f)
        {
            UpdateTimeText(remaining);
            // countdown respects Time.timeScale (paused when timeScale==0). If you want realtime use Time.unscaledDeltaTime.
            yield return null;
            remaining -= Time.deltaTime;
        }
        remaining = 0f;
        UpdateTimeText(remaining);
        DeathScreen.SetActive(true);
        _countdownCoroutine = null;
        // optionally dispatch event when timer ends
        // Signals.Get<TimeUpSignal>().Dispatch();
    }

    /// <summary>
    /// Updates timeText with format MM:SS.<size=...>t</size>
    /// Shows minutes (2 digits), seconds (2 digits) and tenths of a second rendered with a size tag (example requested).
    /// Example output: 01:29.<size=42.21>9</size>
    /// </summary>
    /// <param name="time">seconds remaining</param>
    private void UpdateTimeText(float time)
    {
        if (timeText == null) return;

        time = Mathf.Max(0f, time);
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        // tenths of a second (one digit). If you prefer milliseconds (3 digits) change accordingly.
        int tenths = Mathf.FloorToInt((time * 10f) % 10f);

        // you can adjust the size value below if you want a different visual scale
        string sizeValue = "42.21";
        string formatted = string.Format("{0:00}:{1:00}.<size={2}>{3}</size>", minutes, seconds, sizeValue, tenths);
        timeText.text = formatted;
    }
}
