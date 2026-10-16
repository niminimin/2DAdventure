using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 死亡状态 —— 这个状态的"行为"就是【什么都不做】。
///
/// 【为什么死亡要是一个状态，而不是一个 isDead bool】
/// 用 bool 的话，"死后不该再跑 AI"这条约束必须写在【每一个调用点】：
///     Update()      -> if (isDead) return;
///     FixedUpdate() -> if (!isWait && !isWounded && !isDead) move();
/// 漏掉任何一处，死亡后就会继续执行那一处的逻辑 ——
/// 而且这类遗漏不会报错，只会表现为"偶尔行为诡异"。
/// （野猪之前就是这样：Update() 漏了守卫，死后仍会跑状态机与转向逻辑。）
///
/// 收成状态之后，"谁是当前状态"成为唯一的真相来源：
/// 死亡时 curState 就是本状态，而它的 LogicUpdate / PhysicsUpdate 都是空的，
/// 于是"死后还在跑 AI"【在结构上无法发生】，不再依赖谁记得补守卫。
///
/// 无出边：死亡不可逆。重进场景 / 读档会重新实例化，
/// 由 Enemy.Awake() 按存档决定是否直接销毁。
/// </summary>
public class BoarDeadState : BaseState
{
    public override void OnEnter(Enemy enemy)
    {
        curEnemy = enemy;
    }

    public override void LogicUpdate()
    {
        // 死后不跑任何 AI：不做视野检测、不转向、不累计计时器。
        //
        // 这里【故意留空】而不是加一句 if (isDead) return; ——
        // 空实现本身就是"什么都不做"的表达，不需要再做一次判断。
    }

    public override void PhysicsUpdate()
    {
        // 死后不接受任何移动输入。
        // 尸体仍然受重力影响自然下落（由物理引擎接管，不需要这里插手）。
    }

    public override void OnExit()
    {
    }
}
