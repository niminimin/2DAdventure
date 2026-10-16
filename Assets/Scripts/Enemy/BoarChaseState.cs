using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoarChaseState : BaseState
{
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
        curEnemy.curSpeed = curEnemy.chaseSpeed;
        curEnemy.animator.SetBool("isRun", true);

        // 【必须重置丢失计时】
        // 原来它是在 Enemy.TimeCounter() 里靠 `curState != chaseState` 被动归零的，
        // 现在那段逻辑搬进了本状态 —— 不再有人替我们归零。
        // 不重置的话：上一次追击攒满的 lostTimeCount 会原样带入，
        // 于是刚进入追击就立刻判定"丢失目标"，马上又切回巡逻。
        curEnemy.lostTimeCount = 0f;
    }

    public override void LogicUpdate()
    {
        // ——— 丢失目标计时 ———
        // 原来在 Enemy.TimeCounter() 里，靠 `curState == chaseState` 判断；
        // 搬进来之后这个判断天然成立（本状态就是追击状态，不需要再问一遍）。
        if (!curEnemy.FoundPlayer())
            curEnemy.lostTimeCount += Time.deltaTime;
        else
            curEnemy.lostTimeCount = 0f;

        if (curEnemy.lostTimeCount >= curEnemy.lostTime)
        {
            curEnemy.SwitchState(EnemyState.Patrol);

            // 切换后返回，理由同 BoarPatrolState.LogicUpdate：
            // 不能用旧状态的身份继续改新状态的东西。
            return;
        }

        // ——— 前方悬崖 / 朝向那侧贴墙 → 转向 ———
        if (curEnemy.physicsCheck.touchLedge ||
            (curEnemy.physicsCheck.touchLeftWall  && curEnemy.faceDir.x < 0) ||
            (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
        {
            curEnemy.transform.localScale = new Vector3(curEnemy.faceDir.x, 1, 1);
        }
    }

    public override void PhysicsUpdate()
    {
        // 追击只需要看"是否受伤硬直"：
        // isWait 是巡逻状态内部的概念，离开巡逻时已由 BoarPatrolState.OnExit 重置。
        if (!curEnemy.isWounded)
            curEnemy.move();
    }

    public override void OnExit()
    {
        curEnemy.animator.SetBool("isRun", false);
    }
}
