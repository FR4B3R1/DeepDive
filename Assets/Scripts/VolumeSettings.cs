using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer MyMixer;
    [SerializeField] private Slider MusicSlider;

    public void setMusicVolume()
    {
        float volume = MusicSlider.value;
        MyMixer.SetFloat("Music", volume);
    }

    public void setSFXVolume()
    {
        float volume = MusicSlider.value;
        MyMixer.SetFloat("Sounds", volume);
    }

}
