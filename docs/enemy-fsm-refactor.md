# 敌人 FSM 重构方案（待执行）

> **状态：方案阶段 —— 本文只记录设计，尚未改动任何代码。**
>
> 目标：让「死后还在跑 AI」这一类问题**在结构上无法发生**，
> 而不是靠在调用点补 `if (isDead) return;`。
>
> **对照阅读**：[玩家状态机重构 · 评估稿](player-state-refactor.md)
> —— 那一份是**评估**，玩家是否要重构**尚未决定**，两者的判断依据可以互相对照。

---

## 一、动机

### 1.1 直接症状

野猪死亡后的 **0.4 秒窗口**（`BoarDied.anim` 末尾的 `DestroyAfterAnimation` 动画事件之前），
尸体仍然可能翻转朝向。

现象很难复现，因为：

- 死亡动画在这 0.4 秒里把 `m_Color.a` 从 **1 淡出到 0**，翻转时尸体已在变透明
- 触发条件是「恰好死在悬崖边 / 贴墙 / 等待转身期间」，窗口很窄

**但代码路径确实存在**（见 2.2），条件凑齐时仍会发生。

### 1.2 更根本的问题：三处逻辑游离在状态机之外

这个 bug 表面上是「漏了一个守卫」，但根因是**状态机没有覆盖全部行为**：

```csharp
private void Update()
{
    faceDir = new Vector3(-transform.localScale.x, 0, 0);
    curState.LogicUpdate();     // ① 状态机
    TimeCounter();              // ② 游离 —— 而且内部会写 transform.localScale
}

private void FixedUpdate()
{
    if (!isWait && !isWounded && !isDead)   // ③ 守卫写在【调用点】
        move();
    curState.PhysicsUpdate();
}
```

**① 和 ② 是两个平行的执行路径，状态机只掌管其中一个。**

### 1.3 `TimeCounter()` 存在职责错位

它内部塞了**两件分属不同状态的事**：

| `TimeCounter()` 里的逻辑 | 语义上属于 |
|---|---|
| `isWait` 倒计时 + `transform.localScale = ...`（等待转身） | **`BoarPatrolState`** |
| `lostTimeCount` 累计（丢失目标计时，`curState == chaseState` 时才跑） | **`BoarChaseState`** |

作者其实已经意识到了 —— `BoarPatrolState.cs` 里留着这句注释：

```csharp
// 等待中：不重新判断条件，避免每帧反复置位导致 isWait / 速度 / 朝向震荡。
// 解除等待由 Enemy.TimeCounter() 负责。      ← 承认逻辑被拆到了两个类
```

---

## 二、现状

### 2.1 状态图（当前）

```
        FoundPlayer() 为真
   ┌──────────────────────────┐
   │                          ▼
┌──────────┐            ┌──────────┐
│  Patrol  │◀───────────│  Chase   │
│  巡逻    │  丢失目标   │  追击    │
└──────────┘            └──────────┘

死亡：不是状态，只是一个 bool `isDead`
```

### 2.2 三处游离点

| # | 位置 | 问题 |
|---|---|---|
| 1 | `Enemy.Update()` | **没有 `isDead` 守卫** → 死后仍在跑 `curState.LogicUpdate()` |
| 2 | `Enemy.TimeCounter()` | 独立于状态机被调用，且**内部会写 `transform.localScale`** → 死后仍可能翻转 |
| 3 | `Enemy.FixedUpdate()` | `move()` 靠 `!isWait && !isWounded && !isDead` 三个 bool 守卫，**而不是由状态决定** |

### 2.3 一个隐蔽的坑：`SwitchState` 对未处理的状态**静默返回**

```csharp
public void SwitchState(EnemyState state)
{
    BaseState newState = curState;          // ← 默认保持原状态
    switch (state)
    {
        case EnemyState.Patrol: newState = patrolState; break;
        case EnemyState.Chase:  newState = chaseState;  break;
        default: break;                     // ← 没处理的状态 = 静默无操作
    }
    if (newState == curState) return;       // ← 在这里被吞掉
    ...
}
```

> ⚠️ **只往 `EnemyState` 枚举里加 `Dead` 而忘了加 `case`，`SwitchState(EnemyState.Dead)` 会什么都不做，
> 而且不报错。** 实施时必须同时改枚举和 `switch`。

---

## 三、目标设计

### 3.1 状态图（重构后）

```
        FoundPlayer() 为真
   ┌──────────────────────────┐
   │                          ▼
┌──────────┐            ┌──────────┐
│  Patrol  │◀───────────│  Chase   │
│  巡逻    │  丢失目标   │  追击    │
└──────────┘            └──────────┘
     │                        │
     │   onDied()（从任意状态） │
     ▼                        ▼
   ┌────────────────────────────────┐
   │            Dead                │  ← 新增，无出边
   │  LogicUpdate()   { }           │
   │  PhysicsUpdate() { }           │
   └────────────────────────────────┘
```

### 3.2 职责归属（重构后）

| 逻辑 | 归属 |
|---|---|
| 视野检测 → 转追击 | `BoarPatrolState.LogicUpdate()` |
| 悬崖 / 撞墙 → 进入等待 | `BoarPatrolState.LogicUpdate()` |
| **等待倒计时 + 转身** | **`BoarPatrolState.LogicUpdate()`**（从 `TimeCounter` 搬入） |
| **丢失目标计时 → 转巡逻** | **`BoarChaseState.LogicUpdate()`**（从 `TimeCounter` 搬入） |
| 悬崖 / 撞墙 → 转向 | `BoarChaseState.LogicUpdate()` |
| **移动** | **各状态的 `PhysicsUpdate()`**（从 `FixedUpdate` 搬入） |
| 死亡（什么也不做） | `BoarDeadState` |

### 3.3 重构后要成立的不变量

1. **`Enemy.Update()` 里不再有任何 bool 守卫** —— 因为「死后不该跑 AI」由 `Dead` 状态保证
2. **`Enemy.FixedUpdate()` 里不再有任何 bool 守卫** —— 因为「死后/等待中不该移动」由状态的 `PhysicsUpdate` 保证
3. **所有会写 `transform.localScale`（转向）的代码，都在某个状态类里**
4. **`TimeCounter()` 被删除** —— 它的两份职责各自回到所属状态

> 第 3 条是这个重构的**核心价值**：「谁是当前状态」成为**唯一的真相来源**，
> 不再有第二个地方能改变敌人行为。

---

## 四、实施步骤

拆成 **4 个可独立提交、可独立回滚**的步骤。

### 步骤 0 · 止血（1 行）

```csharp
private void Update()
{
    // 【临时守卫】重构完成后（步骤 2）会被删除。
    if (isDead) return;

    faceDir = new Vector3(-transform.localScale.x, 0, 0);
    curState.LogicUpdate();
    TimeCounter();
}
```

- **目的**：立刻消除现象，让代码与文档口径一致
- **风险**：无
- **注意**：这是**过渡措施**，步骤 2 完成后必须删掉

### 步骤 1 · 引入 `Dead` 状态

**① 枚举加成员**

```csharp
public enum EnemyState { Patrol, Chase, Skill, Dead }
```

**② 新增 `BoarDeadState.cs`**

```csharp
public class BoarDeadState : BaseState
{
    public override void OnEnter(Enemy enemy) { curEnemy = enemy; }
    public override void LogicUpdate()   { }   // 死后不跑任何 AI
    public override void PhysicsUpdate() { }   // 死后不移动
    public override void OnExit()        { }
}
```

**③ `Boar.Awake()` 里创建**（与 `patrolState` / `chaseState` 并列）

```csharp
protected override void Awake()
{
    base.Awake();
    patrolState = new BoarPatrolState();
    chaseState  = new BoarChaseState();
    deadState   = new BoarDeadState();
}
```

`Enemy` 需新增 `protected BaseState deadState;`

**④ `SwitchState` 补 `case`**（⚠️ 别忘了，否则静默无操作）

```csharp
case EnemyState.Dead: newState = deadState; break;
```

**⑤ `onDied()` 切状态**

```csharp
public void onDied()
{
    gameObject.layer = 2;
    isDead = true;
    animator.SetBool("isDead", isDead);

    SwitchState(EnemyState.Dead);          // ★ 关键

    var dif = GetComponent<DataDifination>();
    if (DataManager.instance != null && dif != null)
        DataManager.instance.MarkEnemyDead(dif.ID);
}
```

- **风险**：低（新增为主）
- **此时还删不掉步骤 0 的守卫** —— 因为 `TimeCounter()` 还在跑

### 步骤 2 · `TimeCounter()` 逻辑下沉，删除 `TimeCounter()`

**① `BoarPatrolState.LogicUpdate()` 接收"等待转身"逻辑**

```csharp
public override void LogicUpdate()
{
    if (curEnemy.FoundPlayer())
    {
        curEnemy.SwitchState(EnemyState.Chase);
        return;                                    // ← 注意：切换后应直接返回
    }

    // —— 等待中：自己负责倒计时与转身（原本在 Enemy.TimeCounter 里）——
    if (curEnemy.isWait)
    {
        curEnemy.waitTimeCount += Time.deltaTime;
        if (curEnemy.waitTimeCount >= curEnemy.waitTime)
        {
            curEnemy.waitTimeCount = 0;
            curEnemy.isWait = false;
            curEnemy.transform.localScale = new Vector3(curEnemy.faceDir.x, 1, 1);   // 转身
        }
        return;
    }

    // —— 前方悬崖 / 贴墙 → 进入等待 ——（维持原逻辑）
    if (curEnemy.physicsCheck.touchLedge ||
        (curEnemy.physicsCheck.touchLeftWall  && curEnemy.faceDir.x < 0) ||
        (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
    {
        curEnemy.isWait = true;
        curEnemy.animator.SetBool("isWalk", false);
        curEnemy.waitTimeCount = 0;
        curEnemy.rb.velocity = Vector2.zero;
    }
    else
    {
        curEnemy.animator.SetBool("isWalk", true);
    }
}
```

> ⚠️ **原代码在 `FoundPlayer()` 之后没有 `return`** —— 切换状态后仍会继续往下执行。
> 搬到新结构时**加上 `return`**，否则会在已经切到 Chase 之后又去改 `isWalk`。

**② `BoarChaseState.LogicUpdate()` 接收"丢失目标计时"**

```csharp
public override void LogicUpdate()
{
    // —— 丢失目标计时（原本在 Enemy.TimeCounter 里）——
    if (!curEnemy.FoundPlayer())
        curEnemy.lostTimeCount += Time.deltaTime;
    else
        curEnemy.lostTimeCount = 0;

    if (curEnemy.lostTimeCount >= curEnemy.lostTime)
    {
        curEnemy.SwitchState(EnemyState.Patrol);
        return;                                    // ← 切换后直接返回
    }

    // —— 悬崖 / 贴墙 → 转向 ——（维持原逻辑）
    if (curEnemy.physicsCheck.touchLedge ||
        (curEnemy.physicsCheck.touchLeftWall  && curEnemy.faceDir.x < 0) ||
        (curEnemy.physicsCheck.touchRightWall && curEnemy.faceDir.x > 0))
    {
        curEnemy.transform.localScale = new Vector3(curEnemy.faceDir.x, 1, 1);
    }
}
```

**③ 删除 `Enemy.TimeCounter()` 及其在 `Update()` 里的调用**

```csharp
private void Update()
{
    faceDir = new Vector3(-transform.localScale.x, 0, 0);
    curState.LogicUpdate();       // Dead 状态下天然空转，不需要守卫
}
```

**④ 删除步骤 0 的临时守卫**

- **风险**：中 —— 这是**行为逻辑搬家**，巡逻/追击/丢失目标全都要重测
- **关键**：这是**等价搬运**，不是重写。`lostTimeCount` 原来只在 `curState == chaseState` 时累计 —— 搬进 `BoarChaseState` 后语义完全一致

### 步骤 3 · `move()` 下沉到 `PhysicsUpdate`

```csharp
// Enemy.FixedUpdate()
private void FixedUpdate()
{
    curState.PhysicsUpdate();      // 守卫消失
}
```

```csharp
// BoarPatrolState
public override void PhysicsUpdate()
{
    if (!curEnemy.isWait && !curEnemy.isWounded) curEnemy.move();
}

// BoarChaseState
public override void PhysicsUpdate()
{
    if (!curEnemy.isWounded) curEnemy.move();
}

// BoarDeadState
public override void PhysicsUpdate() { }
```

- **风险**：低（纯搬迁）
- **收益**：`isWait` / `isDead` 的移动守卫也随之内聚到状态里；
  只剩 `isWounded` 还在基类（见第七节）

---

## 五、风险清单

| 风险 | 说明 | 缓解 |
|---|---|---|
| ⚠️ **`SwitchState` 静默无操作** | 加了枚举忘了加 `case`，`Dead` 切换会静默失败 | 步骤 1 必须同时改「枚举 + `switch` case + `deadState` 字段 + `Boar.Awake` 创建」，四处缺一不可 |
| ⚠️ **忘了加 `return`** | 原代码 `FoundPlayer()` 后无 `return`，搬家后若照抄会在切换状态后继续执行 | 步骤 2 的两个 `LogicUpdate` 都已在状态切换后加 `return` |
| **行为回归** | 巡逻 / 追击 / 等待转身 / 丢失目标计时全部受影响 | 按第六节清单逐项手测；`.meta` 与 GUID 不变，场景无需改 |
| **`OnExit` 副作用** | `SwitchState` 会调 `curState.OnExit()`，`BoarPatrolState.OnExit` 会 `SetBool("isWalk", false)` | 切到 `Dead` 时会执行一次 Patrol 的 `OnExit`，与现状一致（现状也会走 `OnExit`），无新增风险 |
| **`Dead` 状态无出边** | 死亡不可逆，符合设计 | 读档/重进场景会重新实例化，`Enemy.Awake` 按存档决定是否销毁 |

---

## 六、验收标准

### 6.1 结构验收（读代码即可验证）

- [ ] `Enemy.Update()` 里**没有任何 bool 守卫**
- [ ] `Enemy.FixedUpdate()` 里**没有任何 bool 守卫**
- [ ] `Enemy.TimeCounter()` **已被删除**
- [ ] 全项目搜索 `transform.localScale`，**所有写给敌人的地方都在状态类里**
- [ ] `SwitchState` 的 `switch` 里有 `case EnemyState.Dead`

### 6.2 行为验收（必须手测）

- [ ] 野猪正常巡逻，到悬崖边**停住、等待 1 秒、转身**
- [ ] 野猪撞墙后**等待、转身**
- [ ] 玩家进入视野 → **转追击**；跑出视野 **5 秒** → **回巡逻**
- [ ] **打死野猪 → 观察死亡动画全程，尸体朝向不翻转**
- [ ] 在悬崖边打死野猪 → 观察（这是原 bug 最容易复现的场景）
- [ ] 在野猪"等待转身"的**后半段**打死它 → 观察
- [ ] 死亡后不再有 `isWalk` / `isRun` 的动画参数被改写（可用 Animator 窗口观察）

### 6.3 回归范围

- [ ] 敌人死亡持久化仍生效（换场景/读档后不复活）
- [ ] 扒墙 / 蹬墙跳 / 滑铲不受影响
- [ ] BGM、宝箱、存档行为不受影响

---

## 七、明确不做（防范围膨胀）

| 不做 | 原因 |
|---|---|
| **把 `isWounded` 也状态化** | 它用**协程**实现（`StartCoroutine(onWounded)` + `WaitForSeconds`）。改成状态要重写异步逻辑，属于**另一次重构**，本次不碰 |
| **引入分层状态机（HSM）** | 目前只有 3 个状态，扁平结构足够。状态数上到 6~8 个再考虑 |
| **引入行为树** | 决策复杂度远没到这个量级，属于过度设计 |
| **`EnemyState.Skill`** | 枚举里已经预留但从未使用，本次**不实现**，也不删除（避免动到无关代码） |
| **玩家端的散 bool** | 玩家有 5 个散 bool（`isWounded`/`isDead`/`isAttack`/`isWallSliding`/`isSliding`），同类问题，但**是独立的另一次重构** |

---

## 八、步骤间的依赖与建议顺序

```
步骤 0（止血）
   │   独立可提交，可随时回滚
   ▼
步骤 1（引入 Dead 状态）
   │   此时 bug 已消除；TimeCounter 的隐患仍在
   ▼
步骤 2（TimeCounter 下沉 + 删守卫）
   │   ★ 核心步骤：状态机从此覆盖全部行为
   ▼
步骤 3（move 下沉）
       收尾：FixedUpdate 也变干净
```

**建议：步骤 0 单独一个 commit；步骤 1~3 可以合成一个 commit，也可以分开。**

> 步骤 2 完成之后，「死后还在跑 AI」这一**类**问题在结构上就不存在了 ——
> 不再依赖任何人记得在某处补守卫。
