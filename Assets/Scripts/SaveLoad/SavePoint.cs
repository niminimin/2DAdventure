using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SavePoint : MonoBehaviour, IInteractable, ISaveable
{
    [Header("广播")]
    public VoidEventSO saveGameEventSO;

    [Header("基本参数")]
    public SpriteRenderer spriteRenderer;
    public Sprite darkSprite;
    public Sprite lightSprite;
    public GameObject lightObj;

    public bool isDone;

    public bool CanInteract { get { return !isDone; } }


    private void RefreshVisual()
    {
        spriteRenderer.sprite = isDone ? lightSprite : darkSprite;
        lightObj.SetActive(isDone);
    }

    private void OnEnable()
    {
        DataManager.instance.RegisterSaveData(this);

        // 场景卸载重载后这里是一个全新实例，isDone 只有场景文件里序列化的默认值(false)。
        // 必须主动把存档里属于自己的状态拉回来 —— 否则切场景回来永远显示"未激活"。
        // LoadData 内部会改 isDone 并调 RefreshVisual，所以这一句就把画面也刷新了。
        DataManager.instance.LoadFor(this);
    }

    private void OnDisable()
    {
        DataManager.instance.UnRegisterSaveData(this);
    }

    public void TriggerAction()
    {
        if(!isDone)
        {
            Restore();
        }
    }

    private void Restore()
    {
        isDone = true;
        RefreshVisual();
        saveGameEventSO.RaiseEvent();
    }

    public DataDifination GetDataID()
    {
        return GetComponent<DataDifination>();
    }

    public void GetSaveData(Data data)
    {
        if(data.interactHasTriggered.ContainsKey(GetDataID().ID))
        {
            data.interactHasTriggered[GetDataID().ID] = isDone;
            Debug.Log("savepoint" + isDone);
        }
        else
        {
            data.interactHasTriggered.Add(GetDataID().ID, isDone);
        }
    }

    public void LoadData(Data data)
    {
        // 用 TryGetValue 而不是字典索引器: 没有这条记录时(新游戏 / 旧存档 / ID 变过)
        // 索引器会抛 KeyNotFoundException, 整个 Load 循环会被异常打断, 后面的对象全部读不到。
        // 找不到就保留场景里的默认值, 这才是"没开过"的正确语义。
        if (data.interactHasTriggered.TryGetValue(GetDataID().ID, out bool saved))
            isDone = saved;

        RefreshVisual();
    }
}
