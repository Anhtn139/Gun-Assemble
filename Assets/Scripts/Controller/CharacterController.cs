using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MoreMountains.TopDownEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic; // added

[Serializable]
public class SkinData
{
    public int skinID;
    public GameObject[] skinPrefabs;
}

public class DronePickupSignals : ASignal<int> {}

public class PowerUpPickedSignal : ASignal<PowerUp> {}

public class EnergyPickupSignals : ASignal<int> {}

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

    [SerializeField] private GameObject[] guns;
    [SerializeField] private GameObject[] drones;

    // runtime xp
    private int _currentExperience = 0;

    // số secondary đã được enable
    private int _enabledSecondaries = 0;
    // số lần đã áp dụng upgrade (reserved, may be used by external logic)
    private int _upgradeAppliedCount = 0;

    // legacy globals kept for compatibility but not used for stacking across weapons
    private float _globalDamageMultiplier = 1f;
    private float _globalFireRateMultiplier = 1f;

    // store each weapon's original TimeBetweenUses so we can compute new values relative to its base
    private readonly Dictionary<Weapon, float> _weaponBaseTimeBetweenUses = new Dictionary<Weapon, float>();

    // per-weapon multipliers: buffs apply only to weapons present at buff time
    private readonly Dictionary<Weapon, float> _perWeaponDamageMultiplier = new Dictionary<Weapon, float>();
    private readonly Dictionary<Weapon, float> _perWeaponFireRateMultiplier = new Dictionary<Weapon, float>();

    // store base numeric damage for weapons if DamageMultiplier field/property isn't available
    private readonly Dictionary<Weapon, float> _weaponBaseDamageValue = new Dictionary<Weapon, float>();

    // Level up UI hook: subscriber will display options and call ChoosePowerUp
    public event Action<PowerUpOption[]> OnLevelUpChoicesAvailable;

    // added field near other runtime fields
    private bool _mainWeaponSetByPowerup = false;
    private ChoosePowerUp powerUpPopUp;

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
        powerUpPopUp = FindAnyObjectByType<UIController>().powerUpPopUp;
    }

    // subscribe to PowerUpPickedSignal so external UI can dispatch a PowerUp asset and CharacterController will apply it
    protected virtual void OnEnable()
    {
        Signals.Get<PowerUpPickedSignal>().AddListener(HandlePowerUpPicked);
        Signals.Get<EnergyPickupSignals>().AddListener(EnergyPickup);
        Signals.Get<DronePickupSignals>().AddListener(DronePickup);
    }

    protected virtual void OnDisable()
    {
        Signals.Get<PowerUpPickedSignal>().RemoveListener(HandlePowerUpPicked);
        Signals.Get<EnergyPickupSignals>().RemoveListener(EnergyPickup);
        Signals.Get<DronePickupSignals>().RemoveListener(DronePickup);
    }

    private void EnergyPickup(int i)
    {
        
    }
    
    private void DronePickup(int i)
    {
        if (drones == null || drones.Length == 0) return;

        foreach (var drone in drones)
        {
            if (drone == null || drone.activeSelf) continue;

            drone.SetActive(true);

            var weapons = drone.transform.Find("Weapons");
            if (weapons != null)
            {
                int childCount = weapons.childCount;
                int modelIndex = Mathf.Clamp(i, 0, childCount - 1);
                for (int c = 0; c < childCount; c++)
                {
                    weapons.GetChild(c).gameObject.SetActive(c == modelIndex);
                }
            }
            break;
        }
    }
    
    /*/// <summary>
    /// Add xp, check for level up and if reached, build 3 powerup options and raise event for UI selection
    /// </summary>
    /// <param name="xp"></param>
    public void AddExperience(int xp)
    {
        _currentExperience += Mathf.Max(0, xp);
        if (_currentExperience >= ExperienceToLevel)
        {
            _currentExperience = 0;
            if (powerUpPopUp != null)
            {
                powerUpPopUp.gameObject.SetActive(true);
            }
        } 
        Signals.Get<ExperiencePickupSignals>().Dispatch(_currentExperience);
    }*/

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
                ApplyEquipMainWeapon(option.WeaponPrefab); // default: sync secondaries
                break;
            case PowerUpOptionType.AddSecondaryWithWeapon:
                ApplyAddSecondary(option.WeaponPrefab);
                break;
            case PowerUpOptionType.DamageBuff:
                ApplyDamageBuffToActiveWeapons(option.Magnitude);
                break;
            case PowerUpOptionType.FireRateBuff:
                ApplyFireRateBuffToActiveWeapons(option.Magnitude);
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
                    var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
                    var mainCurrent = mainHandle?.CurrentWeapon;

                    // If this is the first weapon selected via power-up -> set main weapon
                    // IMPORTANT: do NOT enable secondaries on first pick — pass syncSecondaries=false
                    if (!_mainWeaponSetByPowerup)
                    {
                        ApplyEquipMainWeapon(wp, false); // do not sync/enable secondaries
                        _mainWeaponSetByPowerup = true;
                    }
                    else
                    {
                        float pickMagnitude = powerUp.magnitude > 0f ? powerUp.magnitude : 1.25f;

                        // subsequent weapon pick:
                        // - if chosen equals main's weapon -> try upgrade that weapon instance (or any equipped instance)
                        if (mainCurrent != null && mainCurrent.name == wp.name)
                        {
                            // try upgrade existing equipped weapon instance(s); if none matched, fall back to equipping prefab
                            if (!TryUpgradeIfAlreadyEquipped(wp, pickMagnitude))
                            {
                                ApplyEquipMainWeapon(wp); // default behaviour (sync allowed)
                            }
                        }
                        else
                        {
                            // if not the same as main, try to upgrade any matching secondary first; if none, add/enable a secondary and equip it
                            if (!TryUpgradeIfAlreadyEquipped(wp, pickMagnitude))
                            {
                                ApplyAddSecondary(wp);
                            }
                        }
                    }
                }
                break;

            case PowerUpType.AttackSpeed:
                {
                    var speed = powerUp as FireRateType;
                    float mag = (speed != null) ? speed.FireRate : (powerUp.magnitude > 0f ? powerUp.magnitude : 1f);
                    ApplyFireRateBuffToActiveWeapons(mag);
                }
                break;

            case PowerUpType.DamageIncrease:
                {
                    var dmg = powerUp as DamageType;
                    float mag = (dmg != null) ? dmg.Damage : (powerUp.magnitude > 0f ? powerUp.magnitude : 1f);
                    ApplyDamageBuffToActiveWeapons(mag);
                }
                break;

            case PowerUpType.HealthIncrease:
                powerUp.Apply(this.gameObject);
                break;
        }
    }

    private Weapon GetWeaponFromPowerUp(PowerUp powerUp)
    {
        if (powerUp == null) return null;
        var type = powerUp as WeaponChangeType;
        return type?.weapon;
    }

    // Apply damage multiplier only to weapons that are currently active/equipped at the time of picking
    private void ApplyDamageBuffToActiveWeapons(float multiplier)
    {
        if (multiplier <= 0f) return;
        var handles = this.GetComponentsInChildren<CharacterHandleWeapon>(true);
        foreach (var h in handles)
        {
            if (h == null) continue;
            var handleGO = h.gameObject;
            var ownerCharacter = h.GetComponentInParent<Character>();
            bool handleActive = (handleGO != null && handleGO.activeInHierarchy) || (ownerCharacter != null && ownerCharacter.gameObject.activeInHierarchy);
            if (!handleActive) continue;

            var w = h.CurrentWeapon;
            if (w == null) continue;

            float prev = 1f;
            _perWeaponDamageMultiplier.TryGetValue(w, out prev);
            float next = prev * multiplier;
            _perWeaponDamageMultiplier[w] = next;

            ApplyDamageToWeapon(w, next);
        }
    }

    // Apply fire-rate multiplier only to weapons that are currently active/equipped at the time of picking
    private void ApplyFireRateBuffToActiveWeapons(float speedMultiplier)
    {
        if (speedMultiplier <= 0f) return;
        var handles = this.GetComponentsInChildren<CharacterHandleWeapon>(true);
        foreach (var h in handles)
        {
            if (h == null) continue;
            var handleGO = h.gameObject;
            var ownerCharacter = h.GetComponentInParent<Character>();
            bool handleActive = (handleGO != null && handleGO.activeInHierarchy) || (ownerCharacter != null && ownerCharacter.gameObject.activeInHierarchy);
            if (!handleActive) continue;

            var w = h.CurrentWeapon;
            if (w == null) continue;

            float prev = 1f;
            _perWeaponFireRateMultiplier.TryGetValue(w, out prev);
            float next = prev * speedMultiplier;
            _perWeaponFireRateMultiplier[w] = next;

            ApplyFireRateToWeapon(w, next);
        }
    }

    // Try to apply damage multiplier to a specific weapon instance
    private void ApplyDamageToWeapon(Weapon w, float damageMultiplier)
    {
        if (w == null) return;
        var type = w.GetType();

        // try to set DamageMultiplier field/property if present
        var dmgField = type.GetField("DamageMultiplier", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (dmgField != null && dmgField.FieldType == typeof(float))
        {
            dmgField.SetValue(w, damageMultiplier);
            return;
        }
        var dmgProp = type.GetProperty("DamageMultiplier", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (dmgProp != null && dmgProp.PropertyType == typeof(float) && dmgProp.CanWrite)
        {
            dmgProp.SetValue(w, damageMultiplier);
            return;
        }

        // otherwise find a numeric damage-like field/property and scale it relative to a cached base value
        string[] candidateNames = new[] { "Damage", "WeaponDamage", "DamageCaused", "BaseDamage", "BaseDamageValue" };
        foreach (var name in candidateNames)
        {
            var fi = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi != null && IsNumericType(fi.FieldType))
            {
                float baseVal;
                if (!_weaponBaseDamageValue.TryGetValue(w, out baseVal))
                {
                    baseVal = Convert.ToSingle(fi.GetValue(w));
                    _weaponBaseDamageValue[w] = baseVal;
                }
                fi.SetValue(w, Convert.ChangeType(baseVal * damageMultiplier, fi.FieldType));
                return;
            }
            var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (pi != null && IsNumericType(pi.PropertyType) && pi.CanWrite)
            {
                float baseVal;
                if (!_weaponBaseDamageValue.TryGetValue(w, out baseVal))
                {
                    baseVal = Convert.ToSingle(pi.GetValue(w));
                    _weaponBaseDamageValue[w] = baseVal;
                }
                SetNumericProperty(pi, w, baseVal * damageMultiplier);
                return;
            }
        }

        // fallback: no known place to apply damage multiplier on this weapon
    }

    // Apply fire-rate multiplier to a specific weapon instance (relative to cached base TimeBetweenUses)
    private void ApplyFireRateToWeapon(Weapon w, float fireRateMultiplier)
    {
        if (w == null) return;
        if (!_weaponBaseTimeBetweenUses.TryGetValue(w, out float baseTime))
        {
            baseTime = w.TimeBetweenUses;
            _weaponBaseTimeBetweenUses[w] = baseTime;
        }
        float speed = Mathf.Max(0.0001f, fireRateMultiplier);
        w.TimeBetweenUses = baseTime / speed;
    }

    private void ApplyWeaponUpgrades()
    {
        //Upgrade logic here
    }
    
    private bool IsNumericType(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long) || type == typeof(decimal);
    }

    private void SetNumericProperty(PropertyInfo pi, object target, double value)
    {
        var pt = pi.PropertyType;
        if (pt == typeof(float)) pi.SetValue(target, (float)value);
        else if (pt == typeof(double)) pi.SetValue(target, value);
        else if (pt == typeof(int)) pi.SetValue(target, (int)Math.Round(value));
        else if (pt == typeof(long)) pi.SetValue(target, (long)Math.Round(value));
        else pi.SetValue(target, Convert.ChangeType(value, pt));
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

    // Modified: allow skipping secondary sync when equipping main (first power-up should not enable P2/P3)
    private void ApplyEquipMainWeapon(Weapon chosenWeapon, bool syncSecondaries = true)
    {
        var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
        if (mainHandle == null) return;

        if (chosenWeapon != null)
        {
            mainHandle.ChangeWeapon(chosenWeapon, chosenWeapon.name);
            guns[0].SetActive(false);
            switch (chosenWeapon.WeaponName)
            {
                case "ChainBow":
                    guns[1].SetActive(true);
                    break;
                case "ExplodeBow":
                    guns[3].SetActive(true);
                    break;
                case "MultiBow":
                    guns[2].SetActive(true);
                    break;
            }
            // apply per-weapon multipliers to the main handle (if any exist)
            ApplyPerWeaponMultipliersForHandle(mainHandle);

            if (syncSecondaries)
            {
                // only sync/enable secondaries when explicit sync requested
                SyncSecondariesToMainWeapon();
                // After syncing, ensure secondary multipliers applied
                ApplyGlobalStatMultipliers();
            }
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
    /// Apply per-weapon multipliers to all currently active handles.
    /// This replaces global application: only weapons that have entries in per-weapon dictionaries get modified.
    /// </summary>
    private void ApplyGlobalStatMultipliers()
    {
        var handles = this.GetComponentsInChildren<CharacterHandleWeapon>(true);

        // apply only to active handles/owners
        foreach (var h in handles)
        {
            if (h == null) continue;
            var handleGO = h.gameObject;
            var ownerCharacter = h.GetComponentInParent<Character>();
            bool handleActive = (handleGO != null && handleGO.activeInHierarchy) || (ownerCharacter != null && ownerCharacter.gameObject.activeInHierarchy);
            if (!handleActive) continue;

            var w = h.CurrentWeapon;
            if (w == null) continue;

            // apply per-weapon damage if exists
            if (_perWeaponDamageMultiplier.TryGetValue(w, out float dmgMul))
            {
                ApplyDamageToWeapon(w, dmgMul);
            }

            // apply per-weapon fire rate if exists
            if (_perWeaponFireRateMultiplier.TryGetValue(w, out float frMul))
            {
                ApplyFireRateToWeapon(w, frMul);
            }

            // re-equip so initialization logic runs (if needed)
            h.ChangeWeapon(w, w.name);
        }

        // purge bases for weapons no longer present
        var keys = _weaponBaseTimeBetweenUses.Keys.ToList();
        foreach (var k in keys)
        {
            var stillPresent = false;
            var handles2 = this.GetComponentsInChildren<CharacterHandleWeapon>(true);
            foreach (var hh in handles2)
            {
                if (hh?.CurrentWeapon == k) { stillPresent = true; break; }
            }
            if (!stillPresent)
            {
                _weaponBaseTimeBetweenUses.Remove(k);
                _perWeaponDamageMultiplier.Remove(k);
                _perWeaponFireRateMultiplier.Remove(k);
                _weaponBaseDamageValue.Remove(k);
            }
        }
    }

    // Apply per-weapon multipliers for a single handle (used after equipping one weapon)
    private void ApplyPerWeaponMultipliersForHandle(CharacterHandleWeapon handle)
    {
        if (handle == null) return;
        var w = handle.CurrentWeapon;
        if (w == null) return;

        if (_perWeaponDamageMultiplier.TryGetValue(w, out float dmgMul))
        {
            ApplyDamageToWeapon(w, dmgMul);
        }

        if (_perWeaponFireRateMultiplier.TryGetValue(w, out float frMul))
        {
            ApplyFireRateToWeapon(w, frMul);
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
        if (pickup.CompareTag("Upgrade"))
        {
            Signals.Get<EnergyPickupSignals>().Dispatch(1);
        }

        if (pickup.CompareTag("Drone"))
        {
            var randomUpgrade = pickup.GetComponent<IRandomUpgrade>();
            int index = randomUpgrade != null ? randomUpgrade.RandomUpgradeIndex : 0;
            Signals.Get<DronePickupSignals>().Dispatch(index);
        }
        // disable pickup collider to avoid multi-trigger and optionally destroy
        var col = pickup.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // optional: destroy pickup (commented out to let designer decide)
        Destroy(pickup);
    }

    /// <summary>
    /// Coroutine: đợi 1 frame để đảm bảo CharacterHandleWeapon/Character đã khởi tạo rồi equip weapon.
    /// Nếu disableAfter true thì sẽ disable GameObject sau khi equip xong.
    /// Ngoài ra đảm bảo Health.MasterHealth = null để tránh redirect damage về main.
    /// After equipping we re-apply per-weapon multipliers so newly enabled secondaries receive current buffs only if they were targeted previously.
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
            // apply per-weapon multipliers for this handle only (no global stacking)
            ApplyPerWeaponMultipliersForHandle(handle);
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

            // ensure secondary is active so its CharacterHandleWeapon can initialize and accept ChangeWeapon
            bool wasActive = s.activeSelf;
            if (!wasActive)
            {
                s.SetActive(true);
            }

            // ensure secondary's Health doesn't point to player's/master health
            var health = s.GetComponentInChildren<Health>();
            if (health != null)
            {
                health.MasterHealth = null;
                health.InitializeCurrentHealth();
                health.DamageEnabled();
            }

            var secondaryHandle = s.GetComponentInChildren<CharacterHandleWeapon>(true);
            if (secondaryHandle == null) continue;

            // equip same weapon prefab
            secondaryHandle.ChangeWeapon(currentWeapon, currentWeapon.name);

            // apply per-weapon multipliers to this secondary if any exist for this weapon instance
            ApplyPerWeaponMultipliersForHandle(secondaryHandle);
        }
    }

    /// <summary>
    /// Try to upgrade an already equipped weapon instance (main or secondary).
    /// Returns true if an upgrade was applied to an existing instance.
    /// </summary>
    private bool TryUpgradeIfAlreadyEquipped(Weapon chosenWeaponPrefab, float magnitude)
    {
        if (chosenWeaponPrefab == null) return false;

        var handles = this.GetComponentsInChildren<CharacterHandleWeapon>(true);
        foreach (var h in handles)
        {
            if (h == null) continue;
            var cur = h.CurrentWeapon;
            if (cur == null) continue;

            // match by name (consistent with existing code)
            if (cur.name == chosenWeaponPrefab.name)
            {
                Signals.Get<ApplyWeaponUpgrade>().Dispatch(cur.WeaponName);
                return true;
            }
        }
        return false;
    }
}