using UnityEngine;
[CreateAssetMenu(fileName = "PowerUp", menuName = "Scriptable Objects/FireRate")]
public class FireRateType : PowerUp
{
    [SerializeField] private float fireRate;
    
    public float FireRate => fireRate;
}
