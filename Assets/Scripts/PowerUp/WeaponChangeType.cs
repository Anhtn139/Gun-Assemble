using MoreMountains.TopDownEngine;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUp", menuName = "Scriptable Objects/Weapon")]
public class WeaponChangeType : PowerUp
{
    public enum PowerUpWeapons { Arrow, Multi_Arrow,  Chain_Arrow, Pierce_Arrow}
    
    public PowerUpWeapons weaponType;
    public float attackSpeed;
    public float damage;
    public int projectileCount;
    public ProjectileWeapon weapon;
}
