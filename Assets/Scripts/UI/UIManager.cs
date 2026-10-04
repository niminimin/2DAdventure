using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public PlayerStateBar PlayerStateBar;
    [Header("事件监听")]
    public CharacterEventSO healthEvent;


    private void OnEnable()
    {
        healthEvent.OnEventRaised += OnHealthEvent;
    }

    private void OnDisable()
    {
        healthEvent.OnEventRaised -= OnHealthEvent;
    }

    private void OnHealthEvent(Character character)
    {
        float percentage = character.curHealth / character.maxHealth;
        PlayerStateBar.onHealthChange(percentage);
    }
}
