using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public enum ObjectType { Button, Toggle, PopUp }
public class AudioPlayer : MonoBehaviour
{
    private Button playButton;
    private AudioClip clip;
    private AudioSource audioSource;
    [SerializeField] private ObjectType objectType = ObjectType.Button; // [ Button, Toggle, PopUp]

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        clip = GetComponent<AudioSource>().clip;
        switch (objectType)
        {
            case ObjectType.Button:
                playButton = GetComponent<Button>();
                playButton.onClick.AddListener(() =>
                {
                    PlayClip(clip, transform.position, audioSource.outputAudioMixerGroup);
                });
                break;
            case ObjectType.Toggle:
                GetComponent<Toggle>().onValueChanged.AddListener(value =>
                {
                    if (value) PlayClip(clip, transform.position, audioSource.outputAudioMixerGroup);
                });
                break;
            case ObjectType.PopUp:
                
                break;
        }
    }

    public static async void PlayClip(AudioClip clip, Vector3 position, AudioMixerGroup mixerGroup, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("SFXPlayer: Clip is null");
            return;
        }

        // Tạo object tạm
        GameObject temp = new GameObject("SFX_TempObject");
        AudioSource source = temp.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = mixerGroup;

        // Thiết lập AudioSource
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = 0f; // 0 = UI (2D), 1 = 3D

        // Đặt vị trí object
        temp.transform.position = position;

        // Di chuyển object vào đúng scene
        SceneManager.MoveGameObjectToScene(temp, SceneManager.GetActiveScene());
        DontDestroyOnLoad(temp);
        // Phát âm thanh
        source.Play();

        // Đợi âm thanh phát xong
        int ms = Mathf.CeilToInt(clip.length * 1000);
        await Task.Delay(ms);

        // Hủy object
        Object.Destroy(temp);
    }
}
