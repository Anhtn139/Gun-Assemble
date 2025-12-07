using System;
using UnityEngine;
using UnityEngine.UI;

public class HomeUI : MonoBehaviour
{
    [SerializeField] private Button quitButton;

    private void Start()
    {
        quitButton.onClick.AddListener(Application.Quit);
    }
}
