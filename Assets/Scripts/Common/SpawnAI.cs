using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnAI : MonoBehaviour
{
    [SerializeField] private GameObject AIPrefab;
    [SerializeField] private BoxCollider spawnZone;
    [SerializeField] private int spawnCount = 4;
    [SerializeField] private float minSpacing = 1f; // khoảng cách tối thiểu giữa các AI spawn
    [SerializeField] private int maxAttemptsPerSpawn = 20; // số lần thử vị trí trên mỗi AI

    [Header("Batching")]
    [SerializeField] private int batchSize = 2; // số AI spawn mỗi đợt
    [SerializeField] private float batchInterval = 2f; // thời gian (s) giữa các đợt
    [SerializeField] private float startDelay = 0f; // delay trước khi bắt đầu spawn đợt đầu

    void Start()
    {
        if (AIPrefab == null || spawnZone == null)
        {
            Debug.LogWarning("AIPrefab hoặc spawnZone chưa được gán!");
            return;
        }

        foreach (var o in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Destroy(o);
        }
        StartCoroutine(SpawnBatchesRoutine());
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

        int remaining = Mathf.Max(0, spawnCount);
        List<Vector3> spawnedPositions = new List<Vector3>();
        float minSpacingSqr = minSpacing * minSpacing;

        // world-aligned bounds của BoxCollider
        Bounds bounds = spawnZone.bounds;
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        float fixedY = bounds.center.y; // giữ Y cố định (có thể điều chỉnh nếu muốn spawn trên mặt top bằng bounds.max.y)

        while (remaining > 0)
        {
            int toSpawnThisBatch = Mathf.Clamp(batchSize, 1, remaining);

            for (int i = 0; i < toSpawnThisBatch; i++)
            {
                Vector3 chosenPos = Vector3.zero;
                bool found = false;

                for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
                {
                    // Chọn ngẫu nhiên một cạnh trong 4 cạnh của hình chữ nhật (trên mặt ngang x-z)
                    int edge = Random.Range(0, 4);
                    float rx = Random.Range(minX, maxX);
                    float rz = Random.Range(minZ, maxZ);

                    switch (edge)
                    {
                        case 0: // cạnh z = minZ (bottom)
                            chosenPos = new Vector3(rx, fixedY, minZ);
                            break;
                        case 1: // cạnh z = maxZ (top)
                            chosenPos = new Vector3(rx, fixedY, maxZ);
                            break;
                        case 2: // cạnh x = minX (left)
                            chosenPos = new Vector3(minX, fixedY, rz);
                            break;
                        case 3: // cạnh x = maxX (right)
                            chosenPos = new Vector3(maxX, fixedY, rz);
                            break;
                    }

                    // kiểm tra khoảng cách với các vị trí đã spawn để tránh quá gần nhau
                    bool tooClose = false;
                    for (int j = 0; j < spawnedPositions.Count; j++)
                    {
                        if ((spawnedPositions[j] - chosenPos).sqrMagnitude < minSpacingSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    Instantiate(AIPrefab, chosenPos, Quaternion.identity);
                    spawnedPositions.Add(chosenPos);
                    remaining--;
                }
                else
                {
                    // Nếu không tìm được vị trí hợp lệ sau maxAttemptsPerSpawn, vẫn spawn ở vị trí cuối cùng thử được (hoặc rìa ngẫu nhiên)
                    // chosenPos sẽ giữ lần thử cuối cùng; nếu chưa thử (trường hợp maxAttemptsPerSpawn == 0), fallback về center cạnh
                    if (chosenPos == Vector3.zero)
                    {
                        // fallback đơn giản: trung điểm của một cạnh ngẫu nhiên
                        int edge = Random.Range(0, 4);
                        float rx = (minX + maxX) * 0.5f;
                        float rz = (minZ + maxZ) * 0.5f;
                        switch (edge)
                        {
                            case 0:
                                chosenPos = new Vector3(rx, fixedY, minZ);
                                break;
                            case 1:
                                chosenPos = new Vector3(rx, fixedY, maxZ);
                                break;
                            case 2:
                                chosenPos = new Vector3(minX, fixedY, rz);
                                break;
                            case 3:
                                chosenPos = new Vector3(maxX, fixedY, rz);
                                break;
                        }
                    }

                    Instantiate(AIPrefab, chosenPos, Quaternion.identity);
                    spawnedPositions.Add(chosenPos);
                    remaining--;

                    Debug.LogWarning($"Không tìm được vị trí đủ xa sau {maxAttemptsPerSpawn} lần thử. Vẫn spawn AI tại {chosenPos}.");
                }
            }

            // Nếu còn AI cần spawn, chờ interval trước khi spawn đợt tiếp theo
            if (remaining > 0)
            {
                yield return new WaitForSeconds(batchInterval);
            }
            else
            {
                break;
            }
        }
    }
}
