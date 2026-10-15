using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Character : MonoBehaviour, ISaveable
{
    [Header("事件监听")]
    public VoidEventSO newGameEventSO;

    [Header("基本属性")]
    public float maxHealth;
    public float curHealth;

    [Header("受伤无敌")]
    public float invulnerableDuration;
    private float invulnerableCount;
    private bool isInvulnerable;

    [Header("广播")]
    public UnityEvent<Character> onHealthChanged;
    public UnityEvent<Attack> onTakeDamage;
    public UnityEvent onDead;

    /// <summary>回满血并广播给 UI。新游戏 / 复活都走这里。</summary>
    public void NewGame()
    {
        curHealth = maxHealth;
        isInvulnerable = false;
        onHealthChanged?.Invoke(this);
    }

    private void Start()
    {
        NewGame();

        // 场景加载出来时, 对象身上带的只是编辑器里的默认值 —— 敌人的血量永远是满的。
        // 必须主动把存档里属于自己的记录(位置 + 血量)拉回来。
        // SavePoint 也是这个套路, 见 SavePoint.OnEnable。
        //
        // 【为什么是 Start 而不是 OnEnable】
        // Start 每个实例只跑一次; 而 Player 每次切场景都会被
        // SceneLoad 的 SetActive(false)/(true) 再触发一次 OnEnable
        // (SceneLoad.cs:111 和 :136)。放在 OnEnable 的话, 你走进新场景后
        // 会被传送回上一个存档点, 并且血量退回存档值(中途受的伤白受)。
        DataManager.instance.LoadFor(this);
    }

    private void OnEnable()
    {
        newGameEventSO.OnEventRaised += NewGame;
        DataManager.instance.RegisterSaveData(this);
    }

    private void OnDisable()
    {
        newGameEventSO.OnEventRaised -= NewGame;
        DataManager.instance.UnRegisterSaveData(this);
    }

    private void Update()
    {
        if (isInvulnerable)
        {
            invulnerableCount -= Time.deltaTime;
            if (invulnerableCount <= 0)
            {
                isInvulnerable = false;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Water"))
        {
            //死亡逻辑，血量清空，音效等
            curHealth = 0;
            RecordHealth();                 // 立刻记进内存存档（同 TakeDamage 的理由）
            onHealthChanged?.Invoke(this);
            onDead?.Invoke();
        }
    }

    public void TakeDamage(Attack attacker)
    {
        if (isInvulnerable)
            return;
        curHealth -= attacker.damage;
        if(curHealth > 0)
        {
            //执行受伤
            onTakeDamage?.Invoke(attacker);
            TrigInvulnerable();
        }
        else
        {
            onDead?.Invoke();
            TrigInvulnerable();
        }

        RecordHealth();                     // 立刻记进内存存档
        onHealthChanged?.Invoke(this);
    }

    /// <summary>
    /// 立即把当前血量写进【内存】存档。
    ///
    /// 【为什么不能只靠 GetSaveData】
    /// GetSaveData 只在 DataManager.Save() 里被调用，而 Save() 只遍历 saveableList ——
    /// 也就是【当时已加载】的对象，而且【前提是玩家真的去碰了存档点】。
    /// 没碰过存档点就换场景，伤害就丢了 ——
    /// 表现为「把敌人打伤后换场景，它回满血了」。
    ///
    /// 而死亡是立即记录的（Enemy.onDied -> MarkEnemyDead），
    /// 所以会出现「伤丢、死不丢」的不一致。这里对齐。
    ///
    /// 只写内存，落盘仍由存档点触发 —— 未存档就死亡/读档时，伤害照样会被回滚，这是正确的。
    /// </summary>
    private void RecordHealth()
    {
        if (DataManager.instance == null) return;

        var dif = GetDataID();
        if (dif == null) return;            // 没挂 DataDifination 就不参与存档

        DataManager.instance.MarkCharacterHealth(dif.ID, curHealth);
    }

    public void TrigInvulnerable()
    {
        if (!isInvulnerable)
        {
            isInvulnerable = true;
            invulnerableCount = invulnerableDuration;
        }
    }

    public DataDifination GetDataID()
    {
        return GetComponent<DataDifination>();
    }

    public void GetSaveData(Data data)
    {
        string id = GetDataID().ID;

        // 用【索引器】(存在则覆盖、不存在则新增)，不要用 ContainsKey + Add。
        //
        // 原来的写法只检查 characterPositionDictionary，却在 else 分支里【同时 Add 两个字典】——
        // 只要这两个字典不同步就会撞键，而它们确实会不同步：
        // MarkCharacterHealth() 只写血量、不写位置（打伤敌人时）。
        //
        // 症状：打伤野猪后去存档 → 抛 "An item with the same key has already been added"。
        // 而且因为 Save() 的 foreach 在 try 之外，这个异常会让【整个存档都没写盘】。
        data.characterPositionDictionary[id] = transform.position;
        data.characterHealthDictionary[id] = curHealth;
    }

    public void LoadData(Data data)
    {
        // 必须 TryGetValue: 字典索引器在 key 不存在时抛 KeyNotFoundException。
        // 而 Load() 是在一次事件广播里被调用的 —— 这个异常会一路穿出
        // VoidEventSO.RaiseEvent() 的委托链, 让排在后面的订阅者(UI 收面板、
        // 玩家复活)全部收不到通知。一次"没找到记录"的失手不该有这么大的杀伤面。
        if (data.characterPositionDictionary.TryGetValue(GetDataID().ID, out Vector3 savedPos))
            transform.position = savedPos;

        if (data.characterHealthDictionary.TryGetValue(GetDataID().ID, out float savedHealth))
            curHealth = savedHealth;

        onHealthChanged?.Invoke(this);

        // 这里【不碰 PlayerController】。
        // 复活(清 isDead / 开 GamePlay 输入)是 PlayerController 自己的职责,
        // 由 PlayerRevive() 挂在 loadGameEvent 上完成。
        // 原来这里会 GamePlay.Enable(), 而现在 LoadData 也会被 Start() 调用
        // (出生时拉存档) —— 那一句会在菜单场景里把移动输入打开,
        // 就是之前修过的"菜单里能走动"。
    }
}
