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
        rb.velocity = new Vector2(inputDirection.x * speed * Time.deltaTime , rb.velocity.y);
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
    public void GetWounded(Transform attackerTrans)
    {
        isWounded = true;
        rb.velocity = Vector2.zero;
        Vector2 dir = new Vector2((transform.position.x - attackerTrans.position.x), 0).normalized;
        rb.AddForce(dir * woundForce, ForceMode2D.Impulse);
    }

    public void GetDied()
    {
        isDead = true;
        inputControl.GamePlay.Disable();
    }
    #endregion


}
