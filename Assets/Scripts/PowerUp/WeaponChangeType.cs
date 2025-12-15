using UnityEngine;

[CreateAssetMenu(fileName = "PowerUp", menuName = "Scriptable Objects/Weapon")]
public class WeaponChangeType : PowerUp
{
    public enum PowerUpWeapons { Arrow, Multi_Arrow,  Chain_Arrow}
    
    [SerializeField] private PowerUpWeapons weapon;
    [SerializeField] private float attackSpeed;
    [SerializeField] private float damage;
    [SerializeField] private int projectileCount;
}
