using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMountains.TopDownEngine
{
    public class LevelSelect : MonoBehaviour
    {
        public LevelInfo.LevelCondition levelInfo;
        public GameObject lockIcon;

        public void SetLevel()
        {
            LevelController.Instance.CurrentLevelCondition = levelInfo;
            LevelController.Instance.LoadLevel("GamePlay");
        }
    }
}
