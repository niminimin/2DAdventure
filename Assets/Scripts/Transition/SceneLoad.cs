using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{
    public Transform playerTrans;
    public Vector3 initPosition;
    [Header("事件监听")]
    public SceneLoadEventSO sceneLoadEvent;
    public GameSceneSO firstLoadScene;
    private GameSceneSO curLoadScene;
    private GameSceneSO sceneToLoad;

    [Header("广播")]
    public VoidEventSO afterSceneLoadEvent;
    public FadeEventSO fadeEvent;


    private Vector3 positionToGo;
    private bool fadeScreen;
    private bool isLoading;

    public float fadeDuration;

    private void Awake()
    {
        //curLoadScene = firstLoadScene;
        //curLoadScene.sceneReference.LoadSceneAsync(LoadSceneMode.Additive);
    }

    //TODO 应该改成游戏主界面ui
    private void Start()
    {
        NewGame();
    }

    private void OnEnable()
    {
        sceneLoadEvent.LoadRequestEvent += OnSceneLoadRequest;
    }

    private void OnDisable()
    {
        sceneLoadEvent.LoadRequestEvent -= OnSceneLoadRequest;
    }

    private void NewGame()
    {
        sceneToLoad = firstLoadScene;
        OnSceneLoadRequest(sceneToLoad, initPosition, true);
    }

    /// <summary>
    /// 场景加载事件请求
    /// </summary>
    /// <param name="locationToLoad"></param>
    /// <param name="posToGo"></param>
    /// <param name="fadeScreen"></param>
    private void OnSceneLoadRequest(GameSceneSO locationToLoad, Vector3 posToGo, bool fadeScreen)
    {
        if (isLoading)
            return;
        isLoading = true;
        sceneToLoad = locationToLoad;
        positionToGo = posToGo;
        this.fadeScreen = fadeScreen;

        if(curLoadScene != null) 
        {
            StartCoroutine(UnloadPreviousScene());
        }
        else
        {
            LoadNewScene();
        }
        
    }

    private IEnumerator UnloadPreviousScene()
    {
        if (fadeScreen)
        {
            //变黑
            fadeEvent.FadeIn(fadeDuration);
        }
        yield return new WaitForSeconds(fadeDuration);

        yield return curLoadScene.sceneReference.UnLoadScene();

        playerTrans.gameObject.SetActive(false);
        
        LoadNewScene();
    }

    private void LoadNewScene()
    {
        var loadingOption =  sceneToLoad.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);
        loadingOption.Completed += OnLoadCompelted;
    }
    /// <summary>
    /// 场景加载结束后触发
    /// </summary>
    /// <param name="handle"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnLoadCompelted(AsyncOperationHandle<SceneInstance> handle)
    {
        curLoadScene = sceneToLoad;

        playerTrans.position = positionToGo;
        if (fadeScreen)
        {
            //变透明
            fadeEvent.FadeOut(fadeDuration);
        }
        playerTrans.gameObject.SetActive(true);
        isLoading = false;

        afterSceneLoadEvent.RaiseEvent();
    }
}
