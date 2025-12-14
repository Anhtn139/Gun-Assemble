using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;

/// <summary>
/// Quản lý thanh tiến trình power-up của người chơi.
/// Khi đầy sẽ hiển thị 3 lựa chọn power-up (không trùng loại).
/// Sau khi người chơi chọn 1 power-up, áp dụng hiệu ứng (cố gắng áp dụng mặc định bằng reflection)
/// và reset thanh tiến trình.
/// - Power up types: WeaponChange, DamageIncrease, AttackSpeed, HealthIncrease
/// - File được viết để dễ wiring trong Inspector: gán progressFill, powerUpPanel và 3 button.
/// - Nếu bạn muốn xử lý effect cụ thể hơn, gán các UnityEvent tương ứng trong Inspector.
/// </summary>
public class CharacterLevelController : MonoBehaviour
{
    public enum PowerUpType
    {
        WeaponChange,
        DamageIncrease,
        AttackSpeed,
        HealthIncrease
    }

    [Header("Progress")]
    [Tooltip("Image type Fill (fillAmount) dùng để hiển thị thanh tiến trình")]
    [SerializeField] private Image progressFill;
    [Tooltip("Giá trị tối đa để thanh đầy (progress được tăng bằng AddProgress)")]
    [SerializeField] private float maxProgress = 100f;
    [SerializeField] [Range(0f, 1f)] private float startFill = 0f;

    [Header("PowerUp UI")]
    [Tooltip("Panel chứa UI lựa chọn power-up (active = shown)")]
    [SerializeField] private GameObject powerUpPanel;
    [Tooltip("Buttons (3) dùng để hiển thị 3 lựa chọn. Mỗi button cần có một Text con để hiển thị label.")]
    [SerializeField] private Button[] optionButtons = new Button[3];

    [Header("Defaults / Tunables")]
    [Tooltip("Tỷ lệ tăng damage khi chọn power-up tăng sát thương (ví dụ 0.25 = +25% damage)")]
    [SerializeField] private float damagePercent = 0.25f;
    [Tooltip("Tỷ lệ thay đổi time-between-uses cho attack speed (ví dụ 0.8 = tốc đánh +25%)")]
    [SerializeField] private float attackSpeedMultiplier = 0.8f;

    [Header("Events (optional)")]
    public UnityEvent OnWeaponChangeChosen;
    public UnityEvent OnDamageIncreaseChosen;
    public UnityEvent OnAttackSpeedChosen;
    public UnityEvent OnHealthIncreaseChosen;

    // runtime
    private float _progress = 0f;
    private readonly System.Random _rng = new System.Random();

    void Start()
    {
        _progress = maxProgress * startFill;
        UpdateProgressUI();
        if (powerUpPanel != null) powerUpPanel.SetActive(false);

        // safety: ensure exactly 3 option buttons if provided
        if (optionButtons != null && optionButtons.Length > 0 && optionButtons.Length != 3)
        {
            Debug.LogWarning($"CharacterLevelController: optionButtons.Length = {optionButtons.Length}. Khuyến nghị thiết lập đúng 3 buttons.");
        }
    }

    void Update()
    {
        // (nếu muốn tự động test) - không làm gì ở Update by default.
    }

    /// <summary>
    /// Thêm progress (gọi từ nơi khác khi player làm điều gì đó)
    /// Nếu đầy => show choices.
    /// </summary>
    /// <param name="amount"></param>
    public void AddProgress(float amount)
    {
        if (amount <= 0f) return;
        _progress = Mathf.Clamp(_progress + amount, 0f, maxProgress);
        UpdateProgressUI();
        if (_progress >= maxProgress)
        {
            ShowPowerUpChoices();
        }
    }

    /// <summary>
    /// Reset progress về 0 và cập nhật UI.
    /// </summary>
    public void ResetProgress()
    {
        _progress = 0f;
        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressFill != null)
        {
            float fill = (maxProgress <= 0f) ? 0f : Mathf.Clamp01(_progress / maxProgress);
            progressFill.fillAmount = fill;
        }
    }

    private void ShowPowerUpChoices()
    {
        // lấy 3 loại khác nhau
        var all = Enum.GetValues(typeof(PowerUpType)).Cast<PowerUpType>().ToList();
        var chosen = new List<PowerUpType>();

        // Fisher-Yates shuffle + take first 3
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        chosen = all.Take(Mathf.Min(3, all.Count)).ToList();

        // hiển thị UI
        if (powerUpPanel != null) powerUpPanel.SetActive(true);

        if (optionButtons == null || optionButtons.Length < chosen.Count)
        {
            Debug.LogWarning("CharacterLevelController: optionButtons chưa cấu hình đủ. Không thể hiển thị lựa chọn power-up.");
            return;
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var b = optionButtons[i];
            if (b == null) continue;

            b.onClick.RemoveAllListeners();
            if (i < chosen.Count)
            {
                var type = chosen[i];
                SetButtonLabel(b, PrettyName(type));
                b.interactable = true;
                b.onClick.AddListener(() => OnPowerUpSelected(type));
            }
            else
            {
                // nếu button vượt quá (thường không xảy ra)
                SetButtonLabel(b, "");
                b.interactable = false;
            }
        }
    }

    private void SetButtonLabel(Button b, string text)
    {
        if (b == null) return;
        var txt = b.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = text;
        }
    }

    private string PrettyName(PowerUpType t)
    {
        return t switch
        {
            PowerUpType.WeaponChange => "Đổi vũ khí",
            PowerUpType.DamageIncrease => $"Tăng sát thương +{Math.Round(damagePercent * 100)}%",
            PowerUpType.AttackSpeed => "Tăng tốc đánh",
            PowerUpType.HealthIncrease => "Tăng máu",
            _ => t.ToString()
        };
    }

    private void OnPowerUpSelected(PowerUpType type)
    {
        ApplyPowerUp(type);
        // hide panel và reset
        if (powerUpPanel != null) powerUpPanel.SetActive(false);
        ResetProgress();
    }

    private void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.WeaponChange:
                TryDefaultWeaponChange();
                OnWeaponChangeChosen?.Invoke();
                break;
            case PowerUpType.DamageIncrease:
                if (!TryDefaultIncreaseDamage())
                {
                    Debug.Log("DamageIncrease: default apply failed (no compatible field/property found). Use OnDamageIncreaseChosen event to handle.");
                }
                OnDamageIncreaseChosen?.Invoke();
                break;
            case PowerUpType.AttackSpeed:
                if (!TryDefaultAttackSpeed())
                {
                    Debug.Log("AttackSpeed: default apply failed (no compatible field/property found). Use OnAttackSpeedChosen event to handle.");
                }
                OnAttackSpeedChosen?.Invoke();
                break;
            case PowerUpType.HealthIncrease:
                if (!TryDefaultIncreaseHealth())
                {
                    Debug.Log("HealthIncrease: default apply failed (no compatible property/method found). Use OnHealthIncreaseChosen event to handle.");
                }
                OnHealthIncreaseChosen?.Invoke();
                break;
        }
    }

    #region Default apply implementations (reflection-based, best-effort)

    private void TryDefaultWeaponChange()
    {
        // Nếu có LevelController.Instance.CurrentWeapon (enum WeaponType), tiến hành cycle tới weapon tiếp theo
        try
        {
            var level = LevelController.Instance;
            if (level != null)
            {
                var cur = level.CurrentWeapon;
                var arr = Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>().ToArray();
                int idx = Array.IndexOf(arr, cur);
                int next = (idx + 1) % arr.Length;
                var nextWeapon = arr[next];
                level.CurrentWeapon = nextWeapon;
                // Dispatch signal giống nơi khác trong project (CharacterController đã dùng)
                Signals.Get<ChangeWeaponSignal>().Dispatch(nextWeapon, 0);
                Debug.Log($"PowerUp: đổi vũ khí sang {nextWeapon}");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TryDefaultWeaponChange failed: {ex.Message}");
        }
    }

    private bool TryDefaultIncreaseDamage()
    {
        // tìm CharacterHandleWeapon.CurrentWeapon và cố gắng tăng numeric property có tên phổ biến (Damage, DamageCaused, BaseDamage...)
        var handle = GetComponentInChildren<MoreMountains.TopDownEngine.CharacterHandleWeapon>();
        if (handle == null || handle.CurrentWeapon == null) return false;

        object weaponObj = handle.CurrentWeapon;
        string[] candidateNames = new[] { "Damage", "WeaponDamage", "DamageCaused", "BaseDamage", "BaseDamageValue", "DamageMultiplier" };
        return TryAdjustNumericMember(weaponObj, candidateNames, v => v * (1.0 + damagePercent));
    }

    private bool TryDefaultAttackSpeed()
    {
        // cố gắng tìm property/field liên quan đến thời gian giữa các lần bắn và nhân với attackSpeedMultiplier
        var handle = GetComponentInChildren<MoreMountains.TopDownEngine.CharacterHandleWeapon>();
        if (handle == null || handle.CurrentWeapon == null) return false;

        object weaponObj = handle.CurrentWeapon;
        string[] candidateNames = new[] { "TimeBetweenUses", "TimeBetweenShots", "RateOfFire", "Cooldown", "TimeBetweenAttacks" };

        // For time-like values we multiply by attackSpeedMultiplier (smaller => faster)
        return TryAdjustNumericMember(weaponObj, candidateNames, v => v * attackSpeedMultiplier);
    }

    private bool TryDefaultIncreaseHealth()
    {
        // tìm component Health trên player và cố gắng tăng MaxHealth hoặc gọi method IncreaseMaxHealth / AddMaxHealth
        var health = GetComponentInChildren<Health>();
        if (health == null) return false;

        // try properties first
        var t = health.GetType();
        // try MaxHealth property or field
        var p = t.GetProperty("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
        if (p != null && IsNumericType(p.PropertyType) && p.CanWrite)
        {
            var current = Convert.ToDouble(p.GetValue(health));
            var next = current * (1.0 + damagePercent); // reuse damagePercent as percent increase
            SetNumericMember(p, health, next);
            // try call Reset/Init if exists
            InvokeIfExists(t, health, new[] { "ResetHealthToMaxHealth", "InitializeCurrentHealth", "ResetToMax" });
            return true;
        }

        var f = t.GetField("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
        if (f != null && IsNumericType(f.FieldType))
        {
            var current = Convert.ToDouble(f.GetValue(health));
            var next = current * (1.0 + damagePercent);
            f.SetValue(health, Convert.ChangeType(next, f.FieldType));
            InvokeIfExists(t, health, new[] { "ResetHealthToMaxHealth", "InitializeCurrentHealth", "ResetToMax" });
            return true;
        }

        // try method IncreaseMaxHealth(float) or AddMaxHealth(float)
        var methodNames = new[] { "IncreaseMaxHealth", "AddMaxHealth", "AddMaxHP", "IncreaseHealthMax" };
        foreach (var name in methodNames)
        {
            var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                var param = mi.GetParameters();
                if (param.Length == 1 && (param[0].ParameterType == typeof(float) || param[0].ParameterType == typeof(double)))
                {
                    float delta = (float)(Convert.ToDouble(healthMaxGuess(health)) * damagePercent);
                    mi.Invoke(health, new object[] { delta });
                    InvokeIfExists(t, health, new[] { "ResetHealthToMaxHealth", "InitializeCurrentHealth" });
                    return true;
                }
            }
        }

        return false;

        // helper to guess current max (best-effort)
        double healthMaxGuess(object h)
        {
            var tt = h.GetType();
            var p2 = tt.GetProperty("CurrentHealth");
            if (p2 != null && IsNumericType(p2.PropertyType))
            {
                // fallback: try CurrentHealth * (1+percent) as delta guess
                try
                {
                    return Convert.ToDouble(p2.GetValue(h));
                }
                catch { }
            }
            return 100.0; // default fallback
        }
    }

    #endregion

    #region Reflection helpers

    private bool TryAdjustNumericMember(object target, string[] candidateNames, Func<double, double> adjust)
    {
        if (target == null) return false;
        var t = target.GetType();

        // properties
        foreach (var name in candidateNames)
        {
            var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (pi != null && IsNumericType(pi.PropertyType) && pi.CanRead && pi.CanWrite)
            {
                var cur = Convert.ToDouble(pi.GetValue(target));
                var next = adjust(cur);
                SetNumericMember(pi, target, next);
                return true;
            }
        }

        // fields
        foreach (var name in candidateNames)
        {
            var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null && IsNumericType(fi.FieldType))
            {
                var cur = Convert.ToDouble(fi.GetValue(target));
                var next = adjust(cur);
                fi.SetValue(target, Convert.ChangeType(next, fi.FieldType));
                return true;
            }
        }

        return false;
    }

    private void SetNumericMember(PropertyInfo pi, object target, double value)
    {
        var pt = pi.PropertyType;
        if (pt == typeof(float)) pi.SetValue(target, (float)value);
        else if (pt == typeof(double)) pi.SetValue(target, value);
        else if (pt == typeof(int)) pi.SetValue(target, (int)Math.Round(value));
        else if (pt == typeof(long)) pi.SetValue(target, (long)Math.Round(value));
        else pi.SetValue(target, Convert.ChangeType(value, pt));
    }

    private bool IsNumericType(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long) || type == typeof(decimal);
    }

    private void InvokeIfExists(Type t, object instance, string[] methodNames)
    {
        foreach (var name in methodNames)
        {
            var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi != null && mi.GetParameters().Length == 0)
            {
                mi.Invoke(instance, null);
                return;
            }
        }
    }

    #endregion
}
