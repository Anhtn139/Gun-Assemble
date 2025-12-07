using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace MoreMountains.TopDownEngine
{
    public class LevelSelect : MonoBehaviour, IPointerClickHandler
    {
        public LevelInfo.LevelCondition levelInfo;
        public GameObject lockIcon;
        [SerializeField] TextMeshPro levelName;
        public bool isLocked = false;

        public void SetLevel()
        {
            LevelController.Instance.CurrentLevelCondition = levelInfo;
            LevelController.Instance.CurrentLevel = levelInfo.LevelName;
            LevelController.Instance.LoadLevel("GamePlay");
        }

        private void Start()
        {
            levelName.text = levelInfo.LevelName.ToString();
            if (PlayerPrefs.GetInt("CurrentLevel") >= levelInfo.LevelName)
            {
                lockIcon.SetActive(false);
                isLocked = false;
            }
            else
            {
                isLocked = true;
            }
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (PlayerPrefs.GetInt("CurrentLevel") < levelInfo.LevelName) return;
            SetLevel();
        }

        private void Awake()
        {
            if (PlayerPrefs.GetInt("CurrentLevel") < levelInfo.LevelName)
            {
                isLocked = true;
            }
        }
    }
}
