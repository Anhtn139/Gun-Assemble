using System.Collections.Generic;
using UnityEngine;

public class SpawnUpgrade : MonoBehaviour
{
    [Header("Prefab & Points")]
    [SerializeField] private GameObject upgradePrefab;
    [Tooltip("Các điểm spawn đặt sẵn. Số điểm nên nhiều hơn upgradeCount.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Số lượng")]
    [SerializeField] private int upgradeCount = 3;

    private Transform[] _validPoints;

    void Start()
    {
        if (upgradePrefab == null)
        {
            Debug.LogWarning("[SpawnUpgrade] upgradePrefab chưa được gán!");
            return;
        }

        _validPoints = GetValidSpawnPoints();
        if (_validPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnUpgrade] Không có spawn point hợp lệ.");
            return;
        }

        SpawnUpgrades();
    }

    private Transform[] GetValidSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return new Transform[0];

        var list = new List<Transform>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
                list.Add(spawnPoints[i]);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Spawn upgrade tại các point ngẫu nhiên. Số point nhiều hơn upgrade nên sẽ random chọn vị trí.
    /// </summary>
    private void SpawnUpgrades()
    {
        int pointCount = _validPoints.Length;
        int toSpawn = Mathf.Clamp(upgradeCount, 0, pointCount);

        if (toSpawn == 0) return;

        // Shuffle indices: Fisher-Yates
        int[] indices = new int[pointCount];
        for (int i = 0; i < pointCount; i++)
            indices[i] = i;

        for (int i = pointCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < toSpawn; i++)
        {
            int idx = indices[i];
            Vector3 pos = _validPoints[idx].position;
            Instantiate(upgradePrefab, pos, Quaternion.identity);
        }
    }

    /// <summary>
    /// Gọi khi reload: xóa upgrades hiện tại và spawn lại tại vị trí random mới.
    /// </summary>
    public void Reload()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Drone"))
        {
            Destroy(go);
        }

        if (_validPoints != null && _validPoints.Length > 0 && upgradePrefab != null)
        {
            SpawnUpgrades();
        }
    }
}
