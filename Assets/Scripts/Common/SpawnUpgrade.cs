using System.Collections.Generic;
using UnityEngine;

public class Spawn : MonoBehaviour
{
    [SerializeField] private GameObject upgradePrefab;
    [SerializeField] private BoxCollider spawnZone;
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 3;
    [SerializeField] private float minSpacing = 1f; // khoảng cách tối thiểu giữa các prefab
    [SerializeField] private int maxAttemptsPerSpawn = 20; // số lần thử vị trí cho mỗi prefab

    void Start()
    {
        SpawnUpgrades();
    }

    void Update()
    {
        
    }

    private void SpawnUpgrades()
    {
        if (upgradePrefab == null || spawnZone == null)
        {
            Debug.LogWarning("upgradePrefab hoặc spawnZone chưa được gán!");
            return;
        }

        int minC = Mathf.Max(0, minCount);
        int maxC = Mathf.Max(minC, maxCount);

        int count = Random.Range(minC, maxC + 1);

        List<Vector3> spawnedPositions = new List<Vector3>();
        float minSpacingSqr = minSpacing * minSpacing;

        for (int i = 0; i < count; i++)
        {
            Vector3 chosenPos = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
            {
                Vector3 candidate = GetRandomPositionInZone();

                bool tooClose = false;
                for (int j = 0; j < spawnedPositions.Count; j++)
                {
                    if ((spawnedPositions[j] - candidate).sqrMagnitude < minSpacingSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    chosenPos = candidate;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                Instantiate(upgradePrefab, chosenPos, Quaternion.identity);
                spawnedPositions.Add(chosenPos);
            }
            else
            {
                Debug.LogWarning($"Không tìm được vị trí hợp lệ cho prefab #{i + 1} sau {maxAttemptsPerSpawn} lần thử. Bỏ qua.");
            }
        }
    }

    private Vector3 GetRandomPositionInZone()
    {
        Vector3 zoneCenter = spawnZone.bounds.center;
        Vector3 zoneSize = spawnZone.bounds.size;

        float randomX = Random.Range(zoneCenter.x - zoneSize.x / 2f, zoneCenter.x + zoneSize.x / 2f);
        float randomZ = Random.Range(zoneCenter.z - zoneSize.z / 2f, zoneCenter.z + zoneSize.z / 2f);
        float fixedY = zoneCenter.y;

        return new Vector3(randomX, fixedY, randomZ);
    }
}
