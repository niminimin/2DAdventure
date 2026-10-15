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
        // 扒墙：状态判定在 PlayerController.UpdateWallSlide() 里，这里只负责喂给 Animator
        animator.SetBool("isWallSlide", playerController.isWallSliding);
        // 滑铲：状态判定在 PlayerController.Slide() / UpdateSlide() 里，同理
        animator.SetBool("isSliding", playerController.isSliding);
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
