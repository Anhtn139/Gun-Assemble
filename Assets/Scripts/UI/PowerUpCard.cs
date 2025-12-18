using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class PowerUpCard : MonoBehaviour
{
    public PowerUp powerUp;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Toggle button;

    public void SetPowerUp(PowerUp powerUp)
    {
        this.powerUp = powerUp;
        icon.sprite = powerUp.icon;
        nameText.text = powerUp.name;
        descriptionText.text = powerUp.description;
    }

    private void Awake()
    {
        button.onValueChanged.AddListener(arg0 =>
        {
            if (arg0)
            {
                LevelController.Instance.currentPowerUp = powerUp;
            }
        });
    }
}
