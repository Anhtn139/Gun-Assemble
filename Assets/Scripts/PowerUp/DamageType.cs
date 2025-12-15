using UnityEngine;

public class DamageType : PowerUp
{
    [SerializeField] private float damage;
    
    public float Damage => damage;
}
