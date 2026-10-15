using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存档数据。业务代码用下面的字典按 ID 索引；落盘交给 JsonUtility。
///
/// 为什么要写这些影子列表：
///   JsonUtility 【完全不支持 Dictionary】—— 遇到字典字段会静默跳过，
///   不报错、也不写进 JSON。直接 JsonUtility.ToJson(data) 得到的文件里
///   只有 sceneToSave，位置/血量/交互状态全丢，而且读回来也不报错。
///   所以序列化前把字典摊平成 List，反序列化后再装回字典。
///
/// OnBeforeSerialize / OnAfterDeserialize 由 JsonUtility.ToJson 和
/// JsonUtility.FromJsonOverwrite 自动调用（Unity 的 ISerializationCallbackReceiver），
/// 不需要手动同步。
/// </summary>
[Serializable]
public class Data : ISerializationCallbackReceiver
{
    public string sceneToSave;

    // ---------------- 业务代码用的形态 ----------------
    public Dictionary<string, Vector3> characterPositionDictionary = new Dictionary<string, Vector3>();
    public Dictionary<string, float> characterHealthDictionary = new Dictionary<string, float>();
    public Dictionary<string, bool> interactHasTriggered = new Dictionary<string, bool>();
    /// <summary>
    /// 已死亡的敌人。把野猪当 Boss 处理 —— 它死了就不该再被重建。
    /// 注意：Character 那边照常保存敌人的位置和血量，这里【只额外记死亡】。
    /// </summary>
    public Dictionary<string, bool> deadEnemies = new Dictionary<string, bool>();

    // ---------------- 给 JsonUtility 看的影子列表 ----------------
    [SerializeField] private List<string> positionKeys = new List<string>();
    [SerializeField] private List<Vector3> positionValues = new List<Vector3>();
    [SerializeField] private List<string> healthKeys = new List<string>();
    [SerializeField] private List<float> healthValues = new List<float>();
    [SerializeField] private List<string> interactKeys = new List<string>();
    [SerializeField] private List<bool> interactValues = new List<bool>();
    [SerializeField] private List<string> deadKeys = new List<string>();
    [SerializeField] private List<bool> deadValues = new List<bool>();

    public void OnBeforeSerialize()
    {
        // 每次都先 Clear 再重建。不清的话上一次的旧条目会一直累积在列表里，
        // 越存越大，而且删掉的字典项会"复活"。
        positionKeys.Clear();
        positionValues.Clear();
        foreach (KeyValuePair<string, Vector3> kv in characterPositionDictionary)
        {
            positionKeys.Add(kv.Key);
            positionValues.Add(kv.Value);
        }

        healthKeys.Clear();
        healthValues.Clear();
        foreach (KeyValuePair<string, float> kv in characterHealthDictionary)
        {
            healthKeys.Add(kv.Key);
            healthValues.Add(kv.Value);
        }

        interactKeys.Clear();
        interactValues.Clear();
        foreach (KeyValuePair<string, bool> kv in interactHasTriggered)
        {
            interactKeys.Add(kv.Key);
            interactValues.Add(kv.Value);
        }

        deadKeys.Clear();
        deadValues.Clear();
        foreach (KeyValuePair<string, bool> kv in deadEnemies)
        {
            deadKeys.Add(kv.Key);
            deadValues.Add(kv.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        // 同样先清空，否则会读档时把字典和已有内容合并，而不是覆盖
        characterPositionDictionary.Clear();
        int posCount = Mathf.Min(positionKeys.Count, positionValues.Count);
        for (int i = 0; i < posCount; i++)
            characterPositionDictionary[positionKeys[i]] = positionValues[i];

        characterHealthDictionary.Clear();
        int hpCount = Mathf.Min(healthKeys.Count, healthValues.Count);
        for (int i = 0; i < hpCount; i++)
            characterHealthDictionary[healthKeys[i]] = healthValues[i];

        interactHasTriggered.Clear();
        int flagCount = Mathf.Min(interactKeys.Count, interactValues.Count);
        for (int i = 0; i < flagCount; i++)
            interactHasTriggered[interactKeys[i]] = interactValues[i];

        deadEnemies.Clear();
        int deadCount = Mathf.Min(deadKeys.Count, deadValues.Count);
        for (int i = 0; i < deadCount; i++)
            deadEnemies[deadKeys[i]] = deadValues[i];
    }

    public void SaveGameScene(GameSceneSO saveScene)
    {
        // JsonUtility 能把 AssetReference 里的 m_AssetGUID([SerializeField]) 一起存下来,
        // 读回来时 RuntimeKeyIsValid() 为 true, Addressables 仍然能定位到场景。
        sceneToSave = JsonUtility.ToJson(saveScene);
    }

    public GameSceneSO GetSavedScene()
    {
        var scene = ScriptableObject.CreateInstance<GameSceneSO>();
        JsonUtility.FromJsonOverwrite(sceneToSave, scene);
        return scene;
    }
}
