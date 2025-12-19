using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class PowerUpCard : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    public Toggle button;
    public PowerUpType powerUpType;
    private PowerUp _powerUp;
    
    public void SetPowerUp(PowerUp powerUp)
    {
        this._powerUp = powerUp;
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
                switch (powerUpType)
                {
                    case PowerUpType.WeaponChange :
                        LevelController.Instance.currentPowerUp = _powerUp as WeaponChangeType;
                        break;
                    case PowerUpType.AttackSpeed:
                        LevelController.Instance.currentPowerUp = _powerUp as FireRateType;
                        break;
                }
            }
        });
    }
}
