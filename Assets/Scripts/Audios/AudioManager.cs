using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [Header("事件监听")]
    public PlayAudioEventSO BGMEvent;
    public PlayAudioEventSO FXEvent;

    [Header("组件")]
    public AudioSource BGMSource;
    public AudioSource FXSource;

    private void OnEnable()
    {
        FXEvent.OnEventRaised += OnFxEvent;
        BGMEvent.OnEventRaised += OnBGMEvent;
    }

    private void OnDisable()
    {
        FXEvent.OnEventRaised -= OnFxEvent;
        BGMEvent.OnEventRaised -= OnBGMEvent;
    }

    private void OnBGMEvent(AudioClip clip)
    {
        BGMSource.clip = clip;
        BGMSource.Play();
    }

    private void OnFxEvent(AudioClip clip)
    {
        FXSource.clip = clip;
        FXSource.Play();
    }
}
