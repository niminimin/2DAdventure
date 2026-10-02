using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoarPatrolState : BaseState
{    
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
    }
    public override void LogicUpdate()
    {
        //TODO
        if(!curEnemy.physicsCheck.isGround || (curEnemy.physicsCheck.touchLeftWall && curEnemy.faceDir.x < 0) || (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
        {
            curEnemy.isWait = true;
            curEnemy.rb.velocity = new Vector2(-curEnemy.faceDir.x * 0.2f, curEnemy.rb.velocity.y);
        }
    }
    public override void PhysicsUpdate()
    {
        
    }


    public override void OnExit()
    {
        curEnemy.animator.SetBool("isWalk", false);
    }


}
