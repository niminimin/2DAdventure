using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour, IInteractable, ISaveable
{
    private SpriteRenderer spriteRenderer;
    public Sprite openSprite;
    public Sprite closeSprite;
    public bool isDone;

    // 「已打开」这个状态用 CanInteract 表达，不要去改 tag。
    // tag 是 Sign 用来"发现"物体的依据，改掉它会让 Sign.OnTriggerExit2D 里的
    // CompareTag 一起失效 —— 症状就是图标在离开宝箱后收不起来。
    public bool CanInteract { get { return !isDone; } }

    public void TriggerAction()
    {
        //Debug.Log("open chest");
        if (!isDone)
        {
            OpenChest();
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (GetDataID() == null)
        {
            // 缺 DataDifination 就没法存档。这里【不能静默】——
            // 否则症状是"开箱后切场景回来又关上了"，而没有任何线索指向配置问题。
            Debug.LogWarning($"[{name}] 宝箱缺少 DataDifination 组件，开启状态不会被保存", this);
            RefreshVisual();
            return;
        }

        DataManager.instance.RegisterSaveData(this);

        // 场景卸载重载后这里是一个全新实例，isDone 只有场景文件里序列化的默认值(false)。
        // 必须主动把存档里属于自己的状态拉回来 —— 否则切场景回来永远显示"未开启"。
        // LoadData 内部会改 isDone 并调 RefreshVisual，所以这一句也把画面刷新了。
        DataManager.instance.LoadFor(this);
    }

    private void OnDisable()
    {
        if (GetDataID() != null)
            DataManager.instance.UnRegisterSaveData(this);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = isDone ? openSprite : closeSprite;
    }

    private void OpenChest()
    {
        isDone = true;
        RefreshVisual();
        // 交互音效属于"开箱"这件事本身，所以由宝箱自己播（组件在 Inspector 里配）。
        GetComponent<AudioDefination>()?.PlayAudioClip();
    }

    #region ISaveable
    public DataDifination GetDataID()
    {
        return GetComponent<DataDifination>();
    }

    public void GetSaveData(Data data)
    {
        string id = GetDataID().ID;
        if (data.interactHasTriggered.ContainsKey(id))
        {
            data.interactHasTriggered[id] = isDone;
        }
        else
        {
            data.interactHasTriggered.Add(id, isDone);
        }
    }

    public void LoadData(Data data)
    {
        // 用 TryGetValue 而不是字典索引器：没有这条记录时(新游戏 / 旧存档 / ID 变过)
        // 索引器会抛 KeyNotFoundException，整个 Load 循环会被异常打断，
        // 后面的对象全部读不到。找不到就保留场景默认值(false)，这才是"没开过"的正确语义。
        bool saved;
        if (data.interactHasTriggered.TryGetValue(GetDataID().ID, out saved))
            isDone = saved;

        RefreshVisual();
    }
    #endregion
}
