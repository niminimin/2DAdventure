using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportPoint : MonoBehaviour, IInteractable
{
    public SceneLoadEventSO loadEventSO;
    public GameSceneSO sceneToGo;
    public Vector3 positionToGo;
    // 传送点永远可以交互。
    public bool CanInteract { get { return true; } }

    public void TriggerAction()
    {
        // 换图故意不播音效：交互音属于"开箱"这类本地事件，切换场景时响起来很突兀。
        // 以后想给某个传送门配音，给它挂 AudioDefination 再在这里调 PlayAudioClip() 即可。
        Debug.Log("teleport");
        // 这里接换图逻辑
        loadEventSO.RaiseLoadRequest(sceneToGo, positionToGo, true);
    }
}
