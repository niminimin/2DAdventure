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
        rb.velocity = new Vector2(0, rb.velocity.y);

        // 用攻击者碰撞体的中心算退向，不能用 attacker.transform.position
        // （荆棘是 Tilemap，transform 在网格原点，实际画在 x=49）
        float delta = transform.position.x - attacker.KnockbackOrigin.x;

        // 正好站在攻击者中心线上时 delta 为 0，normalized 会得到零向量 → 退化成朝背后推
        Vector2 dir = Mathf.Abs(delta) < 0.01f
            ? new Vector2(-Mathf.Sign(transform.localScale.x), 0)
            : new Vector2(Mathf.Sign(delta), 0);

        rb.AddForce(dir * woundForce, ForceMode2D.Impulse);
    }

    public void GetDied()
    {
        isDead = true;
        inputControl.GamePlay.Disable();
    }
    #endregion


}
