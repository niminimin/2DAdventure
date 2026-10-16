# 2DAdventure

> 用 Unity 从零手写的 2D 横版动作游戏。
>
> 重点不在玩法体量，而在于把**动作手感、动画状态机、存档系统**这几块引擎细节做扎实 ——
> 所有踩过的坑和设计取舍都记录在 [`docs/`](docs/) 里。

**Unity 2022.3.62f3c1** · `C#` · `Addressables` · `Input System` · `Cinemachine` · `DOTween`

---

## 玩法

横版跑跳 + 近战攻击。玩家可以扒墙滑落、蹬墙跳、滑铲／滑铲跳；
敌人是巡逻型野猪，拥有「发现 → 追击 → 丢失目标 → 回到巡逻」的完整行为。

连招示例：

```
地面滑铲 ──[L]──▶ 滑铲跳 ──[空格]──▶ 空中撞墙扒住 ──[空格]──▶ 蹬墙跳
```

## 操作

| 操作 | 按键 |
|---|---|
| 移动 | `A` `D` ／ `←` `→` |
| 跳跃 | `Space` ／ `W` ／ `K` |
| 攻击 | `J` |
| 交互（开箱 / 存档点） | `E` |
| 滑铲 | `L`（**需在跑动中按下**） |

---

## 技术要点

### 1. 动作系统：扒墙滑落 / 蹬墙跳 / 滑铲 / 滑铲跳

四套动作，4 个动画剪辑、10 条状态转场。几个关键取舍：

- **蹬墙跳直接赋值 `velocity`，不用 `AddForce`** —— `AddForce` 是叠加在现有速度上的，
  而扒墙时 `vy` 恒为负值，会导致起跳高度随下滑速度漂移；直接赋值才能保证每次一样高。
- **蹬墙／滑铲跳后必须短暂锁定横向输入** —— `move()` 每帧无条件覆写 `velocity.x`，
  不锁的话横向冲量下一帧就被抹成 0，表现为「蹬了墙却原地起跳，然后贴回同一面墙」。
- **`wallSlideSpeed = 0` 并不等于静止** —— 物理步在 `FixedUpdate` **之后**还会再叠一次重力，
  恒定额外产生 `g × Δt ≈ 0.79 单位/秒` 的蠕滑。详见 [docs/wall-slide-jump.md](docs/wall-slide-jump.md)。

### 2. 动画状态机

- 解决 **`AnyState` 转场每帧把状态机拽回 Jump** 的问题：扒墙时 `isGround == false`，
  而 `AnyState → Jump` 的条件正是它，导致扒墙动画完全播不出来 —— 通过给该转场补充互斥条件解决。
- 修正 **攻击层覆盖一切动作动画** 的问题：攻击层是 `Override` 且**没有 Avatar Mask**，
  一旦进入就替换整个人物姿势。（纯 Sprite 2D **无法**用 Avatar Mask 只遮上半身 ——
  Mask 只对骨骼／Transform 层级有效，管不了 `SpriteRenderer.m_Sprite` 曲线。）

### 3. 存档系统

对象持稳定唯一 ID，通过 `ISaveable` 接口统一读写，JSON 持久化到 `persistentDataPath`。

**核心是「主动写 + 创建即读」的双向模型**（详见 [docs/save-system.md](docs/save-system.md)）：

- 原实现是**拉取式**：只在存档瞬间遍历「当前在场」的对象收集状态。
  但存档点在 Cave、宝箱在 Forest —— 存档那一刻宝箱根本不存在，状态永远收不到。
- 改为**推送式**：状态一变就主动写进内存；对象被重新创建时再读回。
- `JsonUtility` **完全不支持 `Dictionary`**（静默跳过、不报错），因此每个字典都配了影子 `List` 摊平。
- 「记录到内存」与「保存到磁盘」是两件事：前者在状态变化时，后者只在存档点。

### 4. 敌人 AI：手写有限状态机（FSM）

抽象状态基类统一定义 `OnEnter / LogicUpdate / OnExit`，派生类重写实现多态：

```
BoarPatrolState ⇄ BoarChaseState
```

- `BoxCast` 做**朝向视野**检测（发现玩家 → 转入追击）
- `OverlapCircle` 做**悬崖与墙体**探测，避免巡逻时掉崖

### 5. 事件驱动架构

以 **13 条 `ScriptableObject` 事件通道**替代单例直连，解决跨场景、跨 Prefab 对象
无法互相持有引用的问题。

### 6. 场景与加载

三级场景拆分，基于 Addressables 异步加载卸载，配合 DOTween 实现淡入淡出：

| 场景 | 职责 |
|---|---|
| `InitScene` | 启动入口，异步拉起常驻场景 |
| `Persistent` | **常驻**：UI / 玩家 / AudioManager / DataManager / SceneLoad |
| `Menu` | 主菜单（新游戏 / 继续 / 返回菜单） |
| `Forset` / `Cave` | 关卡 |

---

## 如何运行

### 方式一：直接下载可执行文件

到 [Releases](../../releases) 下载 `2DAdventure-v1.0.0-win64.zip`，
**解压到任意目录**后双击 `2DAdventure.exe`（不要直接在压缩包里运行）。

- 系统要求：Windows 10/11 64 位

### 方式二：用 Unity 打开

1. 用 **Unity 2022.3.x** 打开项目（仓库未包含 `Library/`，首次打开需要重新导入资源）
2. 直接按 Play 即可运行 —— Addressables 的 Play Mode 脚本请设为 **Use Asset Database (fastest)**
3. 若要**打包**，必须先在 `Window > Asset Management > Addressables > Groups`
   里执行 `Build > New Build > Default Build Script` 烘焙资源内容

---

## 项目结构

```
Assets/
├── Scripts/
│   ├── Player/          玩家控制、动画、攻击、受伤
│   ├── Enemy/           敌人 FSM（基类状态 + 野猪巡逻/追击）
│   ├── General/         Character 基类、Chest、PhysicsCheck、Attack
│   ├── SaveLoad/        DataManager / Data / ISaveable
│   ├── ScriptableObject/事件通道（VoidEventSO / FloatEventSO / ...）
│   ├── UI/              暂停面板、菜单、血条
│   ├── Transition/      SceneLoad、传送点
│   └── Audios/          AudioManager、AudioDefination
├── Animations/          动画剪辑与 Animator Controller
├── Prefabs/             Boar 等
├── Scenes/              5 个场景
├── Data SO/             ScriptableObject 资产（含 13 条事件通道）
└── Art Assets/          第三方美术素材
```

---

## 设计文档

排查过程中把所有**踩过的坑和设计取舍**都记了下来，这也是这个项目最想展示的部分：

| 文档 | 内容 |
|---|---|
| [docs/save-system.md](docs/save-system.md) | 存档系统：核心机制、四条触发路径、四个必知的坑 |
| [docs/wall-slide-jump.md](docs/wall-slide-jump.md) | 扒墙 / 蹬墙跳：触发条件、参数推导、四个坑 |
| [docs/slide.md](docs/slide.md) | 滑铲：素材分组验证、输入系统扩展、三个坑 |
| [docs/enemy-fsm-refactor.md](docs/enemy-fsm-refactor.md) | **待执行**：敌人 FSM 重构方案 —— 把游离在状态机之外的逻辑收回状态 |
| [docs/player-state-refactor.md](docs/player-state-refactor.md) | **评估稿（尚未决定）**：玩家是否该做状态机重构，含决策触发条件 |

---

## 已知问题

| # | 问题 | 状态 |
|---|---|---|
| [#1](../../issues/1) | 野猪死亡后仍会执行转向检测，尸体可能翻转朝向 | 已修复 |
| [#3](../../issues/3) | 场景切换返回时 BGM 会从头重播 | 已修复 |
| [#4](../../issues/4) | 敌人死亡未持久化 | 已修复 |
| [#6](../../issues/6) | 宝箱开启状态未持久化 | 已修复 |

---

## 第三方资源

本项目使用了以下第三方资源，版权归各自作者所有：

- **Legacy Fantasy High Forest**、**Generic Character** —— Unity Asset Store 美术素材
- **DOTween** © Daniele Giardini – Demigiant
- **TextMesh Pro** © Unity Technologies
- **Cinemachine** © Unity Technologies
