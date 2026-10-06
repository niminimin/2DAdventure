using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerInputCtrl inputControl;
    private Rigidbody2D rb;
    private PhysicsCheck phyCheck;
    public Vector2 inputDirection;
    [Header("基本参数")]
    public float speed;
    public float jumpForce;
    public float woundForce;
    [Tooltip("垂直弹起力度。要能爬出你设计的坑深: v = sqrt(2 * g * 坑深), g = 9.81 * gravityScale")]
    public float bounceUpForce = 20;
    [Header("State")]
    public bool isWounded;
    public bool isDead;
    public bool isAttack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputControl = new PlayerInputCtrl();
        phyCheck = GetComponent<PhysicsCheck>();
        inputControl.GamePlay.Jump.started += Jump;
        inputControl.GamePlay.Attack.started += PlayerAttack;
    }



    private void OnEnable()
    {
        inputControl.Enable();
    }

    private void OnDisable()
    {
        inputControl.Disable();
    }

    private void Update()
    {
        inputDirection = inputControl.GamePlay.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if(!isWounded)
            move();
    }

    private void move()
    {
        rb.velocity = new Vector2(inputDirection.x * speed, rb.velocity.y);
        int curDir = (int)transform.localScale.x;
        if(inputDirection.x > 0)
        {
            curDir = 1;
        }
        if(inputDirection.x < 0)
        {
            curDir = -1;
        }
        transform.localScale = new Vector3(curDir, 1, 1);
    }
    private void Jump(InputAction.CallbackContext context)
    {
        //Debug.Log("jump");
        if(phyCheck.isGround)
            rb.AddForce(transform.up * jumpForce, ForceMode2D.Impulse);
    }
    private void PlayerAttack(InputAction.CallbackContext context)
    {
        GetComponent<PlayerAnimation>().PlayerAttack();
        isAttack = true;
    }

    #region UnityEvent
    public void GetWounded(Attack attacker)
    {
        isWounded = true;

        Vector2 v = rb.velocity;             // 必须在清零之前读

        // 水平：远离攻击者身上离你最近的那个点
        Vector2 cp = attacker.ClosestPointTo(transform.position);
        float delta = transform.position.x - cp.x;

        // delta == 0 表示你正站在它上面/里面（ClosestPoint 在内部会原样返回你的位置）
        bool standingOnIt = Mathf.Abs(delta) < 0.01f;

        float hx = standingOnIt
            ? -Mathf.Sign(transform.localScale.x)   // 正上方 → 朝背后推
            : Mathf.Sign(delta);

        // 垂直：站在地刺上、或正在下落时，向上弹
        // 这是爬出地刺坑的唯一手段（Jump() 要求 isGround，空中不能二段跳）
        // 在地面上跑着撞竖刺柱时 v.y ≈ 0 且 delta != 0 → vy = 0，柱子不会被弹飞
        float vy = (standingOnIt || v.y < -0.5f) ? 1f : 0f;

        // 只有真要把你往上弹时才清掉 y：否则下落速度会抵消向上的冲量
        // （下落 -15.3 再加 +20 只剩 +4.7，弹起高度从 5.1 格缩到 0.28 格）
        // 其余情况保留 y，避免「跳起来蹭到刺就把跳跃也取消了」
        rb.velocity = new Vector2(0, vy > 0f ? 0f : v.y);

        rb.AddForce(new Vector2(hx * woundForce, vy * bounceUpForce), ForceMode2D.Impulse);
    }

    public void GetDied()
    {
        isDead = true;
        inputControl.GamePlay.Disable();
    }
    #endregion


}
