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
        Debug.Log("teleport");
        // 这里接换图逻辑
        loadEventSO.RaiseLoadRequest(sceneToGo, positionToGo, true);
    }
}
