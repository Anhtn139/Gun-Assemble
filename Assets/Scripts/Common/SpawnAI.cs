using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnAI : MonoBehaviour
{
    [SerializeField] private GameObject AIPrefab;

    [Header("Spawn points (required)")]
    [SerializeField] private Transform[] spawnPoints; // primary spawn anchors

    [Header("Counts & spacing")]
    [SerializeField] private int spawnCount = 4;
    [SerializeField] private float minSpacing = 1f; // minimum spacing between spawned AI
    [SerializeField] private int maxAttemptsPerSpawn = 5; // attempts per AI position

    [Header("Batching")]
    [SerializeField] private int batchSize = 2; // total AI spawned per batch (distributed across points)
    [SerializeField] private float batchInterval = 2f; // seconds between batches
    [SerializeField] private float startDelay = 0f; // delay before first batch

    [Header("Concurrency limit")]
    [SerializeField] private int maxConcurrentAI = 50; // maximum AI that may exist at the same time

    // internal filtered list of valid spawn points (non-null)
    private Transform[] validSpawnPoints;

    void Start()
    {
        if (AIPrefab == null)
        {
            Debug.LogWarning("AIPrefab chưa được gán!");
            return;
        }

        // filter out any null entries in spawnPoints
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var list = new List<Transform>(spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null) list.Add(spawnPoints[i]);
            }
            validSpawnPoints = list.ToArray();
        }
        else
        {
            validSpawnPoints = new Transform[0];
        }

        if (validSpawnPoints.Length == 0)
        {
            Debug.LogWarning("Không có spawnPoints hợp lệ. Hãy gán ít nhất một Transform trong spawnPoints.");
            return;
        }

        // remove existing enemies at start
        foreach (var o in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Destroy(o);
        }

        // no longer using spawnCount to limit overall spawns — concurrency is governed by maxConcurrentAI
        StartCoroutine(SpawnBatchesRoutine());
    }

    private int GetActiveEnemyCount()
    {
        return GameObject.FindGameObjectsWithTag("Enemy").Length;
    }

    // check against currently active enemies and positions already chosen in this batch
    private bool IsTooClose(Vector3 pos, float minSpacingSqr, List<Vector3> batchPositions)
    {
        // check against active enemies
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            if ((enemies[i].transform.position - pos).sqrMagnitude < minSpacingSqr)
                return true;
        }

        // check against positions chosen earlier in this batch so they don't stack
        for (int i = 0; i < batchPositions.Count; i++)
        {
            if ((batchPositions[i] - pos).sqrMagnitude < minSpacingSqr)
                return true;
        }

        return false;
    }

    private IEnumerator SpawnBatchesRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        float minSpacingSqr = minSpacing * minSpacing;

        // Continually spawn batches. Concurrency is enforced by maxConcurrentAI.
        while (true)
        {
            int toSpawnThisBatch = Mathf.Max(1, batchSize);
            int spawnedThisBatch = 0;

            // positions chosen during this batch (to avoid stacking)
            List<Vector3> batchPositions = new List<Vector3>();

            // Evenly distribute "toSpawnThisBatch" across available spawnPoints
            int pointCount = validSpawnPoints.Length;
            int basePerPoint = toSpawnThisBatch / pointCount;
            int remainder = toSpawnThisBatch % pointCount;

            for (int p = 0; p < pointCount; p++)
            {
                int spawnFromPoint = basePerPoint + (p < remainder ? 1 : 0);
                if (spawnFromPoint <= 0) continue; // nothing to spawn from this point this batch

                Transform sp = validSpawnPoints[p];
                Vector3 basePos = sp.position;

                for (int s = 0; s < spawnFromPoint; s++)
                {
                    Vector3 chosenPos = Vector3.zero;
                    bool found = false;

                    // Try to find a valid nearby position (small random offset around the base position)
                    for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
                    {
                        float jitterRange = Mathf.Max(0.0f, minSpacing * 0.5f);
                        Vector3 jitter = new Vector3(Random.Range(-jitterRange, jitterRange), 0f, Random.Range(-jitterRange, jitterRange));
                        chosenPos = basePos + jitter;

                        if (!IsTooClose(chosenPos, minSpacingSqr, batchPositions))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // fallback to basePos
                        chosenPos = basePos;
                    }

                    // Enforce concurrent AI limit: if limit reached, wait until below limit
                    if (maxConcurrentAI > 0)
                    {
                        while (GetActiveEnemyCount() >= maxConcurrentAI)
                        {
                            yield return null; // wait a frame and check again
                        }
                    }

                    // Instantiate the AI
                    Instantiate(AIPrefab, chosenPos, Quaternion.identity);
                    batchPositions.Add(chosenPos);
                    spawnedThisBatch++;

                    if (spawnedThisBatch >= toSpawnThisBatch)
                        break;
                }

                if (spawnedThisBatch >= toSpawnThisBatch)
                    break;
            }

            // wait interval between batches
            yield return new WaitForSeconds(batchInterval);
        }
    }
}
