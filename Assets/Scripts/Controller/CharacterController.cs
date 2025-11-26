using UnityEngine;
using MoreMountains.TopDownEngine;
using System.Collections;
using MoreMountains.Tools;

public class CharacterController : MonoBehaviour
{
    [Header("Secondaries")]
    [Tooltip("Gán 2 character phụ (GameObject). Mặc định để inactive trong prefab)")]
    [SerializeField] private GameObject[] SecondaryCharacters = new GameObject[2];

    [Header("Upgrade / Weapon")]
    [Tooltip("Danh sách vũ khí sẽ đổi khi đủ số lượng pickups (gán prefab Weapon từ TopDown Engine)")]
    [SerializeField] private Weapon[] UpgradedWeapons;
    [Tooltip("Số pickup cần thu thập để kích hoạt đổi vũ khí (mặc định = 3)")]
    [SerializeField] private int PickupsNeededForUpgrade = 3;

    // số secondary đã được enable
    private int _enabledSecondaries = 0;
    // tổng số pickup đã thu thập hiện tại
    private int _pickupCount = 0;
    // số lần đã áp dụng upgrade (dùng để chọn weapon tiếp theo trong mảng)
    private int _upgradeAppliedCount = 0;

    /// <summary>
    /// Gọi khi nhặt 1 upgrade — sẽ tăng bộ đếm pickup, enable 1 secondary (nếu còn).
    /// Khi đạt PickupsNeededForUpgrade thì tiến hành đổi vũ khí.
    /// </summary>
    public void EnableNextSecondary()
    {
        // tăng bộ đếm pickup
        _pickupCount++;

        // bật secondary kế tiếp nếu còn (giữ tối đa SecondaryCharacters.Length active)
        for (int i = 0; i < SecondaryCharacters.Length; i++)
        {
            var s = SecondaryCharacters[i];
            if (s == null) continue;
            if (!s.activeSelf)
            {
                s.SetActive(true);
                _enabledSecondaries++;

                // ensure secondary's Health doesn't point to player's/master health
                var health = s.GetComponentInChildren<Health>();
                if (health != null)
                {
                    health.MasterHealth = null;
                    health.InitializeCurrentHealth();
                    health.DamageEnabled();
                }

                // NOTE: Không equip preview weapon ở đây nữa.
                // Việc đổi vũ khí sẽ chỉ diễn ra khi đạt ngưỡng pickups (ApplyUpgradeAndResetSecondaries).
                break;
            }
        }

        // chỉ đổi vũ khí khi đạt tới ngưỡng pickup (mặc định 3)
        if (_pickupCount >= Mathf.Max(1, PickupsNeededForUpgrade))
        {
            ApplyUpgradeAndResetSecondaries();
            // reset bộ đếm pickup sau khi áp dụng upgrade
            _pickupCount = 0;
        }
        WeaponType type = (WeaponType)_upgradeAppliedCount;
        Signals.Get<ChangeWeaponSignal>().Dispatch(type, _enabledSecondaries);
    }

    /// <summary>
    /// Lấy weapon sẽ được áp dụng tiếp theo từ mảng UpgradedWeapons.
    /// Nếu hết mảng, trả về phần tử cuối cùng; nếu mảng rỗng trả về null.
    /// </summary>
    private Weapon GetNextUpgradedWeapon()
    {
        if (UpgradedWeapons == null || UpgradedWeapons.Length == 0) return null;
        int index = Mathf.Clamp(_upgradeAppliedCount, 0, UpgradedWeapons.Length - 1);
        return UpgradedWeapons[index];
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

    /// <summary>
    /// Khi đủ số pickup: đổi vũ khí character chính và secondary giống nhau, rồi tắt secondaries.
    /// </summary>
    private void ApplyUpgradeAndResetSecondaries()
    {
        // chọn weapon để áp dụng
        Weapon toApply = GetNextUpgradedWeapon();

        // change main weapon (main thường đã sẵn sàng)
        var mainHandle = this.GetComponentInChildren<CharacterHandleWeapon>();
        if ((toApply != null) && (mainHandle != null))
        {
            mainHandle.ChangeWeapon(toApply, toApply.name);
        }

        // change weapon on secondaries (so they have same weapon) and then disable them
        foreach (var s in SecondaryCharacters)
        {
            if (s == null) continue;

            // If secondary is inactive, activate first so its CharacterHandleWeapon can initialize
            bool wasActive = s.activeSelf;
            if (!wasActive)
            {
                s.SetActive(true);

                // ensure secondary's health is independent immediately after activation
                var health = s.GetComponentInChildren<Health>();
                if (health != null)
                {
                    health.MasterHealth = null;
                    health.InitializeCurrentHealth();
                    health.DamageEnabled();
                }
            }

            // equip and then disable after a frame to ensure proper initialization & weapon instantiation
            StartCoroutine(EquipAfterNextFrame(s, toApply, true));
        }

        // tăng chỉ số đã áp dụng upgrade (để lần sau chọn weapon tiếp theo trong mảng)
        _upgradeAppliedCount++;

        // reset counter of enabled secondaries
        _enabledSecondaries = 0;
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
            // cố gắng equip weapon giống main nếu nó tồn tại trong UpgradedWeapons list, hoặc equip phần tử đầu nếu không
            if (UpgradedWeapons != null && UpgradedWeapons.Length > 0)
            {
                // chọn phần tử theo _upgradeAppliedCount - 1 (vũ khí hiện đang được áp dụng) nếu hợp lệ
                int idx = Mathf.Clamp(_upgradeAppliedCount - 1, 0, UpgradedWeapons.Length - 1);
                secondaryHandle.ChangeWeapon(UpgradedWeapons[idx], UpgradedWeapons[idx].name);
            }
            else
            {
                // fallback: try to equip the exact same weapon instance (may not be needed/desired)
                if (currentWeapon != null)
                {
                    secondaryHandle.ChangeWeapon(currentWeapon, currentWeapon.name);
                }
            }
        }
    }

    // Start / Update (nếu cần sau này)
    void Start()
    {
        // ensure secondaries start disabled if assigned
        foreach (var s in SecondaryCharacters)
        {
            if (s != null) s.SetActive(false);
        }
    }

    /// <summary>
    /// Nhận trigger của các upgrade (tag = "Upgrade").
    /// Nếu collider là con của object tag "Upgrade" thì vẫn xử lý (kiểm tra cả root).
    /// Khi bắt được upgrade, gọi EnableNextSecondary() và destroy pickup.
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

        // xử lý pickup
        EnableNextSecondary();

        // destroy pickup object (nếu muốn giữ khác, chỉnh ở đây)
        Destroy(pickup);
    }
}