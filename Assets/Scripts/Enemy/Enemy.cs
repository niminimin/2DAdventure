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
    public void onTakeDamage(Attack attacker)
    {
        // 用攻击者碰撞体的中心算退向，不能用 attacker.transform.position
        // （Tilemap 的 transform 在网格原点(0,0)，和格子上画在哪无关，
        //   例如荆棘画在 x=49，transform 却在 0，会让差值恒为正）
        float delta = transform.position.x - attacker.KnockbackOrigin.x;

        if(delta < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        if(delta >= 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        isWounded = true;
        animator.SetTrigger("Wounded");

        // delta 恰好为 0 时左右无意义：normalized 会给出零向量（击退消失），
        // Mathf.Sign(0) 又返回 1（固定朝右），所以退化成朝背后推
        Vector2 dir = Mathf.Abs(delta) < 0.01f
            ? new Vector2(-faceDir.x, 0)
            : new Vector2(Mathf.Sign(delta), 0);

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
