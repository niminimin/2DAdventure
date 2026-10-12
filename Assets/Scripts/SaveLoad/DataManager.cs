using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class DataManager : MonoBehaviour
{
    public static DataManager instance;
    [Header("事件监听")]
    public VoidEventSO saveGameEventSO;
    public VoidEventSO newGameEventSO;
    public VoidEventSO LoadGameEventSO;

    private List<ISaveable> saveableList = new List<ISaveable>();
    private Data saveData;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this.gameObject);

        saveData = new Data();
    }

    private void OnEnable()
    {
        saveGameEventSO.OnEventRaised += Save;
        LoadGameEventSO.OnEventRaised += Load;
        // 开新游戏必须丢掉上一次运行留在内存里的存档，
        // 否则新场景里的对象在 OnEnable 拉取时会把自己直接恢复成"已激活"。
        if (newGameEventSO != null)
            newGameEventSO.OnEventRaised += ResetData;
    }

    private void OnDisable()
    {
        saveGameEventSO.OnEventRaised -= Save;
        LoadGameEventSO.OnEventRaised -= Load;
        if (newGameEventSO != null)
            newGameEventSO.OnEventRaised -= ResetData;
    }

    private void ResetData()
    {
        saveData = new Data();
    }

    private void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            Load();
        }
    }

    public void RegisterSaveData(ISaveable saveable)
    {
        if (!saveableList.Contains(saveable))
        {
            saveableList.Add(saveable);
        }
    }

    public void UnRegisterSaveData(ISaveable saveable)
    {
        saveableList.Remove(saveable);
    }
    public void Save()
    {
        foreach(var saveable in  saveableList)
        {
            saveable.GetSaveData(saveData);
        }

        foreach (var item in saveData.characterPositionDictionary)
        {
            Debug.Log(item.Key + " " + item.Value);
        }
    }

    public void Load()
    {
        // 还没存过档(没碰过存档点) —— "返回上一个存档点" 退化成"从游戏开头重来"。
        // sceneToSave 由 SceneLoad.GetSaveData() 在保存那一刻写入, 是"有没有存档"的判据。
        if (saveData == null || string.IsNullOrEmpty(saveData.sceneToSave))
        {
            if (newGameEventSO != null)
                newGameEventSO.RaiseEvent();
            return;
        }

        foreach(var saveable in saveableList)
        {
            saveable.LoadData(saveData);
        }
    }

    /// <summary>
    /// 只把存档数据套用给一个对象（不是全部）。
    /// 场景卸载重载后，新出来的对象靠这个把自己恢复成存档里的状态 ——
    /// 否则它只能拿到场景文件里序列化的默认值。
    /// 没有对应记录时 LoadData 内部会保留默认值，所以新游戏里调用它是安全的。
    /// </summary>
    public void LoadFor(ISaveable saveable)
    {
        saveable.LoadData(saveData);
    }
}
