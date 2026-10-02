using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Rigidbody2D rb;
    [HideInInspector] public Animator animator;
    [HideInInspector] public PhysicsCheck physicsCheck;

    [Header("基本参数")]
    public float normalSpeed;
    public float chaseSpeed;
    public float curSpeed;
    public Vector3 faceDir;
    public float woundForce;

    [Header("计时器")]
    public float waitTime;
    public float waitTimeCount;
    public float lostTime;
    public float lostTimeCount;

    [Header("检测玩家")]
    public Vector2 centerOffset;
    public Vector2 checkSize;
    public float checkDistance;
    public LayerMask attackLayer;

    [Header("状态")]
    public bool isWounded;
    public bool isWait;
    public bool isDead;
    private BaseState curState;
    protected BaseState patrolState;
    protected BaseState chaseState;
    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        physicsCheck = GetComponent<PhysicsCheck>();
    }

    private void Update()
    {
        faceDir = new Vector3(-transform.localScale.x, 0, 0);

        curState.LogicUpdate();

        TimeCounter();
    }

    private void OnEnable()
    {
        curState = patrolState;
        curState.OnEnter(this);
    }

    private void FixedUpdate()
    {
        if (!isWait && !isWounded && !isDead)
            move();

        curState.PhysicsUpdate();
    }

    private void OnDisable()
    {
        curState.OnExit();
    }

    public virtual void move()
    {
        rb.velocity = new Vector2(curSpeed * faceDir.x, rb.velocity.y);
    }

    private void TimeCounter()
    {
        if (isWait)
        {
            
            waitTimeCount += Time.deltaTime;
            if(waitTimeCount >= waitTime)
            {
                waitTimeCount = 0;
                isWait = false;
                transform.localScale = new Vector3(faceDir.x, 1, 1);
                
            }
        }

        if (curState == chaseState && !FoundPlayer())
        {
            lostTimeCount += Time.deltaTime;
        }
        else
        {
            lostTimeCount = 0;
        }
    }

    public bool FoundPlayer()
    {
        return Physics2D.BoxCast(transform.position + (Vector3)centerOffset, checkSize, 0, faceDir, checkDistance, attackLayer);
    }

    public void SwitchState(EnemyState state)
    {
        BaseState newState = curState;
        switch (state)
        {
            case EnemyState.Patrol:
                newState = patrolState;
                break;
            case EnemyState.Chase:
                newState = chaseState;
                break;
            default:
                break;
        }
        if(newState == curState) return;
        if(newState == null)
        {
            Debug.LogError($"[{name}] 状态 {state} 未初始化，已忽略切换");
            return;
        }
        curState.OnExit();
        curState = newState;
        curState.OnEnter(this);
    }

    #region Event
    //Event
    public void onTakeDamage(Transform attackerTrans)
    {
        if(transform.position.x - attackerTrans.position.x < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        if(transform.position.x - attackerTrans.position.x >= 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        isWounded = true;
        animator.SetTrigger("Wounded");
        Vector2 dir = new Vector2(transform.position.x - attackerTrans.position.x, 0).normalized;
        rb.velocity = Vector2.zero;
        StartCoroutine(onWounded(dir));
    }

    private IEnumerator onWounded(Vector2 dir)
    {
        rb.AddForce(dir * woundForce, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.45f);
        isWounded = false;
    }

    public void onDied()
    {
        gameObject.layer = 2;
        isDead = true;
        animator.SetBool("isDead", isDead);
    }

    public void DestroyAfterAnimation()
    {
        Destroy(this.gameObject);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position + (Vector3)centerOffset + new Vector3(checkDistance * -transform.localScale.x, 0), 0.2f);
    }
    public enum EnemyState
    {
        Patrol,
        Chase,
        Skill
    }
}
