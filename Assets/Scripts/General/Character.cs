using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Character : MonoBehaviour
{
    [Header("基本属性")]
    public float maxHealth;
    public float curHealth;

    [Header("受伤无敌")]
    public float invulnerableDuration;
    private float invulnerableCount;
    private bool isInvulnerable;

    public UnityEvent<Character> onHealthChanged;
    public UnityEvent<Transform> onTakeDamage;
    public UnityEvent onDead;

    private void Start()
    {
        curHealth = maxHealth;
        isInvulnerable = false;
        onHealthChanged?.Invoke(this);
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

    public void TakeDamage(Attack attacker)
    {
        if (isInvulnerable)
            return;
        curHealth -= attacker.damage;
        if(curHealth > 0)
        {
            //执行受伤
            onTakeDamage?.Invoke(attacker.transform);
            TrigInvulnerable();
        }
        else
        {
            onDead?.Invoke();
        }

        onHealthChanged?.Invoke(this);
    }

    private void TrigInvulnerable()
    {
        if (!isInvulnerable)
        {
            isInvulnerable = true;
            invulnerableCount = invulnerableDuration;
        }
    }


}
