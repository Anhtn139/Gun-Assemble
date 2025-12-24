using UnityEngine;
[CreateAssetMenu(fileName = "PowerUp", menuName = "Scriptable Objects/Damage")]
public class DamageType : PowerUp
{
    [SerializeField] private float damage;
    
    public float Damage => damage;
}
