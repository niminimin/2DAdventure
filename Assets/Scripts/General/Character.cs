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

        onHealthChanged?.Invoke(this);
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
        if (data.characterPositionDictionary.ContainsKey(GetDataID().ID))
        {
            data.characterPositionDictionary[GetDataID().ID] = transform.position;
            data.characterHealthDictionary[GetDataID().ID] = curHealth;
        }
        else
        {
            data.characterPositionDictionary.Add(GetDataID().ID, transform.position);
            data.characterHealthDictionary.Add(GetDataID().ID, curHealth);
        }
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
