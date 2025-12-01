using System;
using System.Collections;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject splashScreen;
    private void Awake()
    {
        StartCoroutine(ShowSplash());
    }
    
    private IEnumerator ShowSplash()
    {
        yield return new WaitForSeconds(4f);
        splashScreen.SetActive(false);
        gameObject.SetActive(false);
    }
}
