using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>
    /// 存档文件路径。Application.persistentDataPath 是各平台统一的可写且持久目录
    /// (Windows 下是 %USERPROFILE%\AppData\LocalLow\&lt;公司名&gt;\&lt;产品名&gt;)。
    /// 注意: 改 Player Settings 里的 Company Name / Product Name 会改变这个路径,
    /// 旧存档会因此"找不到"。
    /// </summary>
    public string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, "save.json"); }
    }

    public bool HasSaveFile
    {
        get { return File.Exists(SavePath); }
    }

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
        //if (Keyboard.current.lKey.wasPressedThisFrame)
        //{
        //    Load();
        //}
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
        // 1) 让所有 saveable 把当前状态写进 saveData。
        //    各 GetSaveData 内部都是"key 存在就赋值、不存在才 Add", 所以同一对象
        //    每次都是覆盖自己那条记录, 不会重复追加。
        foreach(var saveable in  saveableList)
        {
            saveable.GetSaveData(saveData);
        }

        // 2) 整体序列化落盘。
        //    File.WriteAllText 会【覆盖】同名文件 —— 每次存档都是完整重写, 不是追加。
        //    Data 里的字典由 Data.OnBeforeSerialize 摊平成影子列表后才进 JSON。
        try
        {
            string dir = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SavePath, JsonUtility.ToJson(saveData));
            Debug.Log("存档已写入: " + SavePath
                + "  (位置 " + saveData.characterPositionDictionary.Count
                + " 条, 血量 " + saveData.characterHealthDictionary.Count
                + " 条, 交互 " + saveData.interactHasTriggered.Count + " 条)");
        }
        catch (Exception e)
        {
            Debug.LogError("存档写入失败: " + e.Message);
        }
    }

    public void Load()
    {
        // 没有存档文件 —— "返回上一个存档点" / "继续" 退化成"从游戏开头重来"。
        if (!HasSaveFile)
        {
            FallbackToNewGame();
            return;
        }

        try
        {
            // FromJsonOverwrite 填进【已存在的】saveData 实例, 内部会自动调用
            // Data.OnAfterDeserialize 把影子列表装回字典。
            JsonUtility.FromJsonOverwrite(File.ReadAllText(SavePath), saveData);
        }
        catch (Exception e)
        {
            // 存档损坏 / 被手改坏 —— 当作没有存档, 而不是让整个游戏崩在这一步
            Debug.LogError("存档读取失败, 按没有存档处理: " + e.Message);
            saveData = new Data();
            FallbackToNewGame();
            return;
        }

        foreach(var saveable in saveableList)
        {
            saveable.LoadData(saveData);
        }
    }

    private void FallbackToNewGame()
    {
        if (newGameEventSO != null)
            newGameEventSO.RaiseEvent();
    }

    /// <summary>
    /// 登记一个已死亡的敌人（把野猪当 Boss：死了就不再重建）。
    ///
    /// 只写进【内存里的 saveData】，落盘要等下一次 Save()（碰存档点）。
    /// 这是刻意与存档系统保持一致的语义：
    ///   打死野猪 → 换场景回来 → 它不会复活（内存里记着）
    ///   打死野猪后【没存档】就死了 → 读档 → 它还在（存档里没这条记录）
    /// </summary>
    public void MarkEnemyDead(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        saveData.deadEnemies[id] = true;
    }

    public bool IsEnemyDead(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        bool dead;
        return saveData.deadEnemies.TryGetValue(id, out dead) && dead;
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
