using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BoarChaseState : BaseState
{
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
        //Debug.Log("chase");
        curEnemy.curSpeed = curEnemy.chaseSpeed;
        curEnemy.animator.SetBool("isRun", true);
    }

    public override void LogicUpdate()
    {
        if(curEnemy.lostTimeCount >= curEnemy.lostTime)
        {
            curEnemy.SwitchState(EnemyState.Patrol);
        }
        if (curEnemy.physicsCheck.touchLedge ||
        (curEnemy.physicsCheck.touchLeftWall && curEnemy.faceDir.x < 0) ||
        (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
        {
            curEnemy.transform.localScale = new Vector3(curEnemy.faceDir.x, 1, 1);
        }
    }


    public override void PhysicsUpdate()
    {
        
    }    

    public override void OnExit()
    {
        curEnemy.animator.SetBool("isRun", false);
    }
}
