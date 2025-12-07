using System;
using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.TopDownEngine;

public class VFXColliderMovement : MonoBehaviour
{
    public VisualEffect vfx;  // Tham chiếu đến VisualEffect component
    public List<Vector3> positions;  // Danh sách các vị trí cho ColliderPos
    private int currentIndex = 0;  // Chỉ mục hiện tại trong danh sách
    private float timeSinceLastMove = 0f;  // Thời gian kể từ lần cập nhật cuối
    public float updateRate = 2f;  // T // Tốc độ Lerp (di chuyển nhanh hay chậm)

    void Start()
    {
        // Kiểm tra nếu chưa có VFX component thì lấy từ GameObject
        if (vfx == null)
            vfx = GetComponent<VisualEffect>();
        
        if (positions.Count == 0)
        {
            Debug.LogError("Danh sách các vị trí trống!");
        }
        var pos = FindObjectsByType<LevelSelect>(FindObjectsSortMode.None);
        var sortedObjects = pos.OrderBy(obj => obj.transform.GetSiblingIndex()).ToArray();
        foreach (var p in sortedObjects)
        {
            if (!p.isLocked)
            {
                positions.Add(p.transform.position);
            }
        }
    }

    /*private void Awake()
    {
        DontDestroyOnLoad(gameObject); 
    }*/

    void Update()
    {
        // Tăng thời gian kể từ lần cập nhật cuối
        timeSinceLastMove += Time.deltaTime;

        // Kiểm tra xem có đến lúc cập nhật ColliderPos không
        if (timeSinceLastMove >= updateRate)
        {
            timeSinceLastMove = 0f;  // Reset thời gian

            // Cập nhật ColliderPos từ danh sách các vị trí
            vfx.SetVector3("ColliderPos", positions[currentIndex]);

            // Cập nhật chỉ mục để di chuyển đến vị trí tiếp theo trong danh sách
            currentIndex = (currentIndex + 1) % positions.Count;
        }
    }
}