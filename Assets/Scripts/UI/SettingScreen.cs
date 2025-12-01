using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

public class SettingScreen : MonoBehaviour
{
    [SerializeField] private Slider masterVolume;
    [SerializeField] private Slider BGMVolume;
    [SerializeField] private Slider SFXVolume;
    
    private void Awake()
    {
        masterVolume.onValueChanged.AddListener(arg0 =>
        {
            MMSoundManager.Instance.SetVolumeMaster(arg0);
        });
        
        BGMVolume.onValueChanged.AddListener(arg0 =>
        {
            MMSoundManager.Instance.SetVolumeMusic(arg0);
        });
        
        SFXVolume.onValueChanged.AddListener(arg0 =>
        {
            MMSoundManager.Instance.SetVolumeSfx(arg0);
        });
    }
}
