using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSettingsSFX : MonoBehaviour
{
    [SerializeField] private AudioMixer Mixer;
    [SerializeField] private Slider SFXSlider;


    public void setSFXVolume()
    {
        float volume = SFXSlider.value;
        Mixer.SetFloat("Sounds", volume);
    }

}
