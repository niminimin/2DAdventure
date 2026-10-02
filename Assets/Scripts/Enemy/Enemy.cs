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
    [HideInInspector] public float curSpeed;
    public Vector3 faceDir;
    public float woundForce;

    [Header("计时器")]
    public float waitTime;
    public float waitTimeCount;

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
        curSpeed = normalSpeed;
        rb.velocity = new Vector2(curSpeed * faceDir.x * Time.deltaTime, rb.velocity.y);
    }

    private void TimeCounter()
    {
        if (isWait)
        {
            animator.SetBool("isWalk", false);
            waitTimeCount += Time.deltaTime;
            if(waitTimeCount >= waitTime)
            {
                waitTimeCount = 0;
                isWait = false;
                transform.localScale = new Vector3(faceDir.x, 1, 1);
                animator.SetBool("isWalk", true);
            }
        }
    }

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
}
