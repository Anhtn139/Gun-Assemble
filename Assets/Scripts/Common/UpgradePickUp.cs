using UnityEngine;

/// <summary>
/// Interface để lấy id của object đã random.
/// </summary>
public interface IRandomUpgrade
{
    int RandomUpgradeIndex { get; }
}

/// <summary>
/// Hiển thị vũ khí để pick up. Mỗi lần spawn sẽ random hiện một vũ khí từ list children.
/// </summary>
public class UpgradePickUp : MonoBehaviour, IRandomUpgrade
{
    [Tooltip("Container chứa các child là model vũ khí (VD: object Weapons). Để trống thì dùng chính transform này.")]
    [SerializeField] private Transform weaponContainer;

    private int _randomUpgradeIndex = -1;

    public int RandomUpgradeIndex => _randomUpgradeIndex;

    void Start()
    {
        ShowRandomWeapon();
    }

    /// <summary>
    /// Chọn ngẫu nhiên 1 child để hiện, tắt các child còn lại.
    /// </summary>
    private void ShowRandomWeapon()
    {
        Transform container = weaponContainer != null ? weaponContainer : transform;
        int childCount = container.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning($"[UpgradePickUp] Không có child nào trong container {container.name}.");
            return;
        }

        _randomUpgradeIndex = Random.Range(0, childCount);

        for (int i = 0; i < childCount; i++)
        {
            container.GetChild(i).gameObject.SetActive(i == _randomUpgradeIndex);
        }
    }
}
