using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnUpgrade : MonoBehaviour
{
    [SerializeField] private GameObject upgradePrefab;
    [SerializeField] private BoxCollider spawnZone;
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 3;
    [SerializeField] private float minSpacing = 1f; // khoảng cách tối thiểu giữa các prefab
    [SerializeField] private int maxAttemptsPerSpawn = 20; // số lần thử vị trí cho mỗi prefab

    [Header("Batching")]
    [SerializeField] private int batchSize = 1; // số prefab spawn mỗi đợt
    [SerializeField] private float batchInterval = 1f; // thời gian (s) giữa các đợt
    [SerializeField] private float startDelay = 0f; // delay trước khi bắt đầu đợt đầu

    protected Coroutine _spawnCoroutine = null;

    void Start()
    {
        if (upgradePrefab == null || spawnZone == null)
        {
            Debug.LogWarning("upgradePrefab hoặc spawnZone chưa được gán!");
            return;
        }

        _spawnCoroutine = StartCoroutine(SpawnBatchesRoutine());
    }

    void OnDisable()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    void Update()
    {
        
    }

    private IEnumerator SpawnBatchesRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        int minC = Mathf.Max(0, minCount);
        int maxC = Mathf.Max(minC, maxCount);
        int remaining = Random.Range(minC, maxC + 1);

        List<Vector3> spawnedPositions = new List<Vector3>();
        float minSpacingSqr = minSpacing * minSpacing;

        // world-aligned bounds của BoxCollider
        Bounds bounds = spawnZone.bounds;
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        float fixedY = bounds.center.y;

        while (remaining > 0)
        {
            int toSpawnThisBatch = Mathf.Clamp(batchSize, 1, remaining);

            for (int i = 0; i < toSpawnThisBatch; i++)
            {
                Vector3 chosenPos = Vector3.zero;
                bool found = false;

                Vector3 lastCandidate = Vector3.zero;
                bool attemptedAny = false;

                for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
                {
                    Vector3 candidate = GetRandomPositionInZone();
                    attemptedAny = true;
                    lastCandidate = candidate;

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

                if (!found)
                {
                    // fallback: nếu đã có lần thử thì dùng lastCandidate, nếu không thì center zone
                    if (attemptedAny)
                    {
                        chosenPos = lastCandidate;
                    }
                    else
                    {
                        Vector3 zoneCenter = spawnZone.bounds.center;
                        chosenPos = new Vector3(zoneCenter.x, zoneCenter.y, zoneCenter.z);
                    }

                    Debug.LogWarning($"Không tìm được vị trí hợp lệ sau {maxAttemptsPerSpawn} lần thử. Vẫn spawn tại {chosenPos}.");
                }

                Instantiate(upgradePrefab, chosenPos, Quaternion.identity);
                spawnedPositions.Add(chosenPos);
                remaining--;
            }

            if (remaining > 0)
            {
                yield return new WaitForSeconds(batchInterval);
            }
        }

        _spawnCoroutine = null;
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
