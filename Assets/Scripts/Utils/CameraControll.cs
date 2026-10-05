using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System;

public class CameraControll : MonoBehaviour
{
    public CinemachineConfiner2D confiner2D;
    public CinemachineImpulseSource impulseSource;
    public VoidEventSO cameraShakeEvent;
    private void Awake()
    {
        confiner2D = GetComponent<CinemachineConfiner2D>();
        //Debug.Log("getconfiner");
    }

    private void Start()
    {
        GetNewCameraBounds();
    }

    private void OnEnable()
    {
        cameraShakeEvent.OnEventRaised += OnCameraShaked;
    }

    private void OnDisable()
    {
        cameraShakeEvent.OnEventRaised -= OnCameraShaked;
    }

    private void GetNewCameraBounds()
    {
        var obj = GameObject.FindGameObjectWithTag("Bounds");
        if (obj == null)
        {
            //Debug.LogError("Cant find obj");
            return;
        }
            
        Debug.Log(obj.name);
        confiner2D.m_BoundingShape2D = obj.GetComponent<Collider2D>();
        confiner2D.InvalidateCache();
    }    
    private void OnCameraShaked()
    {
        impulseSource.GenerateImpulse();
    }
}
