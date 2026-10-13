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
    public FloatEventSO VolumeChangeEvent;

    [Header("广播")]
    public FloatEventSO SyncVolumeEvent;

    [Header("组件")]
    public AudioSource BGMSource;
    public AudioSource FXSource;
    public AudioMixer mixer;


    private void Start()
    {
        // 必须在 Start 而不是 Awake 里广播:
        // UIManager 是在自己的 OnEnable 里 syncVolumeEvent.OnEventRaised += ... 订阅的,
        // 而 Awake 阶段它还没订阅 -> OnEventRaised 是 null -> ?.Invoke() 直接静默丢掉,
        // 表现就是"开局滑块位置和实际音量对不上"。
        // Start 保证在本场景所有 Awake / OnEnable 都执行完之后才跑。
        if (mixer == null)
        {
            Debug.LogWarning("AudioManager.mixer 没有连, 音量无法同步");
            return;
        }

        float db;
        if (!mixer.GetFloat("MasterVolume", out db))
        {
            // GetFloat 返回 false = 这个参数没在 AudioMixer 窗口里暴露(Expose)
            Debug.LogWarning("AudioMixer 里没有暴露参数 \"MasterVolume\", 音量无法同步/修改");
            return;
        }

        // 广播【滑块位置 0..1】而不是分贝 —— 分贝只在音频系统内部出现,
        // UI 拿到就能直接塞给 Slider, 不需要知道任何换算公式。
        if (SyncVolumeEvent != null)
            SyncVolumeEvent.RaiseEvent(DbToSlider(db));
    }

    /// <summary>
    /// 滑块位置(0..1) → 分贝。
    /// 用对数而不是线性: 人耳对音量的感知接近对数, 线性映射会让"前半段几乎听不出变化,
    /// 后半段陡然变响"。对数下 0.5 大约是 -6dB(感知上的一半), 1.0 是 0dB(原始音量)。
    /// </summary>
    public static float SliderToDb(float value)
    {
        // Log10(0) = -Infinity, 必须挡住。这里同时把最低端当作静音
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }

    /// <summary>
    /// 分贝 → 滑块位置(0..1)。和 SliderToDb 严格互逆:
    ///   SliderToDb(0.5) = -6.0206  ->  DbToSlider(-6.0206) = 0.5
    ///   SliderToDb(1)   =  0       ->  DbToSlider(0)       = 1
    ///   SliderToDb(0)   = -80      ->  DbToSlider(-80)     = 0
    /// </summary>
    public static float DbToSlider(float db)
    {
        if (db <= -80f)
            return 0f;

        return Mathf.Pow(10f, db / 20f);
    }
    private void OnEnable()
    {
        FXEvent.OnEventRaised += OnFxEvent;
        BGMEvent.OnEventRaised += OnBGMEvent;
        VolumeChangeEvent.OnEventRaised += OnVolumeEvent;
    }

    private void OnDisable()
    {
        FXEvent.OnEventRaised -= OnFxEvent;
        BGMEvent.OnEventRaised -= OnBGMEvent;
        VolumeChangeEvent.OnEventRaised -= OnVolumeEvent;
    }

    private void OnVolumeEvent(float sliderValue)
    {
        // 收到的是滑块位置 0..1, 换算成 dB 才交给 mixer
        mixer.SetFloat("MasterVolume", SliderToDb(sliderValue));
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
