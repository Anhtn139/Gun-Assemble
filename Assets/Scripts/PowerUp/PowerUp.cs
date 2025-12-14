using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUp", menuName = "Scriptable Objects/PowerUp")]
public class PowerUp : ScriptableObject
{

    [Header("Identity")]
    [Tooltip("Tên hiển thị của power-up")]
    public string displayName;
    [Tooltip("Icon hiển thị trên UI")]
    public Sprite icon;
    [Tooltip("Loại power-up (dùng để logic/lọc)")]
    public CharacterLevelController.PowerUpType kind;
    [TextArea][Tooltip("Mô tả hiển thị cho người chơi")]
    public string description;

    [Header("Behaviour")]
    [Tooltip("Nếu true thì hiệu ứng có thời lượng, ngược lại áp dụng tức thì (vĩnh viễn)")]
    public bool isTimed = false;
    [Tooltip("Thời lượng (giây) nếu isTimed = true")]
    public float duration = 5f;
    [Tooltip("Độ mạnh chung (tỉ lệ hoặc giá trị), ý nghĩa tuỳ loại")]
    public float magnitude = 1f;

    [Header("Debug / Tuning")]
    [Tooltip("Cho phép xếp chồng nhiều lần")]
    public bool canStack = false;

    // --- API cơ bản để override trong các lớp con ---
    // Gọi khi muốn áp dụng hiệu ứng ngay lập tức (hoặc bắt đầu hiệu ứng timed)
    public virtual void Apply(GameObject target)
    {
        // Default: không làm gì. Lớp con override để thực hiện logic cụ thể.
        Debug.Log($"PowerUp.Apply: {displayName} áp dụng lên {target?.name ?? "null"}");
    }

    // Gọi để rollback/loại bỏ hiệu ứng (dùng với timed hoặc nếu muốn undo)
    public virtual void Remove(GameObject target)
    {
        // Default: không làm gì. Lớp con override để rollback nếu cần.
        Debug.Log($"PowerUp.Remove: {displayName} gỡ khỏi {target?.name ?? "null"}");
    }

    // Trả về một coroutine để executor (MonoBehaviour) khởi chạy khi cần áp dụng timed effect.
    // Ví dụ: StartCoroutine(myPowerUp.ApplyTemporary(this.gameObject));
    public IEnumerator ApplyTemporary(GameObject target)
    {
        Apply(target);

        if (isTimed && duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            Remove(target);
        }
    }

    // Hữu ích để hiển thị UI
    public virtual string GetLabel()
    {
        if (!string.IsNullOrEmpty(displayName)) return displayName;
        return kind.ToString();
    }

    public virtual string GetDescription()
    {
        if (!string.IsNullOrEmpty(description)) return description;
        return $"{GetLabel()} (magnitude: {magnitude}, duration: {duration}s)";
    }
}
