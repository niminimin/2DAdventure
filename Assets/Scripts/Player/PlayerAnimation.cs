using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rd;
    private PhysicsCheck phyCheck;
    private PlayerController playerController;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rd = GetComponent<Rigidbody2D>();
        phyCheck = GetComponent<PhysicsCheck>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        SetAnimation();   
    }

    public void SetAnimation()
    {
        animator.SetFloat("velocityX", Mathf.Abs(rd.velocity.x));
        animator.SetFloat("velocityY", rd.velocity.y);
        animator.SetBool("isGround", phyCheck.isGround);
        animator.SetBool("isDead", playerController.isDead);
        animator.SetBool("isAttack", playerController.isAttack);
    }

    public void PlayerWounded()
    {
        animator.SetTrigger("wounded");
    }

    public void PlayerAttack()
    {
        animator.SetTrigger("attack");
    }
}
