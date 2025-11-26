using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelInfo", menuName = "Scriptable Objects/LevelInfo")]
public class LevelInfo : ScriptableObject
{
    [Serializable]
    public class LevelCondition
    {
        public int LevelName;
        [Min(0)] public int TotalEnemy = 0;
        [Min(0f)] public float TimeToComplete = 300f;
    }

    [SerializeField] LevelCondition[] levels = Array.Empty<LevelCondition>();

    public LevelCondition[] LevelsArray => levels;
    public int Count => levels?.Length ?? 0;

    public LevelCondition Get(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return levels[index];
    }

    public bool TryGet(int index, out LevelCondition condition)
    {
        if (levels != null && index >= 0 && index < levels.Length)
        {
            condition = levels[index];
            return true;
        }
        condition = null;
        return false;
    }

    public LevelCondition GetByName(int name)
    {
        if (levels == null) return null;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i].LevelName == name)
                return levels[i];
        }
        return null;
    }

    void OnValidate()
    {
        levels ??= Array.Empty<LevelCondition>();
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] ??= new LevelCondition();
            levels[i].LevelName = i + 1;
            levels[i].TimeToComplete = 300f;
            levels[i].TotalEnemy = 30 + i * 10;
        }
    }
}
