using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoarPatrolState : BaseState
{    
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
        curEnemy.curSpeed = curEnemy.normalSpeed;
    }
    public override void LogicUpdate()
    {
        if (curEnemy.FoundPlayer())
        {
            curEnemy.SwitchState(Enemy.EnemyState.Chase);
        }
        // 等待中：不重新判断条件，避免每帧反复置位导致 isWait / 速度 / 朝向震荡。
        // 解除等待由 Enemy.TimeCounter() 负责。
        if (curEnemy.isWait)
            return;

        // 前方是边缘(touchLedge)、或贴着朝向的那面墙 → 进入等待并转身
        if (curEnemy.physicsCheck.touchLedge ||
            (curEnemy.physicsCheck.touchLeftWall  && curEnemy.faceDir.x < 0) ||
            (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
        {
            curEnemy.isWait = true;
            curEnemy.animator.SetBool("isWalk", false);
            curEnemy.waitTimeCount = 0;
            // 顶掉朝前的动量，避免等待期间继续越过边缘
            curEnemy.rb.velocity = Vector2.zero;
        }
        else
        {
            curEnemy.animator.SetBool("isWalk", true);
        }
    }
    public override void PhysicsUpdate()
    {
        
    }


    public override void OnExit()
    {
        curEnemy.animator.SetBool("isWalk", false);
        Debug.Log("exit");
    }


}
