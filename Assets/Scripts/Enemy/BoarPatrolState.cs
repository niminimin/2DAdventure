using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoarPatrolState : BaseState
{
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
        curEnemy.curSpeed = curEnemy.normalSpeed;
        curEnemy.animator.SetBool("isWalk", true);

        // 进入巡逻 = 从"非等待"开始。
        // isWait / waitTimeCount 的整个生命周期现在都在本状态内，
        // 这里把入口条件写死，避免上一次残留的值让野猪一进来就卡在等待里。
        curEnemy.isWait = false;
        curEnemy.waitTimeCount = 0f;
    }

    public override void LogicUpdate()
    {
        if (curEnemy.FoundPlayer())
        {
            curEnemy.SwitchState(EnemyState.Chase);

            // 【切换后必须返回】
            // 原来的代码缺这一句：切到追击之后，下面那段"巡逻中"的逻辑
            // 还会以旧状态的身份继续执行，可能把 isWalk / isWait / velocity 又改一遍，
            // 把刚进入的追击状态搅乱。
            return;
        }

        // ——— 等待中：自己负责倒计时与转身 ———
        // 这段原来在 Enemy.TimeCounter() 里。它本来就属于巡逻状态
        //（"走到边缘 → 停一下 → 转身"是巡逻的行为），放在基类里有两个坏处：
        //   1. 它是脱离状态机的【第二条执行路径】—— 死后照样会跑
        //   2. 同一个状态的逻辑被拆到两个类，改一边容易漏另一边
        if (curEnemy.isWait)
        {
            curEnemy.waitTimeCount += Time.deltaTime;
            if (curEnemy.waitTimeCount >= curEnemy.waitTime)
            {
                curEnemy.waitTimeCount = 0f;
                curEnemy.isWait = false;
                curEnemy.transform.localScale = new Vector3(curEnemy.faceDir.x, 1, 1);   // 转身
            }

            // 等待期间不重新判断条件，避免每帧反复置位导致 isWait / 速度 / 朝向震荡
            return;
        }

        // ——— 前方是边缘(touchLedge)、或贴着朝向的那面墙 → 进入等待并转身 ———
        if (curEnemy.physicsCheck.touchLedge ||
            (curEnemy.physicsCheck.touchLeftWall  && curEnemy.faceDir.x < 0) ||
            (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
        {
            curEnemy.isWait = true;
            curEnemy.animator.SetBool("isWalk", false);
            curEnemy.waitTimeCount = 0f;
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
        // "等待中不移动"是巡逻状态自己的事，所以守卫写在这里，
        // 而不是像原来那样写在 Enemy.FixedUpdate() 里（那里要同时管三个 bool）。
        if (!curEnemy.isWait && !curEnemy.isWounded)
            curEnemy.move();
    }

    public override void OnExit()
    {
        curEnemy.animator.SetBool("isWalk", false);

        // 【必须清掉 isWait】离开巡逻状态 = 不再处于"等待"中。
        // 倒计时逻辑已经搬进本状态，离开之后没有任何东西会再让它归位 ——
        // 不清的话 isWait 会一直停在 true，接下来无论切到哪个状态都动不了。
        curEnemy.isWait = false;
        curEnemy.waitTimeCount = 0f;
    }
}
