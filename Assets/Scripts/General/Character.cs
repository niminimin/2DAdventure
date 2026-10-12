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

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.isDead = false;
            pc.inputControl.GamePlay.Enable();
        }
    }
}
