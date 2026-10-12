using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Data 
{
    public string sceneToSave;
    public Dictionary<string, Vector3> characterPositionDictionary = new Dictionary<string, Vector3>();
    public Dictionary<string, float> characterHealthDictionary = new Dictionary<string, float>();
    public Dictionary<string, bool> interactHasTriggered = new Dictionary<string, bool>();

    public void SaveGameScene(GameSceneSO saveScene)
    {
        sceneToSave = JsonUtility.ToJson(saveScene);
    }

    public GameSceneSO GetSavedScene()
    {
        var scene = ScriptableObject.CreateInstance<GameSceneSO>();
        JsonUtility.FromJsonOverwrite(sceneToSave, scene);
        return scene;
    }
}
