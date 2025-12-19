using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MoreMountains.TopDownEngine;
using System.Collections;
using MoreMountains.Tools;

[Serializable]
public class SkinData
{
    public int skinID;
    public GameObject[] skinPrefabs;
}

public class PowerUpPickedSignal : ASignal<PowerUp> {}

public enum PowerUpType
{
    WeaponChange,
    DamageIncrease,
    AttackSpeed,
    HealthIncrease
}

public class CharacterController : MonoBehaviour
{
    public enum PowerUpOptionType
    {
        EquipMainWeapon,       // change main weapon to the selected prefab
        AddSecondaryWithWeapon,// enable a secondary and equip it with the selected weapon
        DamageBuff,            // increase damage for all current weapons (multiplier)
        FireRateBuff           // increase fire rate for all current weapons (multiplier)
    }

    [Serializable]
    public class PowerUpOption
    {
        public PowerUpOptionType OptionType;
        public Weapon WeaponPrefab;     // used for weapon options
        public float Magnitude = 1f;    // used for stat buffs (multiplier)
        public string DisplayName;
    }

    [Header("Secondaries")]
    [Tooltip("Gán 2 character phụ (GameObject). Mặc định để inactive trong prefab)")]
    [SerializeField] private GameObject[] SecondaryCharacters = new GameObject[2];
    [SerializeField] private SkinData[] SkinDatas;

    [Header("Experience")]
    [Tooltip("Số kinh nghiệm cần để mở power-up selection")]
    [SerializeField] private int ExperienceToLevel = 3;
    [Tooltip("Số XP nhận mỗi lần nhặt pickup (tag = 'Upgrade')")]
    [SerializeField] private int ExperiencePerPickup = 1;

    // runtime xp
    private int _currentExperience = 0;

    // số secondary đã được enable
    private int _enabledSecondaries = 0;
    // số lần đã áp dụng upgrade (reserved, may be used by external logic)
    private int _upgradeAppliedCount = 0;

    // global multipliers applied by stat powerups (persist until changed)
    private float _globalDamageMultiplier = 1f;
    private float _globalFireRateMultiplier = 1f;

    // Level up UI hook: subscriber will display options and call ChoosePowerUp
    public event Action<PowerUpOption[]> OnLevelUpChoicesAvailable;

    void Start()
    {
        // ensure secondaries start disabled if assigned
        foreach (var s in SecondaryCharacters)
        {
            if (s != null) s.SetActive(false);
        }

        foreach (var s in SkinDatas)
        {
            if (s.skinID == LevelController.Instance.skinID)
            {
                foreach (var p in s.skinPrefabs)
                {
                    p.SetActive(true);
                }
            }
            else
            {
                foreach (var p in s.skinPrefabs)
                {
                    p.SetActive(false);
                }
            }
        }
    }

    // subscribe to PowerUpPickedSignal so external UI can dispatch a PowerUp asset and CharacterController will apply it
    protected virtual void OnEnable()
    {
        Signals.Get<PowerUpPickedSignal>().AddListener(HandlePowerUpPicked);
    }

    protected virtual void OnDisable()
    {
        Signals.Get<PowerUpPickedSignal>().RemoveListener(HandlePowerUpPicked);
    }

    /// <summary>
    /// Add xp, check for level up and if reached, build 3 powerup options and raise event for UI selection
    /// </summary>
    /// <param name="xp"></param>
    public void AddExperience(int xp)
    {
        _currentExperience += Mathf.Max(0, xp);
        if (_currentExperience >= ExperienceToLevel)
        {
            _currentExperience = 0;
            PublishLevelUpChoices();
        }
    }

    /// <summary>
    /// Build up to three PowerUpOption choices (stat buffs only now).
    /// Weapon options are provided by PowerUp assets and should be dispatched via PowerUpPickedSignal by the UI when needed.
    /// </summary>
    private void PublishLevelUpChoices()
    {
        var pool = new System.Collections.Generic.List<PowerUpOption>();

        // add stat buff options
        pool.Add(new PowerUpOption()
        {
            OptionType = PowerUpOptionType.DamageBuff,
            Magnitude = 1.25f,
            DisplayName = "Increase Damage x1.25"
        });
        pool.Add(new PowerUpOption()
        {
            OptionType = PowerUpOptionType.FireRateBuff,
            Magnitude = 1.25f,
            DisplayName = "Increase Fire Rate x1.25"
        });

        // choose up to 3 distinct random options (pool currently holds 2 stat buffs)
        var rnd = new System.Random();
        var selected = pool.OrderBy(x => rnd.Next()).Take(Math.Min(3, pool.Count)).ToArray();

        // raise event for UI to display choices
        OnLevelUpChoicesAvailable?.Invoke(selected);
    }

    /// <summary>
    /// Called by UI (or other system) when player picks an option.
    /// For weapon-related picks the UI should dispatch a PowerUp (with weapon) via PowerUpPickedSignal.
    /// </summary>
    public void ChoosePowerUp(PowerUpOption option)
    {
        if (option == null) return;

        switch (option.OptionType)
        {
            case PowerUpOptionType.EquipMainWeapon:
                ApplyEquipMainWeapon(option.WeaponPrefab);
                break;
            case PowerUpOptionType.AddSecondaryWithWeapon:
                ApplyAddSecondary(option.WeaponPrefab);
                break;
            case PowerUpOptionType.DamageBuff:
                _globalDamageMultiplier *= option.Magnitude;
                ApplyGlobalStatMultipliers();
                break;
            case PowerUpOptionType.FireRateBuff:
                _globalFireRateMultiplier *= option.Magnitude;
                ApplyGlobalStatMultipliers();
                break;
        }
    }

    // SIGNAL handler: called when a PowerUp ScriptableObject is dispatched by UI or other system
    protected virtual void HandlePowerUpPicked(PowerUp powerUp)
    {
        if (powerUp == null) return;
        // immediate (permanent) application
        ApplyImmediatePowerUp(powerUp);
    }

    private void ApplyImmediatePowerUp(PowerUp powerUp)
    {
        if (powerUp == null) return;

        switch (powerUp.kind)
        {
            case PowerUpType.WeaponChange:
                var wp = GetWeaponFromPowerUp(powerUp);
                if (wp != null)
                {
                    ApplyEquipMainWeapon(wp);
                }
                break;

            case PowerUpType.AttackSpeed:
                var speed = powerUp as FireRateType;
                if (speed != null)
                {
                    var magD = speed.FireRate;
                    _globalFireRateMultiplier *= magD;
                }

                ApplyGlobalStatMultipliers();
                break;

            case PowerUpType.DamageIncrease:
                var damageType = powerUp as DamageType;
                var magF = damageType.Damage;
                _globalDamageMultiplier *= magF;
                ApplyGlobalStatMultipliers();
                break;

            case PowerUpType.HealthIncrease:
                powerUp.Apply(this.gameObject);
                break;
        }
    }

    private IEnumerator ApplyTimedPowerUp(PowerUp powerUp, float duration)
    {
        if (powerUp == null) yield break;

        switch (powerUp.kind)
        {
            case PowerUpType.WeaponChange:
                var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
                if (mainHandle != null)
                {
                    var previous = mainHandle.CurrentWeapon;
                    var weapon = GetWeaponFromPowerUp(powerUp);
                    if (weapon != null)
                    {
                        ApplyEquipMainWeapon(weapon);
                        yield return new WaitForSeconds(duration);
                        if (previous != null)
                        {
                            mainHandle.ChangeWeapon(previous, previous.name);
                            SyncSecondariesToMainWeapon();
                        }
                    }
                }
                break;

            case PowerUpType.DamageIncrease:
                var magD = HasFloatFieldOrProp(powerUp, "magnitude", out float mD) ? mD : 1.25f;
                _globalDamageMultiplier *= magD;
                ApplyGlobalStatMultipliers();
                yield return new WaitForSeconds(duration);
                _globalDamageMultiplier = Mathf.Approximately(magD, 0f) ? 1f : _globalDamageMultiplier / magD;
                ApplyGlobalStatMultipliers();
                break;

            case PowerUpType.AttackSpeed:
                var magF = HasFloatFieldOrProp(powerUp, "magnitude", out float mF) ? mF : 1.25f;
                _globalFireRateMultiplier *= magF;
                ApplyGlobalStatMultipliers();
                yield return new WaitForSeconds(duration);
                _globalFireRateMultiplier = Mathf.Approximately(magF, 0f) ? 1f : _globalFireRateMultiplier / magF;
                ApplyGlobalStatMultipliers();
                break;

            case PowerUpType.HealthIncrease:
                yield return StartCoroutine(powerUp.ApplyTemporary(this.gameObject));
                break;
        }
    }

    private Weapon GetWeaponFromPowerUp(PowerUp powerUp)
    {
        if (powerUp == null) return null;
        var type = powerUp as WeaponChangeType;
        return type.weapon;
    }

    private bool HasFloatFieldOrProp(object obj, string name, out float value)
    {
        value = 0f;
        if (obj == null) return false;
        var t = obj.GetType();
        var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(float))
        {
            value = (float)fi.GetValue(obj);
            return true;
        }
        var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(float) && pi.CanRead)
        {
            value = (float)pi.GetValue(obj);
            return true;
        }
        return false;
    }

    private bool HasBoolFieldOrProp(object obj, string name, out bool value)
    {
        value = false;
        if (obj == null) return false;
        var t = obj.GetType();
        var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(bool))
        {
            value = (bool)fi.GetValue(obj);
            return true;
        }
        var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (pi != null && pi.PropertyType == typeof(bool) && pi.CanRead)
        {
            value = (bool)pi.GetValue(obj);
            return true;
        }
        return false;
    }

    private void ApplyEquipMainWeapon(Weapon chosenWeapon)
    {
        var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
        if (mainHandle == null) return;

        if (chosenWeapon != null)
        {
            mainHandle.ChangeWeapon(chosenWeapon, chosenWeapon.name);
            SyncSecondariesToMainWeapon();
        }
    }

    private void ApplyAddSecondary(Weapon weaponToGive)
    {
        // find next inactive secondary and enable it
        for (int i = 0; i < SecondaryCharacters.Length; i++)
        {
            var s = SecondaryCharacters[i];
            if (s == null) continue;
            if (!s.activeSelf)
            {
                s.SetActive(true);
                _enabledSecondaries++;

                // ensure secondary's Health is independent
                var health = s.GetComponentInChildren<Health>();
                if (health != null)
                {
                    health.MasterHealth = null;
                    health.InitializeCurrentHealth();
                    health.DamageEnabled();
                }

                // equip weapon if provided
                var handle = s.GetComponentInChildren<CharacterHandleWeapon>();
                if (handle != null && weaponToGive != null)
                {
                    StartCoroutine(EquipAfterNextFrame(s, weaponToGive, false));
                }
                break;
            }
        }
    }

    /// <summary>
    /// Apply global multipliers to all currently equipped weapons.
    /// Note: Weapon implementations must respect these multipliers for their projectiles.
    /// This method will try to call well-known APIs by reflection where available; otherwise it will just call ChangeWeapon re-equip to let weapon initialization pick up global multipliers if implemented.
    /// </summary>
    private void ApplyGlobalStatMultipliers()
    {
        var handles = this.GetComponentsInChildren<CharacterHandleWeapon>(true);
        foreach (var h in handles)
        {
            var w = h.CurrentWeapon;
            if (w == null) continue;

            // try to set commonly used fields/properties via reflection (best-effort)
            var type = w.GetType();

            // Damage multiplier support (field/property names commonly used)
            var dmgField = type.GetField("DamageMultiplier");
            if (dmgField != null && dmgField.FieldType == typeof(float))
            {
                dmgField.SetValue(w, _globalDamageMultiplier);
            }
            else
            {
                var dmgProp = type.GetProperty("DamageMultiplier");
                if (dmgProp != null && dmgProp.PropertyType == typeof(float) && dmgProp.CanWrite)
                {
                    dmgProp.SetValue(w, _globalDamageMultiplier);
                }
            }

            // Fire rate multiplier / rate support
            var frField = type.GetField("FireRateMultiplier");
            if (frField != null && frField.FieldType == typeof(float))
            {
                frField.SetValue(w, _globalFireRateMultiplier);
            }
            else
            {
                var frProp = type.GetProperty("FireRateMultiplier");
                if (frProp != null && frProp.PropertyType == typeof(float) && frProp.CanWrite)
                {
                    frProp.SetValue(w, _globalFireRateMultiplier);
                }
            }

            // As a fallback, re-equip the same weapon so its initialization can pick up global multipliers if those are read from a central place
            h.ChangeWeapon(w, w.name);
        }
    }

    /// <summary>
    /// Coroutine: đợi 1 frame để đảm bảo CharacterHandleWeapon/Character đã khởi tạo rồi equip weapon.
    /// Nếu disableAfter true thì sẽ disable GameObject sau khi equip xong.
    /// Ngoài ra đảm bảo Health.MasterHealth = null để tránh redirect damage về main.
    /// </summary>
    private IEnumerator EquipAfterNextFrame(GameObject characterGO, Weapon weapon, bool disableAfter)
    {
        // wait one frame so that Start()/Initialization() on components in characterGO can run
        yield return null;

        if (characterGO == null) yield break;

        // ensure secondary's health is independent
        var health = characterGO.GetComponentInChildren<Health>();
        if (health != null)
        {
            health.MasterHealth = null;
            health.InitializeCurrentHealth();
            health.DamageEnabled();
        }

        var handle = characterGO.GetComponentInChildren<CharacterHandleWeapon>();
        if (handle != null && weapon != null)
        {
            handle.ChangeWeapon(weapon, weapon.name);
        }

        if (disableAfter)
        {
            // small delay to ensure weapon instantiation completed
            yield return null;
            characterGO.SetActive(false);
        }
    }

    // Exposed helper: force sync weapon of secondaries to main (gọi nếu main đổi weapon theo logic khác)
    public void SyncSecondariesToMainWeapon()
    {
        var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
        if (mainHandle == null) return;
        var currentWeapon = mainHandle.CurrentWeapon;
        if (currentWeapon == null) return;

        // nếu bạn muốn secondaries dùng exact same prefab, gán UpgradedWeapons phù hợp trước (Inspector)
        foreach (var s in SecondaryCharacters)
        {
            if (s == null) continue;
            var secondaryHandle = s.GetComponentInChildren<CharacterHandleWeapon>();
            if (secondaryHandle == null) continue;
            // equip same weapon prefab
            secondaryHandle.ChangeWeapon(currentWeapon, currentWeapon.name);
        }
    }

    /// <summary>
    /// Nhận trigger của các upgrade (tag = "Upgrade").
    /// Pickup không trực tiếp bật secondary nữa — thay vào đó trao XP.
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        GameObject pickup = other.gameObject;

        // direct tag check
        if (!pickup.CompareTag("Upgrade"))
        {
            // fallback: kiểm tra root (nếu collider là child của prefab upgrade)
            if (other.transform.root != null && other.transform.root.gameObject != null && other.transform.root.gameObject.CompareTag("Upgrade"))
            {
                pickup = other.transform.root.gameObject;
            }
            else
            {
                return;
            }
        }

        // xử lý pickup -> give XP
        AddExperience(ExperiencePerPickup);

        // disable pickup collider to avoid multi-trigger and optionally destroy
        var col = pickup.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // optional: destroy pickup (commented out to let designer decide)
        // Destroy(pickup);
    }
}