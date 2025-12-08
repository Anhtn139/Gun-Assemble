using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using DG.Tweening;

namespace MoreMountains.TopDownEngine
{
    public class LevelSelect : MonoBehaviour, IPointerClickHandler
    {
        public LevelInfo.LevelCondition levelInfo;
        public GameObject lockIcon;
        [SerializeField] GameObject highlight;
        [SerializeField] GameObject content;
        [SerializeField] TextMeshPro levelName;
        public bool isLocked = false;
        [SerializeField] private AudioSource audioSource;

        [Header("Highlight Float (DOTween)")]
        [SerializeField] private float floatAmplitude = 0.25f;
        [SerializeField] private float floatDuration = 1f;
        [SerializeField] private Ease floatEase = Ease.InOutSine;

        private Tween _floatTween;
        private Vector3 _highlightStartLocalPos;

        // store initial world position of this LevelSelect once (so MapScroll can use it later)
        private Vector3 _initialWorldPosition;
        public Vector3 InitialWorldPosition => _initialWorldPosition;

        public void SetLevel()
        {
            LevelController.Instance.CurrentLevelCondition = levelInfo;
            LevelController.Instance.CurrentLevel = levelInfo.LevelName;
            LevelController.Instance.LoadLevel("GamePlay");
        }

        private void Awake()
        {
            // capture initial world position once at Awake (before map may move)
            _initialWorldPosition = highlight.transform.position;

            if (PlayerPrefs.GetInt("CurrentLevel") < levelInfo.LevelName)
            {
                isLocked = true;
            }
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

            if (PlayerPrefs.GetInt("CurrentLevel") == levelInfo.LevelName)
            {
                if (highlight != null)
                {
                    highlight.SetActive(true);
                    StartFloatingEffect();
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (PlayerPrefs.GetInt("CurrentLevel") < levelInfo.LevelName) return;
            SetLevel();
            audioSource.Play();
        }

        private void OnEnable()
        {
            // if highlight was already active, ensure effect is running
            if (highlight != null && highlight.activeSelf)
            {
                StartFloatingEffect();
            }
        }

        private void OnDisable()
        {
            StopFloatingEffect();
        }

        private void OnDestroy()
        {
            StopFloatingEffect();
        }

        private void StartFloatingEffect()
        {
            // store start local position
            _highlightStartLocalPos = content.transform.localPosition;

            // prevent multiple tweens
            StopFloatingEffect();

            // move to startY + amplitude, loop infinitely with Yoyo for smooth float
            float targetY = _highlightStartLocalPos.y + floatAmplitude;
            _floatTween = content.transform.DOLocalMoveY(targetY, floatDuration)
                .SetEase(floatEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(this); // id with this component for easy kill if needed
        }

        private void StopFloatingEffect()
        {
            if (_floatTween != null && _floatTween.IsActive())
            {
                _floatTween.Kill(false);
                _floatTween = null;
            }

            // optionally snap back to start position
            if (highlight != null)
            {
                content.transform.localPosition = _highlightStartLocalPos;
            }
        }
    }
}
