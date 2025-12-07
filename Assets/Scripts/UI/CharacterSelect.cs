using UnityEngine;

public class CharacterSelect : MonoBehaviour
{
    
    public void SetSkin(int skinID)
    {
        LevelController.Instance.skinID = skinID;
    }
}
