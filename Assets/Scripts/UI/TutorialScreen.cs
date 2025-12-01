using UnityEngine;
using UnityEngine.UI;

public class TutorialScreen : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image bgImage;
    [SerializeField] private Sprite[] sprites;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    private int currentSpriteIndex = 0;

    private void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNext);
        if (backButton != null) backButton.onClick.AddListener(OnBack);

        // initialize UI
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNext);
        if (backButton != null) backButton.onClick.RemoveListener(OnBack);
    }

    private void OnNext()
    {
        if (sprites == null || sprites.Length == 0) return;
        currentSpriteIndex = Mathf.Clamp(currentSpriteIndex + 1, 0, sprites.Length - 1);
        UpdateUI();
    }

    private void OnBack()
    {
        if (sprites == null || sprites.Length == 0) return;
        currentSpriteIndex = Mathf.Clamp(currentSpriteIndex - 1, 0, sprites.Length - 1);
        UpdateUI();
    }

    private void UpdateUI()
    {
        // update displayed sprite
        if (bgImage != null)
        {
            bgImage.sprite = (sprites != null && sprites.Length > 0) ? sprites[currentSpriteIndex] : null;
        }

        bool hasSprites = sprites != null && sprites.Length > 0;
        // hide back button on first, hide next button on last
        if (backButton != null) backButton.gameObject.SetActive(hasSprites && currentSpriteIndex > 0);
        if (nextButton != null) nextButton.gameObject.SetActive(hasSprites && currentSpriteIndex < sprites.Length - 1);
    }
}
