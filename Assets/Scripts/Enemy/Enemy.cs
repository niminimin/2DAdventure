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
    /// <summary>
    /// 仅作对外查询 / 动画参数用 —— 【不再充当逻辑守卫】。
    ///
    /// "死后不跑 AI、不移动"现在由 BoarDeadState 保证：死亡时 curState 就是它，
    /// 于是 Update / FixedUpdate 里一个 if (isDead) 都不需要。
    /// 这正是本次重构的目的：约束从"每个调用点"收敛到"状态本身"。
    /// </summary>
    public bool isDead;
    private BaseState curState;
    protected BaseState patrolState;
    protected BaseState chaseState;
    protected BaseState deadState;
    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        physicsCheck = GetComponent<PhysicsCheck>();

        // 把野猪当 Boss：存档里记着它已经死了 → 直接销毁，不让它出现在场景里。
        //
        // 放在 Awake 而不是 Start，是为了避免"闪一帧" —— 对象一被实例化就销毁，
        // 不会先渲染出来再消失。
        //
        // 两条路径下 saveData 在场景实例化时都已就绪：
        //   普通切场景 —— saveData 一直在内存里
        //   读档       —— DataManager 标了 [DefaultExecutionOrder(-100)]，
        //                 它的 Load() 会在 SceneLoad.LoadData() 触发场景加载之前跑完
        var dif = GetComponent<DataDifination>();
        if (DataManager.instance != null && dif != null
            && DataManager.instance.IsEnemyDead(dif.ID))
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        faceDir = new Vector3(-transform.localScale.x, 0, 0);

        // 状态机现在覆盖【全部】行为，所以这里一个 bool 守卫都不需要：
        // 死后 curState 就是 BoarDeadState，它的 LogicUpdate 是空实现。
        //
        // 注意原来这里还挂着 TimeCounter() —— 那是脱离状态机的【第二条执行路径】，
        // 死亡后照样会跑（而且它内部会写 transform.localScale，即"转向"）。
        // 它的两份职责已经分别下沉到 BoarPatrolState / BoarChaseState。
        curState.LogicUpdate();
    }

    private void OnEnable()
    {
        curState = patrolState;
        curState.OnEnter(this);
    }

    private void FixedUpdate()
    {
        // move() 的守卫下沉到了各状态的 PhysicsUpdate：
        //   Patrol -> 等待中 / 受伤硬直 不移动
        //   Chase  -> 受伤硬直 不移动
        //   Dead   -> 什么都不做
        // 所以这里不再需要 isWait / isWounded / isDead 三个 bool 判断。
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
            case EnemyState.Dead:
                newState = deadState;
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
        // 用攻击者身上离自己最近的点算退向，不能用 attacker.transform.position
        // 也不能用包围盒中心（多段 Tilemap 的中心可能落在空点上）
        float delta = transform.position.x - attacker.ClosestPointTo(transform.position).x;

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

        // 切进死亡状态 —— 这是"死后不再跑 AI"的【唯一约束点】。
        // 切进去之后 curState 就是 BoarDeadState，它的 LogicUpdate / PhysicsUpdate
        // 都是空实现，所以 Update / FixedUpdate 里一个 isDead 守卫都不需要。
        //
        // 原来的写法是把 isDead 当成状态机之外的 bool，于是守卫必须写在每个调用点：
        // 结果 Update() 漏了一处，死后仍在跑状态机与转向逻辑。
        SwitchState(EnemyState.Dead);

        // 登记死亡。
        // 不记的话，重新加载场景时这只野猪会作为"活的"被重建出来 ——
        // 因为 Character 那边只保存了它的位置和血量，血量 > 0 就是"活着"。
        var dif = GetComponent<DataDifination>();
        if (DataManager.instance != null && dif != null)
            DataManager.instance.MarkEnemyDead(dif.ID);
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

}
