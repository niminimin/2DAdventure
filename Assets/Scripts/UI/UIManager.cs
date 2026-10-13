using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public PlayerStateBar PlayerStateBar;
    public GameObject GameOverPanel;
    public Button settingButton;
    public GameObject pausePanel;
    [Tooltip("拖 Canvas/Pause Panel/Slider。不要用 pausePanel.GetComponent<Slider>() —— pausePanel 是面板, 本身没有 Slider 组件")]
    public Slider volumeSlider;

    [Header("事件监听")]
    public CharacterEventSO healthEvent;
    public SceneLoadEventSO sceneLoadEvent;
    public VoidEventSO newGameEvent;
    public VoidEventSO loadGameEvent;
    public VoidEventSO backToMenuEvent;
    public FloatEventSO syncVolumeEvent;


    private void OnEnable()
    {
        healthEvent.OnEventRaised += OnHealthEvent;
        sceneLoadEvent.LoadRequestEvent += OnSceneLoadRequest;
        newGameEvent.OnEventRaised += OnHidePanelEvent;
        loadGameEvent.OnEventRaised += OnHidePanelEvent;
        backToMenuEvent.OnEventRaised += OnHidePanelEvent;
        syncVolumeEvent.OnEventRaised += OnSyncVolumeEvent;
    }

    private void OnDisable()
    {
        healthEvent.OnEventRaised -= OnHealthEvent;
        sceneLoadEvent.LoadRequestEvent -= OnSceneLoadRequest;
        newGameEvent.OnEventRaised -= OnHidePanelEvent;
        loadGameEvent.OnEventRaised -= OnHidePanelEvent;
        backToMenuEvent.OnEventRaised -= OnHidePanelEvent;
        syncVolumeEvent.OnEventRaised -= OnSyncVolumeEvent;

    }

    private void OnSyncVolumeEvent(float sliderValue)
    {
        // 原来的 pausePanel.GetComponent<Slider>() 是 null —— pausePanel 是那个面板,
        // 它自己只有 RectTransform / CanvasRenderer / Image, Slider 是它的子物体
        // (Canvas/Pause Panel/Slider), 所以那行一执行就 NullReferenceException。
        if (volumeSlider == null)
        {
            Debug.LogWarning("UIManager.volumeSlider 没有连, 音量滑块不会同步");
            return;
        }

        // 事件通道传的就是滑块位置 0..1(分贝换算全部在 AudioManager 里),
        // 所以这里一个公式都不用写 —— 也就不会再出现"两个文件各写一份、改一边漏一边"。
        //
        // 用 SetValueWithoutNotify 而不是 .value = :
        // 直接赋 .value 会触发 slider 的 onValueChanged, 而它连的是 VolumeChangeEvent
        // -> 会反向再写一次 mixer。
        volumeSlider.SetValueWithoutNotify(sliderValue);
    }

    private void OnHidePanelEvent()
    {
        GameOverPanel.SetActive(false);
    }

    private void OnSceneLoadRequest(GameSceneSO sceneToLoad, Vector3 arg1, bool arg2)
    {
        if(sceneToLoad.sceneType == SceneType.Menu)
        {
            PlayerStateBar.gameObject.SetActive(false);
        }
        else
        {
            PlayerStateBar.gameObject.SetActive(true);
        }
    }

    private void OnHealthEvent(Character character)
    {
        float percentage = character.curHealth / character.maxHealth;
        PlayerStateBar.onHealthChange(percentage);
    }

    public void TogglePausePanel()
    {
        if (pausePanel.activeInHierarchy)
        {
            pausePanel.SetActive(false);
            Time.timeScale = 1;
        }
        else
        {
            pausePanel.SetActive(true);
            Time.timeScale = 0;
        }
    }
}
