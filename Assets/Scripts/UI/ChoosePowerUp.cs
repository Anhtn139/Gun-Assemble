using System;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

public class ChoosePowerUp : MonoBehaviour
{
    [SerializeField] private PowerUpCard[] powerUpCards;
    [SerializeField] private PowerUp[] powerUps;
    [SerializeField] private PowerUpRandomController powerUpRandomController;
    [SerializeField] private Button selectPowerUpButton;
    
    private void OnEnable()
    {
        for (int i = 0; i < powerUpCards.Length; i++)
        {
            powerUpCards[i].SetPowerUp(powerUps[i]);
        }
    }

    private void Awake()
    {
        Signals.Get<PowerUpPickedSignal>().Dispatch(LevelController.Instance.currentPowerUp);
    }
}
