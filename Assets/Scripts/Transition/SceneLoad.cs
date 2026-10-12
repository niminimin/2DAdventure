using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour, ISaveable
{
    public Transform playerTrans;
    public Vector3 initPosition;
    public Vector3 menuPosition;
    [Header("事件监听")]
    public SceneLoadEventSO sceneLoadEvent;
    public VoidEventSO newGameEvent;
    public VoidEventSO loadGameDataEvent;
    public VoidEventSO backToMenuEvent;

    [Header("场景")]
    public GameSceneSO menuScene;
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
        //NewGame();
        LoadMenu();
    }

    private void OnEnable()
    {
        sceneLoadEvent.LoadRequestEvent += OnSceneLoadRequest;
        newGameEvent.OnEventRaised += NewGame;
        backToMenuEvent.OnEventRaised += LoadMenu;
        DataManager.instance.RegisterSaveData(this);
    }

    private void OnDisable()
    {
        sceneLoadEvent.LoadRequestEvent -= OnSceneLoadRequest;
        newGameEvent.OnEventRaised -= NewGame;
        backToMenuEvent.OnEventRaised -= LoadMenu;
        DataManager.instance.UnRegisterSaveData(this);
    }

    private void NewGame()
    {
        sceneToLoad = firstLoadScene;
        //OnSceneLoadRequest(sceneToLoad, initPosition, true);
        sceneLoadEvent.RaiseLoadRequest(sceneToLoad, initPosition, true);
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

        if(curLoadScene.sceneType == SceneType.Location)
            afterSceneLoadEvent.RaiseEvent();
    }

    private void LoadMenu()
    {
        sceneLoadEvent.RaiseLoadRequest(menuScene, menuPosition, true);
    }

    public DataDifination GetDataID()
    {
        return GetComponent<DataDifination>();
    }

    public void GetSaveData(Data data)
    {
        data.SaveGameScene(curLoadScene);
    }

    public void LoadData(Data data)
    {
        var playerID = playerTrans.gameObject.GetComponent<DataDifination>().ID;
        if (data.characterPositionDictionary.ContainsKey(playerID))
        {
            positionToGo = data.characterPositionDictionary[playerID];
            sceneToLoad = data.GetSavedScene();
        }
        OnSceneLoadRequest(sceneToLoad, positionToGo, true);
    }
}
