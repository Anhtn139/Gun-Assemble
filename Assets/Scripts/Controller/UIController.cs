using System;
using MoreMountains.Tools;
using UnityEngine;

public class LoadingSignal : ASignal<bool>{}
public class UIController : MMSingleton<UIController>
{
    [SerializeField] GameObject PauseScreen;
    [SerializeField] GameObject DeathScreen;
    [SerializeField] GameObject LoadScreen;

    public void ReloadScene()
    {
        LevelController.Instance.ReloadCurrentScene();
        PauseScreen.SetActive(false);
        DeathScreen.SetActive(false);
    }

    private void Start()
    {
        Signals.Get<LoadingSignal>().AddOnlyListener(b =>
        {
            LoadScreen.gameObject.SetActive(b);
        });
    }
}
