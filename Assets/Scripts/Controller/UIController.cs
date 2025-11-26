using System;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingSignal : ASignal<bool>{}
public class ChangeWeaponSignal : ASignal<WeaponType, int>{}
public class UIController : MMSingleton<UIController>
{
    [SerializeField] GameObject PauseScreen;
    [SerializeField] GameObject DeathScreen;
    [SerializeField] GameObject LoadScreen;
    [SerializeField] private Image gunImg;
    [SerializeField] private Image miniImg;
    [SerializeField] private TextMeshProUGUI gunText;
    [SerializeField] Sprite[] miniSprites;
    [SerializeField] private Sprite[] gunSprites;
    [SerializeField] private GameObject[] minionCount;

    protected override void Awake()
    {
        Signals.Get<ChangeWeaponSignal>().AddOnlyListener((type, i) =>
        {
            foreach (var img in minionCount) img.SetActive(false);
            for (int j = 0; j < i; j++)
            {
                minionCount[j].SetActive(true);
            }
            gunImg.sprite = gunSprites[(int)type];
            miniImg.sprite = miniSprites[(int)type];
            gunText.text = type.ToString();
        });
    }

    public void ReloadScene()
    {
        LevelController.Instance.ReloadCurrentScene();
        PauseScreen.SetActive(false);
        DeathScreen.SetActive(false);
    }
    
    public void MainMenu()
    {
        LevelController.Instance.MainMenu();
    }

    public void NextLevel()
    {
        LevelController.Instance.NextLevel();
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
        
    }
}
