using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Event/SceneLoadEventSO")]
public class SceneLoadEventSO : ScriptableObject
{
    public UnityAction<GameSceneSO, Vector3, bool> LoadRequestEvent;

    /// <summary>
    /// 场景加载请求
    /// </summary>
    /// <param name="locationToLoad">需要加载的场景</param>
    /// <param name="posToGo">Player在场景初始位置</param>
    /// <param name="fadeScreen">场景渐变效果</param>
    public void RaiseLoadRequest(GameSceneSO locationToLoad, Vector3 posToGo, bool fadeScreen)
    {
        LoadRequestEvent?.Invoke(locationToLoad, posToGo, fadeScreen);
    }
}
