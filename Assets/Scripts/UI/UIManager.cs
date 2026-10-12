using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public PlayerStateBar PlayerStateBar;
    public GameObject GameOverPanel;
    [Header("事件监听")]
    public CharacterEventSO healthEvent;
    public SceneLoadEventSO sceneLoadEvent;
    public VoidEventSO newGameEvent;
    public VoidEventSO loadGameEvent;
    public VoidEventSO backToMenuEvent;


    private void OnEnable()
    {
        healthEvent.OnEventRaised += OnHealthEvent;
        sceneLoadEvent.LoadRequestEvent += OnSceneLoadRequest;
        newGameEvent.OnEventRaised += OnHidePanelEvent;
        loadGameEvent.OnEventRaised += OnHidePanelEvent;
        backToMenuEvent.OnEventRaised += OnHidePanelEvent;
    }

    private void OnDisable()
    {
        healthEvent.OnEventRaised -= OnHealthEvent;
        sceneLoadEvent.LoadRequestEvent -= OnSceneLoadRequest;
        newGameEvent.OnEventRaised -= OnHidePanelEvent;
        loadGameEvent.OnEventRaised -= OnHidePanelEvent;
        backToMenuEvent.OnEventRaised -= OnHidePanelEvent;
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
}
