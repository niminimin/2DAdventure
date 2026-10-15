# 扒墙 / 蹬墙跳（Wall Slide & Wall Jump）

> 功能：人物在空中碰到左右墙壁时，若正在下落且按住朝墙方向，可以**缓慢下滑扒住墙壁**；
> 此时按跳跃键可**蹬墙跳开**，实现左右横跳爬墙。

本文记录**做了什么、为什么这样做、以及怎么调参**。

---

## 一、素材

扒墙动画的 4 帧来自图集里的子精灵（**不是独立文件**）：

```
图集：Assets/Art Assets/Generic-Character/png/blue/char_blue_2.png
精灵：char_blue_wallSlide0 / 1 / 2 / 3
```

同一张图里还有 `walk0-9`、`slide0_0-2` / `slide1_0-2` / `slide2_0-1`、`attack2_0-7`、`2_30-39`。

---

## 二、触发条件

### 进入扒墙（5 个条件必须**同时**满足）

| # | 条件 | 代码 |
|---|---|---|
| 1 | 在空中 | `!phyCheck.isGround` |
| 2 | 未受伤、未死亡 | `!isWounded && !isDead` |
| 3 | 贴着左墙或右墙 | `phyCheck.touchLeftWall \|\| phyCheck.touchRightWall` |
| 4 | **正在下落** | `rb.velocity.y <= 0.01f` |
| 5 | **按住朝墙方向** | `inputDirection.x * dir > 0.01f` |

### 退出扒墙（任一满足）

| 触发 | 去向 |
|---|---|
| 松开朝墙方向 | 回到普通下落（`Jump` 状态） |
| 落地 | `BlueLand` → `BlueIdle`/`BlueRun` |
| 墙到头（不再 `touchWall`） | 回到普通下落 |
| 按跳跃键 | **蹬墙跳** |
| 受伤 / 死亡 | 强制脱离 |
| **按攻击键** | **被忽略** —— 扒墙时禁止攻击（原因见第六节「坑 4」） |

### 扒墙中每帧行为

```csharp
// PlayerController.FixedUpdate()
rb.velocity = new Vector2(inputDirection.x * speed, -wallSlideSpeed);
//              横向照常给（被墙挡住 = 贴住）      纵向锁定为缓慢下滑
```

### 蹬墙跳

```csharp
// PlayerController.WallJump()
isWallSliding     = false;
wallJumpLockTimer = wallJumpLockTime;
float away        = -wallSlideDir;
rb.velocity          = new Vector2(away * wallJumpXForce, wallJumpYForce);
transform.localScale = new Vector3(away, 1, 1);   // 人物转向背对墙
```

---

## 三、参数（Inspector 可调）

挂在玩家身上 `PlayerController` 的 **「扒墙 / 蹬墙跳」** 分组：

| 参数 | 默认值 | 说明 |
|---|---|---|
| `wallSlideSpeed` | **2** | 下滑速度。**调成 0 就变成「完全扒住不动」** |
| `wallJumpYForce` | **17** | 蹬墙跳垂直初速。与 `jumpForce` 一致 → 蹬墙跳和普通跳一样高 |
| `wallJumpXForce` | **8** | 蹬墙跳横向初速。略大于 `speed`(6) → 弹开感明显 |
| `wallJumpLockTime` | **0.2** | 蹬墙后锁定横向输入的时间 |

### 数值是怎么算的

```
玩家 Rigidbody2D: gravityScale = 4  →  g = 9.81 × 4 = 39.24
jumpForce = 17  →  跳高 = 17² / (2 × 39.24) ≈ 3.68 单位
wallJumpYForce = 17 → 蹬墙跳同样 3.68 单位高
wallJumpXForce(8) × wallJumpLockTime(0.2) = 1.6 单位分离距离
    墙探测最远只到离中心 0.52 单位 → 1.6 足够脱离，不会被重新吸住
```

---

## 四、改了哪些文件

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/Player/PlayerController.cs` | 新增 4 个参数 + `isWallSliding` 状态；新增 `UpdateWallSlide()`、`WallJump()`；改 `FixedUpdate()`、`Jump()`、`PlayerAttack()`、`PlayerRevive()` |
| `Assets/Scripts/Player/PlayerAnimation.cs` | `SetAnimation()` 加一行喂 `isWallSlide` 给 Animator |
| `Assets/Animations/Player/BlueWallSlide.anim` | **新建**：4 帧循环，`frameRate=8`，`length=0.5s` |
| `Assets/Animations/Player/PlayerAnimator.controller` | 新增参数 + 状态 + 3 条转场；**并修正了原有的 `ANY -> Jump`** |

> 没有动 `Enemy` 那套 `BaseState` 状态机 —— 玩家继续用现有的**标志位风格**（`isWounded` / `isDead` / `isAttack`），保持一致。

---

## 五、Animator 结构

### 参数

```
velocityX(Float)  velocityY(Float)  isGround(Bool)
wounded(Trigger)  isDead(Bool)      isAttack(Bool)  attack(Trigger)
isWallSlide(Bool)   ← 本次新增
```

扒墙状态判定全部在 `PlayerController.UpdateWallSlide()` 里完成，`PlayerAnimation` 只负责把结果喂给 Animator —— **不在 Animator 里做物理判断**。

### Base Layer 状态图（改动后）

```
BlueIdle ──[velocityX > 0.1]──▶ BlueRun
BlueRun  ──[velocityX < 0.1]──▶ BlueIdle

Jump (Blend Tree) ──[isGround = true]──▶ BlueLand
Jump ──[isWallSlide = true]──▶ BlueWallSlide          ← 新增
BlueWallSlide ──[isWallSlide=false && isGround=false]──▶ Jump    ← 新增
BlueWallSlide ──[isGround = true]──▶ BlueLand          ← 新增
BlueLand ──[退出时间到]──▶ BlueIdle
BlueLand ──[velocityX > 0.1]──▶ <EXIT>

ANY ──[isGround=false && isWallSlide=false]──▶ Jump    ← 关键修正
```

---

## 六、🔴 四个必须知道的坑（都是实际踩过的）

### 坑 1：`move()` 每帧无条件覆写 `velocity.x` → 必须有输入锁定

```csharp
// PlayerController.move()
rb.velocity = new Vector2(inputDirection.x * speed, rb.velocity.y);   // ← 无条件覆写
```

蹬墙跳给了横向冲量后，如果玩家没按方向键，**下一帧 `FixedUpdate` 就会被写成 0**，
表现是「蹬了墙却原地起跳，然后贴回同一面墙」。

所以 `wallJumpLockTimer` **不是锦上添花，是必需品**：

```csharp
if (wallJumpLockTimer > 0f)
{
    wallJumpLockTimer -= Time.fixedDeltaTime;
    return;                        // 锁定期内完全不碰 velocity.x
}
```

### 坑 2：普通跳用 `AddForce`（叠加），蹬墙跳必须用**直接赋值**

```csharp
// 普通跳（保留原样）
rb.AddForce(transform.up * jumpForce, ForceMode2D.Impulse);

// 蹬墙跳
rb.velocity = new Vector2(away * wallJumpXForce, wallJumpYForce);
```

`AddForce` 是**叠加**在现有速度上的。扒墙时 `vy` 恒为 `-wallSlideSpeed`，
用 `AddForce` 的话蹬墙高度会随下滑速度漂移；直接赋值才能保证**每次都一样高**。

### 坑 3：`ANY -> Jump` 会把扒墙动画每帧拽走

原有转场是：

```yaml
ANY -> Jump   [isGround = IfNot]
```

它会在**任何状态**下触发。一旦加了 `BlueWallSlide`，因为扒墙时 `isGround` 就是 `false`，
这条 AnyState 转场会**每帧**把状态机拉回 `Jump`，扒墙动画根本播不出来。

**修正**：给它补一个 `isWallSlide = IfNot` 条件：

```yaml
ANY -> Jump   [isGround = IfNot, isWallSlide = IfNot]
```

> 为什么原来和 `BlueLand` 不冲突？因为 `BlueLand` 是在 `isGround == true` 时进入的，
> AnyState 的 `isGround = IfNot` 自然为假，不会打架。扒墙状态没有这个「天然豁免」。

### 坑 4：扒墙时仍能攻击，攻击动画会把扒墙整个盖掉

**症状**：扒在墙上按 **J**，人物立刻切成攻击动画，但**身体还在贴墙往下滑**。

**两个原因叠加**：

**① 代码层** —— `PlayerAttack()` 原本没有任何状态守卫：

```csharp
private void PlayerAttack(InputAction.CallbackContext context)
{
    GetComponent<PlayerAnimation>().PlayerAttack();   // animator.SetTrigger("attack")
    isAttack = true;
}
```

对比 `Jump()` 是有守卫的（`if (isGround) ... else if (isWallSliding)`），攻击这条路径漏了。

**② 动画层** —— Attack Layer 是 **weight=1 / Override / `mask = null`** 的全身层：

| Layer | 名称 | Weight | Avatar Mask | Blending |
|---|---|---|---|---|
| 0 | Base Layer | 基础层 | null | Override |
| 1 | Wound Layer | 1 | null | Override |
| 2 | **Attack Layer** | 1 | **null** | Override |

Mask 为空 → 一进攻击状态就**整个人物姿势被替换**，Base Layer 的 `BlueWallSlide` 直接被盖掉。

> ⚠️ **不要试图用 Avatar Mask 做「只盖上半身」** —— 在纯 Sprite 2D 里这行不通。
> Avatar Mask 只能遮罩骨骼 / Transform 层级，而这里动画曲线直接改 `SpriteRenderer.m_Sprite`（`classID: 212`），
> Mask 对这类曲线没有任何作用。

**③ 附带后果：物理与动画不一致**

`UpdateWallSlide()` 的守卫是 `isGround || isWounded || isDead`，**没有 `isAttack`**。
所以攻击期间物理上仍在扒墙下滑，等攻击播完、人还挂在墙上时，动画会突然从攻击切回扒墙，看起来很跳。

**修复（已实施）**：

```csharp
private void PlayerAttack(InputAction.CallbackContext context)
{
    if (isWallSliding) return;     // 扒着墙，腾不出手
    GetComponent<PlayerAnimation>().PlayerAttack();
    isAttack = true;
}
```

**外加一道 Animator 保险** —— Attack Layer 的进入转场加 `isWallSlide = IfNot`：

```yaml
Attack Layer:  New State -> PlayerAttack1  [isAttack=If, attack=If, isWallSlide=IfNot]
```

> **不能只靠 Animator 保险**：那样 `isAttack` 永远不会被 `AttackBehaviour.OnStateExit()` 复位，
> 会一直卡在 `true`，等人离开墙的瞬间突然触发一次攻击。

---

## 七、调参指南

> ### ⚠️ 改完 Inspector 里的参数，**必须 `Ctrl+S` 保存场景，再按 Play**
>
> 本项目的场景是 **Addressables 加载**的：按 Play 时 `InitLoad.Awake()` 会执行
> `Addressables.LoadSceneAsync("Persistent")`，加载的是 **AssetDatabase 里已保存的场景**。
> 场景不保存的话，运行时实例拿到的还是旧版本 —— 你在 Inspector 里改的数字**根本进不去**。
>
> **这个坑真实发生过**：`wallSlideSpeed` 被改成 `0`（想完全扒住不动）却毫无效果，
> 观察到的仍然是缓慢下滑。原因是 `Persistent.unity` 里**当时连这个字段都不存在** ——
>
> 给脚本**新增**序列化字段时，Unity **不会**自动写进已有的场景实例；
> 字段在场景里缺失时，反序列化一律回落到**脚本默认值 `wallSlideSpeed = 2f`**。
> 所以表现就是「改了没用，还是有点快」。
>
> **判断方法**：`git diff Assets/Scenes/Persistent.unity` —— 如果看不到你新加的字段，
> 就说明场景没保存，运行时用的还是脚本默认值。

| 想要的效果 | 怎么调 |
|---|---|
| 下滑更快 / 更慢 | 调 `wallSlideSpeed`（注意下面那条「设 0 也会蠕滑」） |
| **真正**完全扒住不掉 | `wallSlideSpeed = 0` **且**扒墙期间把 `gravityScale` 置 0 —— 单设 0 仍有约 0.79 单位/秒的蠕滑 |
| 蹬墙跳得更高 | 调大 `wallJumpYForce`（注意别超过 `jumpForce` 太多，否则普通跳失去价值） |
| 弹开更远 | 调大 `wallJumpXForce` 或 `wallJumpLockTime` |
| 蹬墙后感觉「操作被夺走」 | 调小 `wallJumpLockTime`（但低于 0.15 左右就会开始被 `move()` 抹掉横向速度） |
| 扒墙动画太慢 / 太快 | 改 `BlueWallSlide.anim` 的 `frameRate`（当前 8） |

### ⚠️ `wallSlideSpeed = 0` **并不等于**「完全静止」

```
实际下滑速度 = wallSlideSpeed + 重力 × gravityScale × FixedTimestep
```

代入本项目参数（`Fixed Timestep = 0.02`、`Physics2D.gravity = -9.81`、玩家 `gravityScale = 4`）：

```
重力 × gravityScale × FixedTimestep = 9.81 × 4 × 0.02 = 0.785 单位/秒
```

| 你设的 `wallSlideSpeed` | **实际下滑速度** |
|---|---|
| `0` | **0.785 单位/秒**（能看到缓慢蠕滑） |
| `2`（脚本默认） | **2.785 单位/秒** |

**根因是 Unity 物理步的执行顺序：**

```
① FixedUpdate()              → 我们写 vy = -wallSlideSpeed
② 物理步：施加重力            → vy += g × dt          ← 凭空多出 -0.785
③ 物理步：用【新的 vy】积分位置 → Δy = vy × dt
```

`FixedUpdate` 只是把速度**改掉**，但紧接着物理步**还会再叠一次重力**才去积分位置 ——
所以无论 `wallSlideSpeed` 设多少，都会额外多出 `g × dt` 这一段。

**想真正锁死**，需要在扒墙期间把重力一起关掉（放在 `FixedUpdate` 里，幂等、不会漏恢复）：

```csharp
rb.gravityScale = isWallSliding ? 0f : normalGravityScale;
```

`normalGravityScale` 建议在 `Awake()` 里缓存 `rb.gravityScale` 的原始值。

### ❌ 这不是物理材质的问题

摩擦是**切向阻力**，只能**减缓**下滑，**不可能制造**下滑。
墙的有效摩擦是 `0.2`（Unity 内置默认材质 `0.4` 与玩家材质 `0` 取平均），
它反而在**轻微抑制**这个蠕滑；`bounciness = 0`，垂直方向也没有弹跳。

> **当前取舍**：保留这 0.785 单位/秒的蠕滑。它让「扒墙」有个自然的衰减感，
> 也避免玩家无限挂在墙上。要改成绝对静止就按上面的 `gravityScale` 方案。

---

## 八、已知限制 / 上线前需要实测的点

1. **左右判定可能不对称**
   玩家碰撞体是 `CapsuleCollider2D offset = (-0.1, 0.9)`，而 `localScale.x = -1`（人物朝左）
   **会镜像碰撞体**；但 `PhysicsCheck` 的左右探测点是不随翻转变化的固定值
   （`leftOffset = -0.44`、`rightOffset = +0.28`）。

   结果：朝左时左探测点比碰撞体边缘**多伸出约 0.19 单位**，可能表现为「还差一点就隔空下滑」。

   **如果肉眼可见**，给墙检测单独加一组参数（如 `wallCheckRadius` / `wallCheckDistance`），
   不要继续蹭用为敌人悬崖检测调过的 `leftOffset` / `rightOffset`。

2. **墙上有 0.2 的摩擦**
   墙的 `TilemapCollider2D` 没有指定材质 → 走 Unity 内置默认 `0.4`；
   玩家材质 `normal.physicsMaterial2D` 是 `0`；默认合并方式 Average → **0.2**。

   因为扒墙时**每帧直接覆写 `velocity.y`**，这个摩擦影响可控。
   若实测下滑卡顿，把 `Project Settings > Physics 2D > Default Material` 设成 `normal`。

3. **素材朝向约定**
   需要确认 `char_blue_wallSlide0-3` 画的是「面朝哪边贴哪侧墙」。
   人物靠 `localScale.x = -1` 镜像，若方向约定相反，贴左墙时人物会「背对墙」。

4. **未做跳跃缓冲 / Coyote Time**
   本次只交付扒墙 + 蹬墙跳核心。`Jump()` 用的是 `started` 回调，没有缓冲窗口，
   落地前一瞬按跳会丢失输入。属于独立的手感优化项。

---

## 九、验证记录

| 项 | 结果 |
|---|---|
| 编译 | 0 错误 0 警告 |
| `BlueWallSlide.anim` | `frameRate=8`、`length=0.5`、`isLooping=True`、4 帧顺序正确 |
| Animator 参数 | `isWallSlide(Bool)` 已存在 |
| Animator 状态 | `BlueWallSlide` motion 已绑定 |
| Base Layer 转场 | 4 条全部就位（含 `ANY -> Jump` 修正） |
| Attack Layer 保险 | `New State -> PlayerAttack1 [isAttack=If, attack=If, isWallSlide=IfNot]` |
| 扒墙时攻击 | 代码守卫 `if (isWallSliding) return;` 已加 |
| 场景序列化 | `Persistent.unity` 已写入 `wallSlideSpeed` / `wallJumpYForce` / `wallJumpXForce` / `wallJumpLockTime` / `isWallSliding`（见第七节的保存坑） |
