using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System;

public class CameraControll : MonoBehaviour
{
    [Header("事件监听")]
    public VoidEventSO afterSceneLoadEvent;
    public VoidEventSO cameraShakeEvent;

    public CinemachineConfiner2D confiner2D;
    public CinemachineImpulseSource impulseSource;
    private void Awake()
    {
        confiner2D = GetComponent<CinemachineConfiner2D>();
        //Debug.Log("getconfiner");
    }

    //private void Start()
    //{
    //    GetNewCameraBounds();
    //}

    private void OnEnable()
    {
        cameraShakeEvent.OnEventRaised += OnCameraShaked;
        afterSceneLoadEvent.OnEventRaised += OnAfterSceneLoadEvent;
    }

    private void OnDisable()
    {
        cameraShakeEvent.OnEventRaised -= OnCameraShaked;
        afterSceneLoadEvent.OnEventRaised -= OnAfterSceneLoadEvent;
    }
    private void GetNewCameraBounds()
    {
        var obj = GameObject.FindGameObjectWithTag("Bounds");
        if (obj == null)
        {
            //Debug.LogError("Cant find obj");
            return;
        }
            
        //Debug.Log(obj.name);
        confiner2D.m_BoundingShape2D = obj.GetComponent<Collider2D>();
        confiner2D.InvalidateCache();
    }
    #region Event
    private void OnAfterSceneLoadEvent()
    {
        GetNewCameraBounds();
    }
  
    private void OnCameraShaked()
    {
        impulseSource.GenerateImpulse();
    }
    #endregion
}
