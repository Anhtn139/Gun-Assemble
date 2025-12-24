using System;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

public class ChoosePowerUp : MonoBehaviour
{
    [SerializeField] private PowerUpCard[] powerUpCards;
    [SerializeField] private WeaponChangeType[] weaponTypes; // list các WeaponChangeType assets (vũ khí trong game)
    [SerializeField] private FireRateType fireRateType;
    [SerializeField] private DamageType damageType;
    [SerializeField] private Button selectPowerUpButton;
    private PowerUp[] powerUpsToShow;
    
    private void OnEnable()
    {
        powerUpsToShow = RandomizePowerUp();
        
        for (int i = 0; i < powerUpCards.Length; i++)
        {
            powerUpCards[i].SetPowerUp(powerUpsToShow[i]);
        }

        Time.timeScale = 0f;
    }

    private void Awake()
    {
        selectPowerUpButton.onClick.AddListener(() =>
        {
            Signals.Get<PowerUpPickedSignal>().Dispatch(LevelController.Instance.currentPowerUp);
            Time.timeScale = 1f;
        });
    }

    // Trả về mảng PowerUp để hiển thị trên các card
    public PowerUp[] RandomizePowerUp()
    {
        int count = Mathf.Max(1, powerUpCards != null ? powerUpCards.Length : 3);
        PowerUp[] result = new PowerUp[count];

        // Fisher-Yates shuffle helper
        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Pool các stat powerups (assets)
        var statPool = new List<PowerUp>();
        if (damageType != null) statPool.Add(damageType);
        if (fireRateType != null) statPool.Add(fireRateType);

        // Chuẩn bị danh sách vũ khí từ WeaponChangeType assets (nếu có)
        var weaponAssets = new List<PowerUp>();
        if (weaponTypes != null && weaponTypes.Length > 0)
        {
            foreach (var wt in weaponTypes)
            {
                if (wt != null) weaponAssets.Add(wt);
            }
        }

        // Xác định lần đầu vào level bằng flag của LevelController
        bool firstShowThisLevel = false;
        if (LevelController.Instance != null)
        {
            firstShowThisLevel = LevelController.Instance.isNewGame;
            if (firstShowThisLevel)
            {
                // mark đã hiển thị lần đầu cho level này
                LevelController.Instance.isNewGame = false;
            }
        }

        if (firstShowThisLevel)
        {
            // Lần đầu trong level: ưu tiên show vũ khí (tối đa count) — dựa trên WeaponChangeType assets nếu có,
            // nếu không có asset nào thì fallback sang statPool
            Shuffle(weaponAssets);

            int filled = 0;
            int assetToShow = Math.Min(count, weaponAssets.Count);
            for (int i = 0; i < assetToShow; i++)
            {
                result[filled++] = weaponAssets[i];
            }

            // fill remaining bằng stat powerups (không trùng)
            var sp = new List<PowerUp>(statPool);
            Shuffle(sp);
            while (filled < count)
            {
                if (sp.Count > 0)
                {
                    result[filled++] = sp[0];
                    sp.RemoveAt(0);
                }
                else
                {
                    // fallback nếu thiếu
                    var fb = ScriptableObject.CreateInstance<PowerUp>();
                    fb.kind = PowerUpType.DamageIncrease;
                    fb.displayName = "Damage +10%";
                    fb.description = "Small damage bump";
                    fb.magnitude = 1.10f;
                    result[filled++] = fb;
                }
            }
        }
        else
        {
            // Bình thường: build full pool = weapon assets + stat assets, chọn ngẫu nhiên không trùng
            var fullPool = new List<PowerUp>();
            fullPool.AddRange(weaponAssets);

            if (damageType != null) fullPool.Add(damageType);
            if (fireRateType != null) fullPool.Add(fireRateType);

            Shuffle(fullPool);

            var usedNames = new HashSet<string>();
            int idx = 0;
            foreach (var candidate in fullPool)
            {
                if (idx >= count) break;
                string key = !string.IsNullOrEmpty(candidate.displayName) ? candidate.displayName : candidate.GetLabel();
                if (string.IsNullOrEmpty(key)) key = Guid.NewGuid().ToString();
                if (usedNames.Add(key))
                {
                    result[idx++] = candidate;
                }
            }

            // nếu chưa đủ distinct, điền bằng stat fallback
            while (idx < count)
            {
                bool placed = false;
                foreach (var s in statPool)
                {
                    string k = !string.IsNullOrEmpty(s.displayName) ? s.displayName : s.GetLabel();
                    if (!usedNames.Contains(k))
                    {
                        result[idx++] = s;
                        usedNames.Add(k);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    var fb = ScriptableObject.CreateInstance<PowerUp>();
                    fb.kind = PowerUpType.DamageIncrease;
                    fb.displayName = $"Damage +{10 + idx}%";
                    fb.description = "Fallback damage";
                    fb.magnitude = 1.1f;
                    result[idx++] = fb;
                }
            }
        }

        // đảm bảo không có null
        for (int k = 0; k < result.Length; k++)
        {
            if (result[k] == null)
            {
                var fb = ScriptableObject.CreateInstance<PowerUp>();
                fb.kind = PowerUpType.DamageIncrease;
                fb.displayName = "Damage +10%";
                fb.description = "Fallback damage";
                fb.magnitude = 1.1f;
                result[k] = fb;
            }
        }

        return result;
    }
}
