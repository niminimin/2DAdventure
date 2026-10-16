# 玩家状态机重构 · 评估稿

> ## ⚠️ 本文**不预设结论**
>
> **状态：评估阶段 —— 尚未改动任何代码，也尚未决定是否要改。**
>
> 玩家是否要重构**仍然值得商榷**。本文的目标是**把判断依据摆清楚**，
> 而不是论证"应该做"。第七、八节分别列出**支持**和**反对**的理由，
> 第十节给出**决策触发条件** —— 到那时再决定也不迟。
>
> 对照阅读：[敌人 FSM 重构方案](enemy-fsm-refactor.md)（那一份是**已决定要做的方案**）

---

## 一、现状盘点

### 1.1 相关代码规模

| 文件 | 行数 |
|---|---|
| `Assets/Scripts/Player/PlayerController.cs` | **406** |
| `Assets/Scripts/Player/PlayerAnimation.cs` | 47 |
| `Assets/Scripts/Player/AttackBehaviour.cs` | 36 |
| `Assets/Scripts/Player/WoundedBehaviour.cs` | 36 |
| `Assets/Scripts/General/Character.cs` | 182 |

### 1.2 状态相关字段（共 9 个）

```csharp
// PlayerController.cs:43-54
public bool  isWounded;                        // 硬直
public bool  isDead;                           // 死亡
public bool  isAttack;                         // 攻击中
public bool  isWallSliding;                    // 扒墙中
public bool  isSliding;                        // 滑铲中

private float wallSlideDir;                    // 扒墙方向
private float wallJumpLockTimer;               // 横向输入锁定（蹬墙跳/滑铲跳共用）
private float slideDir;                        // 滑铲方向
private float slideTimer;                      // 滑铲剩余时间
```

### 1.3 耦合面：这些字段在文件里被反复读取

| 字段 | 在 `PlayerController.cs` 中出现 |
|---|---|
| `isSliding` | **10 次** |
| `isWallSliding` | **9 次** |
| `wallJumpLockTimer` | **8 次** |
| `isWounded` | **7 次** |
| `isDead` | **6 次** |
| **合计** | **40 次** |

### 1.4 同一个判断散落在 3 处（核心问题）

```csharp
// ① FixedUpdate —— 优先级靠代码顺序隐式表达
private void FixedUpdate()
{
    UpdateWallSlide();
    UpdateSlide();

    if (isWounded) return;                              // 优先级 1
    if (wallJumpLockTimer > 0f) { ...; return; }        // 优先级 2
    if (isSliding) { ...; return; }                     // 优先级 3
    if (isWallSliding) { ...; } else { move(); }        // 优先级 4
}

// ② Jump —— 又判断一遍
private void Jump(InputAction.CallbackContext context)
{
    if (phyCheck.isGround) { if (isSliding) SlideJump(); else ...; }
    else if (isWallSliding) { WallJump(); }
}

// ③ PlayerAttack —— 再判断一遍
private void PlayerAttack(InputAction.CallbackContext context)
{
    if (isWallSliding) return;
    if (isSliding) return;
    ...
}
```

**"我现在是什么状态"这件事，被重复表达了 3 次。**

> 这与敌人 #1 的根因**属于同一类风险**：约束写在多个调用点，漏一处就是一个 bug。
> 区别在于 —— **敌人那边已经真的漏了一处并产生了 bug；玩家这边还没触发。**

### 1.5 互斥关系目前靠"隐式条件"保证

`isWallSliding` 与 `isSliding` 之所以互斥，是因为：

- `UpdateWallSlide()` 要求 `!phyCheck.isGround`
- `Slide()` 要求 `phyCheck.isGround`

**但没有任何东西强制这件事。** 哪天有人放宽了其中一个条件，就可能同时为 `true`。

---

## 二、判断标准：一个 bool 该不该变成状态

只看一件事：

> **它和其他 bool 是「互斥」还是「可叠加」？**

| | 处理方式 |
|---|---|
| **互斥**（同一时刻只能有一个） | 适合做**状态** |
| **可叠加**（能与其他状态共存） | **不该**做状态，保持 flag 或独立层 |

---

## 三、逐个字段判定

| 字段 | 互斥？ | 可叠加？ | 判定 |
|---|---|---|---|
| `isDead` | ✅ 终态 | ❌ | **状态** |
| `isWounded` | ✅ 硬直期间锁死 `move()` | ❌ | **状态**（带计时） |
| `isSliding` | ✅ | ❌ | **状态**（带计时） |
| `isWallSliding` | ✅ | ❌ | **状态** |
| ⚠️ `isAttack` | ❌ **能边跑边攻击** | ✅ | **不该做状态** —— 保持 flag（动画上它本来就是独立 Layer） |
| ⚠️ `wallJumpLockTimer` | ❌ 与空中状态**共存** | ✅ | **不是状态** —— 是 0.2 秒的瞬时修饰 |
| ⚠️ `isInvulnerable`（在 `Character`） | ❌ 与任何状态共存 | ✅ | **不是状态** —— 无敌帧修饰 |

> **关键区分：不是所有计时器都是状态。**
> `isSliding` 排他地占据整个角色（是状态）；
> `wallJumpLockTimer` 只是给空中状态加的一个修饰（不是状态）。

---

## 四、玩家与敌人的本质差异

这决定了**做法不该照搬**：

| | 敌人 | 玩家 |
|---|---|---|
| 状态数量 | 2~3 | **6~8** |
| **转移由谁驱动** | **世界**（视野 / 丢失计时） | **输入 + 物理条件** |
| 转移密度 | 低（几秒一次） | **高**（玩家随时操作） |
| 状态本质 | **决策**："我该做什么" | **运动约束的产物**："我现在处于什么处境" |

**最关键的一条：**

> 敌人的状态是**决策**；玩家的状态大多是**输入和物理条件的函数**。
>
> 例如"跑步"不是玩家决定的，而是「在地面 + 有水平输入」的**结果** ——
> 它**每帧都能算出来，不需要存储**。

### 因此玩家的状态可分两类

| 类型 | 例子 | 特征 | 需要存储？ |
|---|---|---|---|
| **粘性状态** | `Dead` / `Wounded` / `Sliding` | 一旦进入由**计时器**维持，中途条件变化也不打断 | ✅ 必须存 |
| **派生状态** | `Idle` / `Run` / `Airborne` / `WallSlide` | 完全由「输入 + 是否在地面 + 是否贴墙」算出 | ❌ 每帧算即可 |

---

## 五、如果要做的两种方案

### 方案 A（轻）· 优先级计算

把散落三处的优先级**收敛成一个函数**，不引入状态类：

```csharp
private PlayerState ComputeState()
{
    // —— 第 1 层：粘性状态（有计时器，一旦生效压倒一切）——
    if (isDead)    return PlayerState.Dead;
    if (isWounded) return PlayerState.Wounded;
    if (isSliding) return PlayerState.Sliding;

    // —— 第 2 层：派生状态（由输入 + 物理条件决定）——
    if (!phyCheck.isGround)
        return IsWallSliding() ? PlayerState.WallSlide : PlayerState.Airborne;

    return Mathf.Abs(inputDirection.x) > 0.1f ? PlayerState.Run : PlayerState.Idle;
}
```

**最大的价值：把现在"隐式的优先级"变成"显式的代码顺序"。**
读这段代码的顺序，就是设计上的优先级顺序。

`FixedUpdate` 随之变成：

```csharp
switch (ComputeState())
{
    case PlayerState.Wounded:   break;
    case PlayerState.Sliding:   rb.velocity = new Vector2(slideDir * slideSpeed, rb.velocity.y); break;
    case PlayerState.WallSlide: rb.velocity = new Vector2(inputDirection.x * speed, -wallSlideSpeed); break;
    default:                    move(); break;
}
```

**改动面小**：不碰输入回调（`Jump` / `Attack` / `Slide` 可以继续 `if (curState == ...)` 判断），不改手感。

### 方案 B（重）· 状态类 + 多态

照搬敌人的 `BaseState` 那套，每个状态一个类：

```csharp
public abstract class PlayerStateBase
{
    public abstract void OnEnter(PlayerController p);
    public abstract void LogicUpdate();
    public abstract void PhysicsUpdate();
    public abstract void OnExit();
}
```

6~8 个状态类，`FixedUpdate` 变成 `curState.PhysicsUpdate()`。

**但有个必须解决的问题**：玩家的 `Jump` / `Attack` / `Slide` 是**输入事件回调**，不是每帧轮询。
要么给状态基类加 `OnJump()` / `OnSlide()` 接口（8 个类都要实现一遍），
要么回调里查 `curState`（那就退化成方案 A 的判断方式）。

### 对比

| | 方案 A · 优先级计算 | 方案 B · 状态类 |
|---|---|---|
| 改动面 | 小（1 个新函数 + 改 `FixedUpdate`） | 大（8 个新类 + 改所有回调） |
| 优先级显式化 | ✅ | ✅ |
| 互斥有结构保证 | ⚠️ 靠 `ComputeState` 的 `if` 顺序 | ✅ 靠单引用 |
| 输入事件分发 | 无需改动 | **需要额外设计** |
| 手感回归风险 | 低 | **高** |
| 状态数增长时的可扩展性 | 中 | 高 |

---

## 六、支持「做」的理由

1. **消除同款风险** —— 1.4 节那个"同一个判断散落 3 处"的结构，和敌人 #1 的根因是同一类。
   敌人那边已经真的漏了；玩家这边只是**还没触发**。
2. **优先级显式化** —— 现在的优先级（`isWounded` > `wallJumpLockTimer` > `isSliding` > `isWallSliding`）
   完全靠 `FixedUpdate` 里的代码顺序表达，**改一行顺序就可能改变游戏行为，而且没有任何提示**。
3. **互斥关系需要强制** —— 目前 `isWallSliding` / `isSliding` 互斥靠两个方法各自的条件，
   **没有编译器或结构层面的保证**。
4. **为后续动作铺路** —— 如果以后要做冲刺、攀爬、盾反、连段取消（attack cancel），
   bool 数量会继续增长，**越晚重构越贵**。

---

## 七、支持「不做」的理由

1. **目前没有实际 bug** —— 与敌人不同，玩家侧还没有出现过"两个 bool 同时成立"的真实故障。
   重构的是**潜在**风险，不是**已发生**的问题。
2. **玩家的状态大多是派生的** —— 按第三节的划分，真正需要"存储"的只有
   `Dead` / `Wounded` / `Sliding` 三个。**结构性收益不如敌人明显**。
3. **手感极度敏感** ⚠️ —— 玩家是体验核心，优先级、锁定时间、速度合成差一点点都能感觉出来。
   而敌人的重构不会影响手感。
4. **代码规模尚可读** —— `PlayerController.cs` 406 行，还在"一眼能看完"的范围内。
5. **收益是"未来的"** —— 而成本是"现在的"，且回归测试要覆盖全部动作。

---

## 八、如果要做的渐进路径

**建议分两步，第一步就够用很久：**

### 第 1 步 · 只做优先级显式化（低风险）

- 新增 `PlayerState` 枚举 + `ComputeState()`
- `FixedUpdate` 改成 `switch`
- **不碰** `Jump` / `Attack` / `Slide` 三个输入回调
- **不碰** `wallJumpLockTimer` 的语义
- 手感应当完全不变（只是把原有优先级搬到一处）

### 第 2 步 · 视情况再决定是否上状态类

只有当"状态数继续增长"或"输入分发确实需要按状态分派"时，才做方案 B。

### ⛔ 明确不做

- **`isAttack` 不要进状态机** —— 它能和跑步叠加，进状态机反而会破坏"边跑边打"
- **`wallJumpLockTimer` / `isInvulnerable` 不要进** —— 它们是修饰，不是状态

---

## 九、结论：不预设，给出决策触发条件

**本文不给出"做"或"不做"的结论。** 建议等到下列**任一条件成立**时再重新评估：

| # | 触发条件 | 为什么它意味着该做了 |
|---|---|---|
| 1 | 玩家动作再增加 **2 个以上**（冲刺 / 攀爬 / 盾反…） | bool 数量到 7~8 个，耦合面（当前 40 处引用）会显著恶化 |
| 2 | 出现第一次「两个状态 bool 同时为 true」的实际 bug | 说明隐式互斥已经撑不住了 |
| 3 | 需要在 **4 处以上**补同款守卫 | 说明"判断散落"已经到了不可维护的程度 |
| 4 | 要做**动作取消 / 连段系统**（attack cancel、combo） | 这类系统**必须**有显式状态才能表达优先级与取消窗口 |
| 5 | `PlayerController.cs` 超过 **600 行** | 单文件认知负荷开始过高 |

**在那之前**，玩家现状是"**有潜在风险但可控**"。
如果要投入重构时间，**优先做敌人那份**（有实际 bug + 顺带修职责错位 + 手感风险低）。

---

## 十、附：本节结论所依据的关键判断

如果将来重新评估，这三个判断是核心，可优先复核：

1. **玩家的状态大多是「输入 + 物理条件的函数」**，因此不必都存储 ——
   这条如果被推翻（比如引入大量有内部计时的动作），方案 B 的必要性会上升。
2. **`isAttack` 可叠加，不属于运动状态** ——
   如果将来攻击也要锁移动（变成硬直），那它就该进状态机。
3. **手感回归风险高于敌人** ——
   如果有充分的时间做回归测试（每个动作 × 各种边界），这条的权重会下降。
