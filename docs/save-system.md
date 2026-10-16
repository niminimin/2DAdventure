# 存档系统（Save / Load）

> 记录存档系统的**核心机制**、架构、四条触发路径、以及踩过的坑。
> 本文由「修复敌人死亡未持久化」这个 bug 的排查过程整理而来。
>
> **只想快速理解机制的话，直接看第二节。**

---

## 一、三个核心角色

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/SaveLoad/DataManager.cs` | 单例（挂在 Persistent 上），标了 **`[DefaultExecutionOrder(-100)]`**。负责登记对象、汇总数据、读写 `save.json` |
| `Assets/Scripts/SaveLoad/Data.cs` | 纯数据容器。**业务用 `Dictionary`，落盘靠影子 `List`**（原因见坑 1） |
| `Assets/Scripts/SaveLoad/ISaveable.cs` | 所有可存档对象实现的接口：`GetDataID()` / `GetSaveData(Data)` / `LoadData(Data)` |

存储位置：

```csharp
Application.persistentDataPath + "/save.json"
// Windows: %USERPROFILE%\AppData\LocalLow\<公司名>\<产品名>\save.json
```

> ⚠️ 改 Player Settings 里的 **Company Name / Product Name 会改变这个路径**，旧存档会因此"找不到"。

### 谁实现了 ISaveable

| 对象 | 存什么 |
|---|---|
| `Character`（玩家 / 敌人） | 位置 + 血量 |
| `SavePoint` | `interactHasTriggered[ID] = isDone` |
| `Chest` | `interactHasTriggered[ID] = isDone` |
| `SceneLoad` | 当前场景（`GameObject` 序列化成 JSON）+ 玩家位置 |

---

## 二、核心机制：主动写 + 创建即读

### 一句话

> **状态变化时，对象主动把状态推给 `DataManager`；
> 对象被重新创建时，主动向 `DataManager` 拉回自己的状态。**

`DataManager` 不"定时去问"。它只是**一本长期存活的记事本**。

### 为什么这本记事本能活过场景切换

```csharp
// SceneLoad.cs
yield return curLoadScene.sceneReference.UnLoadScene();        // 只卸载【当前 Location 场景】
playerTrans.gameObject.SetActive(false);
sceneToLoad.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);
```

**`Persistent` 从游戏启动到结束，从来没有被卸载过。**
→ 挂在它上面的 `DataManager.instance` 一直活着
→ `saveData` 里那几个字典一直活着

所以：野猪和宝箱被销毁了，但**关于它们的记录住在另一个不会被销毁的地方**。

### 原来的模型错在哪

原来的 `DataManager` 用的是**拉取式（pull）**：

```
存档点触发 → DataManager 遍历 saveableList，逐个问"你现在什么状态？"
```

这个模型有个致命限制：**只有"在场"的对象才能被问到。**

而本项目的布局是 **存档点在 Cave、宝箱在 Forest** —— 存档那一刻宝箱根本不存在，永远不会被问到。

**正确模型是推送式（push）**：

```
状态一变   → 对象自己推给 DataManager（写记事本）
对象被创建 → 对象自己从 DataManager 拉回（读记事本）
```

### 两个动作

| 时刻 | 钩子 | 做什么 |
|---|---|---|
| **状态变化** | 事件发生的那一行 | `DataManager.MarkXxx(id, value)` —— **写**记事本 |
| **对象被创建** | `Awake` / `OnEnable` / `Start` | `DataManager.LoadFor(this)` 或 `IsXxx(id)` —— **读**记事本 |

### ⚠️ 读的时机是「实例诞生」，不是「每帧」

同一个实例活着的时候**不要反复读** —— 读了会覆盖玩家中途的进度。

比如玩家受伤后 `curHealth` 变了但还没存档，
如果每帧都 `LoadData`，这次受伤就会被"治好"。

**读只在「新实例诞生时」做一次。**

### 该用哪个钩子？`Awake` / `OnEnable` / `Start`

| 钩子 | 调用时机 | 同一实例调用次数 | 本项目用在哪、为什么 |
|---|---|---|---|
| **`Awake`** | 实例化后**立刻**，早于 `OnEnable` | **1 次** | `Enemy.Awake()` 查死亡标记 —— 需要**最早**，`Destroy` 得够快才不会闪一帧 |
| **`OnEnable`** | `Awake` 之后；此后每次 `SetActive(true)` | **可能多次** | `Chest.OnEnable/OnDisable` 成对注册/反注册；`SavePoint` 也是这个做法 |
| **`Start`** | 第一帧 `Update` 之前，晚于所有 `Awake`/`OnEnable` | **1 次** | `Character.Start()` 恢复血量+位置 —— **故意不用 `OnEnable`**，见下 |

#### 为什么 `Character` 必须用 `Start` 而不是 `OnEnable`

`Character.cs` 里有一段长注释专门讲这件事：

> `SceneLoad` 切场景时会对玩家做 `SetActive(false)` / `SetActive(true)`
> （`SceneLoad.cs:111` 和 `:136`）。**每做一次就会重跑一遍 `OnEnable`。**
> 如果把恢复逻辑写在 `OnEnable`，玩家每走进一个新场景就会
> **被传送回上一个存档点**，而且**血量退回存档值**（中途受的伤白受）。
>
> `Start` 每个实例只跑一次；而玩家挂在 `Persistent` 上、切场景**不会重新实例化**，
> 所以 `Start` 实际上只在游戏启动时跑一次。

#### 但 `Awake` 期间，别的对象可能还没初始化

`DataManager.instance` 之所以在 `Enemy.Awake()` 里就一定可用，是靠**加载顺序**保证的：

- `DataManager` 挂在 `Persistent` 上，而 `Persistent` 是**第一个被加载**的场景
- 它的 `Awake` 早就跑完了

如果哪天要读的是**同场景另一个对象**的状态，`Awake` 就不安全了
（同场景内的 `Awake` 顺序 Unity 不保证），那时候要改用 `Start`。

### 三个写入入口 —— 新加东西照这三个抄

```csharp
// 1. 敌人死亡
Enemy.onDied()             → MarkEnemyDead(id)                 → deadEnemies[id] = true

// 2. 交互物（宝箱 / 存档点）
Chest.OpenChest()          → MarkInteractTriggered(id, true)   → interactHasTriggered[id] = true

// 3. 血量变化（受伤 / 溺水）
Character.RecordHealth()   → MarkCharacterHealth(id, hp)       → characterHealthDictionary[id] = hp
```

三个方法都在 `DataManager` 里，写法一模一样，都带**空 ID 保护**
（ID 为空就跳过，避免所有对象挤在 `""` 这一条记录上）。

### 只写内存，**不立即落盘**

| 操作 | 结果 | 为什么 |
|---|---|---|
| 开箱 → 换场景回来 | **记得** | 记事本在内存里 |
| 开箱 → 碰存档点 → 读档 | **记得** | 已经落盘了 |
| 开箱 → **没存档就死了** → 读档 | **忘记** | ✅ 正确：未保存的进度就该丢 |

**「记录到内存」和「保存到磁盘」是两件事。**
如果 `MarkXxx` 改成"立即落盘"，就变成了随时随地自动存档，和「存档点」这套机制直接冲突。

### 一张图

```
        ┌────────────────────────────────────────────────┐
        │  DataManager   (单例 / 挂在 Persistent 上)      │
        │  ┌──────────────────────────────────────────┐  │
        │  │ saveData : Data          ← 长期存活的记事本 │  │
        │  │   characterPositionDictionary            │  │
        │  │   characterHealthDictionary              │  │
        │  │   interactHasTriggered  ← 宝箱 / 存档点   │  │
        │  │   deadEnemies           ← 野猪           │  │
        │  └──────────────────────────────────────────┘  │
        └───────▲────────────────────────────┬───────────┘
                │  写                         │  查
   ┌────────────┴───────────┐   ┌─────────────▼─────────────┐
   │ 【状态变化的那一刻】    │   │ 【对象被创建的那一刻】     │
   │ Enemy.onDied()         │   │ Enemy.Awake()             │
   │ Chest.OpenChest()      │   │ Chest.OnEnable()          │
   │ Character.TakeDamage() │   │ Character.Start()         │
   └────────────────────────┘   └───────────────────────────┘
```

> 具体的逐帧时间线（野猪死亡 / 宝箱开箱）见本文第四、五节。

---

## 三、四条触发路径

### ① 碰存档点 → 写盘

```
SavePoint.TriggerAction() → Restore()
 ├─ isDone = true; RefreshVisual();
 └─ saveGameEventSO.RaiseEvent()
     └─ DataManager.Save()
         ├─ 遍历 saveableList，逐个 GetSaveData(saveData)   ← 汇总进内存
         └─ File.WriteAllText(SavePath, JsonUtility.ToJson(saveData))   ← 覆盖式写盘
```

`Save()` 每次都是**完整重写**，不是追加。

### ② 切场景（不写盘）

```
SceneLoad.OnSceneLoadRequest()
 ├─ 淡入 → curLoadScene.UnLoadScene()        ← 卸载旧场景
 ├─ playerTrans.SetActive(false)
 └─ Addressables LoadSceneAsync(Additive)    ← 加载新场景
     └─ OnLoadCompelted → 放好玩家位置 → SetActive(true)
```

新场景的对象在 `Character.Start()` 里各自调 `DataManager.LoadFor(this)`，
从**内存里的 `saveData`** 恢复自己。

### ③ 读档

```
loadGameEvent.RaiseEvent()
 ├─ DataManager.Load()          ← 【先跑】因为 [DefaultExecutionOrder(-100)]
 │   ├─ 读盘 → JsonUtility.FromJsonOverwrite 填进 saveData
 │   └─ 通知当前已注册的对象 LoadData
 └─ SceneLoad.LoadData()        ← 【后跑】广播 LoadRequest 加载存档场景
```

**这个先后顺序是刻意依赖的**：必须先把存档读进内存，再加载场景 ——
否则新场景里的对象 `Start()` 时读到的还是旧数据。

### ④ 新游戏 → **清内存 + 删磁盘**

```
Menu「New Game」/ 死亡面板「Restart」
 └─ newGameEventSO.RaiseEvent()
     ├─ DataManager.ResetData()
     │   ├─ saveData = new Data()      ← 清内存
     │   └─ DeleteSaveFile()           ← 【必须】删磁盘
     ├─ SceneLoad.NewGame()            ← 回到 initScene
     ├─ Character.NewGame()            ← 回满血
     └─ PlayerController.PlayerInit()  ← 复活并解锁输入
```

**`DeleteSaveFile()` 这一句是必须的** —— 只清内存的话，「新游戏」只是个**幻觉**：

| 操作 | 只清内存（❌ 错误） | 清内存 + 删盘（✅ 正确） |
|---|---|---|
| 新游戏 → **没存档就死** → 点「回到存档点」 | **被传回上一个游戏的存档点** | 从游戏开头重来 |
| 新游戏 → 回菜单 → 点「继续」 | **读回上一个游戏的进度** | 「继续」等价于新游戏 |

**根源还是同一个母题**：`Load()` 的第一件事是

```csharp
if (!HasSaveFile) { FallbackToNewGame(); return; }   // ← 它信任"磁盘上没有文件"=="没有存档"
```

而「只清内存」**并没有让这句话成立** —— 于是 `Load()` 高高兴兴地把**上一个游戏**的存档读了回来。

> ⚠️ **反过来也成立**：`FallbackToNewGame()` 是靠广播 `newGameEvent` 走到 `ResetData()` 的，
> 所以「没有存档」和「存档损坏读不出来」两条路径也会执行删除 ——
> 前者文件不存在会静默返回；后者会把读不出来的**损坏存档删掉**
> （`Load()` 里本来就声明"按没有存档处理"，删掉它让状态一致）。

---

## 四、敌人死亡持久化

### 背景

野猪被当作 **Boss** 处理：**它死了就不该再被重建**。

行业常规是「只保存 Boss 状态，不管小兵」，我们只有一个野猪，正好适用。

### 原来的 bug

存档系统保存了敌人的**位置**和**血量**，但**没保存「死亡」**：

| 敌人状态 | 修复前 | 修复后 |
|---|---|---|
| 位置 | ✅ 存 | ✅ 存 |
| 血量 | ✅ 存 | ✅ 存 |
| **死亡** | ❌ **不存** | ✅ **存** |

结果就是「受伤的野猪保留了伤，但被打死的野猪满血复活」——**病灶不是"保存了血量"，而是"漏了死亡"**。

机制：野猪死亡时 `DestroyAfterAnimation()`（动画事件）销毁了 GameObject → `Character.OnDisable()` 把它从
`saveableList` 摘掉 → **但存档内存里那条记录没删**，还留着"活着时"的血量 → 场景重载时被当成活的重建出来。

### 实现

**① `Data.cs`** —— 新增 `deadEnemies` 字典 + 影子列表

```csharp
public Dictionary<string, bool> deadEnemies = new Dictionary<string, bool>();

[SerializeField] private List<string> deadKeys = new List<string>();
[SerializeField] private List<bool>   deadValues = new List<bool>();
// OnBeforeSerialize / OnAfterDeserialize 里照 characterPositionDictionary 的写法补齐
```

**② `DataManager.cs`** —— 两个访问器

```csharp
public void MarkEnemyDead(string id) { if (string.IsNullOrEmpty(id)) return; saveData.deadEnemies[id] = true; }
public bool IsEnemyDead(string id)   { if (string.IsNullOrEmpty(id)) return false; return saveData.deadEnemies.TryGetValue(id, out bool d) && d; }
```

**③ `Enemy.onDied()`** —— 死亡时登记

```csharp
var dif = GetComponent<DataDifination>();
if (DataManager.instance != null && dif != null)
    DataManager.instance.MarkEnemyDead(dif.ID);
```

**④ `Enemy.Awake()`** —— 场景加载时检查

```csharp
var dif = GetComponent<DataDifination>();
if (DataManager.instance != null && dif != null && DataManager.instance.IsEnemyDead(dif.ID))
    Destroy(gameObject);
```

### 为什么检查放在 `Awake()` 而不是 `Start()`

**避免"闪一帧"** —— 对象一被实例化就销毁，不会先渲染出来再消失。

两条路径下 `saveData` 在场景实例化时都已就绪：

- 普通切场景：`saveData` 一直在内存里
- 读档：`DataManager` 标了 `[DefaultExecutionOrder(-100)]`，它的 `Load()` 会在
  `SceneLoad.LoadData()` 触发场景加载**之前**跑完

### 语义边界（刻意的）

`MarkEnemyDead()` 只写**内存**，落盘要等下一次 `Save()`（碰存档点）。这与存档系统整体语义一致：

| 操作 | 结果 |
|---|---|
| 打死野猪 → 换场景回来 | **不复活**（内存里记着） |
| 打死野猪 → 碰存档点 → 读档 | **不复活**（落盘了） |
| 打死野猪 → **没存档就死了** → 读档 | **复活**（存档里没这条记录）—— 正确 |

---

## 五、宝箱开启状态持久化

### 原来的 bug

`Chest` 只实现了 `IInteractable`，**漏了 `ISaveable`** —— 所以 `isDone` 根本不进存档系统。

```csharp
public class Chest : MonoBehaviour, IInteractable      // ← 漏了 ISaveable
```

症状：开箱 → 离开场景 → 回来，**宝箱重新关上了**（还能再开一次）。

而 `Data.interactHasTriggered` 这个字典**早就存在**（`SavePoint` 在用），宝箱只是没接进去。

### 实现（两处都改，缺一不可）

#### ① 实现 `ISaveable` —— 让它能被「读」

照 `SavePoint` 的模式补齐三个接口方法 + 注册/反注册：

```csharp
public class Chest : MonoBehaviour, IInteractable, ISaveable
{
    private void OnEnable()
    {
        if (GetDataID() == null)
        {
            Debug.LogWarning($"[{name}] 宝箱缺少 DataDifination 组件，开启状态不会被保存", this);
            RefreshVisual();
            return;
        }
        DataManager.instance.RegisterSaveData(this);
        DataManager.instance.LoadFor(this);      // 场景重载后拉回自己的状态
    }

    private void OnDisable() { if (GetDataID() != null) DataManager.instance.UnRegisterSaveData(this); }

    public DataDifination GetDataID() { return GetComponent<DataDifination>(); }

    public void GetSaveData(Data data)
    {
        string id = GetDataID().ID;
        if (data.interactHasTriggered.ContainsKey(id)) data.interactHasTriggered[id] = isDone;
        else data.interactHasTriggered.Add(id, isDone);
    }

    public void LoadData(Data data)
    {
        bool saved;
        if (data.interactHasTriggered.TryGetValue(GetDataID().ID, out saved)) isDone = saved;
        RefreshVisual();      // 把 isDone 刷到 sprite 上
    }
}
```

顺带把「设 sprite」抽成了 `RefreshVisual()`，`OnEnable` / `OpenChest` / `LoadData` 三处共用。

#### ② 开箱时**立即**写入内存存档 —— 这才是关键

**只做 ① 是不够的**，这一点是实际排查出来的：

> `GetSaveData()` **只在 `Save()` 里被调用**，而 `Save()` 只遍历 `saveableList` ——
> 也就是**当前已加载**的对象。

而本项目 **存档点在 Cave、宝箱在 Forest**：

```
在 Forest 开箱 → isDone = true（只改在组件上）
  → 没有任何人写 saveData
    → 走到 Cave（Forest 卸载，宝箱被销毁）
      → 回 Forest → 新宝箱实例 → LoadData 查不到记录 → 保持关闭
```

**而且即使完全不存档、只是 Cave↔Forest 来回切，宝箱一样会关上** ——
因为 `LoadData` 读的是**内存里的 `saveData`**，而那个值从来没被写进去过。

所以 `OpenChest()` 里必须立即记录：

```csharp
private void OpenChest()
{
    isDone = true;
    RefreshVisual();
    DataManager.instance?.MarkInteractTriggered(GetDataID().ID, true);   // ← 关键
    GetComponent<AudioDefination>()?.PlayAudioClip();
}
```

### 📐 由此得出的通用原则：离散事件 vs 连续状态

`saveData` 是**存档点快照**，不是**实时状态库**。所以记录时机必须分开：

| 类型 | 例子 | 记录时机 |
|---|---|---|
| **连续状态** | 玩家/敌人**位置** | `Save()` 遍历快照（可接受：位置是瞬态值） |
| **变化即需保留的状态** | 血量、开箱、打死 Boss、激活存档点 | **变化的那一刻**就写进内存 `saveData` |

> **野猪死亡之所以没这个 bug，就是因为它一开始就用的是"立即记录"**：
> `Enemy.onDied()` → `DataManager.MarkEnemyDead()`。
> 宝箱是后来才补的，**漏了这一步**，只实现了 `GetSaveData`。
>
> **给任何新的「离散事件类」对象接存档时，两处都要做** —— 只做 `GetSaveData` 是个陷阱，
> 因为它在同场景下**看起来能工作**（存档点遍历时正好也在场），只在跨场景时才暴露。

`SavePoint` 本身就是那个"看起来能工作"的例子：它的 `Restore()` 会**自己触发 `Save()`**，
而那一刻它肯定在 `saveableList` 里，所以只靠 `GetSaveData` 就够了 —— 这是特例，不要照抄。

#### ✅ 敌人「打伤」原本不会被记住 —— 已对齐修复

按上表，敌人的血量属于**连续状态**，原本只在 `Save()` 时快照，于是出现不一致：

| 操作 | 修复前 | 修复后 |
|---|---|---|
| 把野猪**打死** → 换场景 | **记得**（`MarkEnemyDead` 立即记录） | 记得 |
| 把野猪**打伤**（没打死）→ 不存档就换场景 | **回满血**（血量只在存档点快照） | **保留残血** |

根源还是同一个：`Character.GetSaveData()` 只在 `Save()` 里被调用 ——
**前提是玩家真的去碰了存档点**。没碰过存档点就换场景，伤害就丢了。

**修复**：在 `Character` 里抽出 `RecordHealth()`，受伤和溺水时立即写内存：

```csharp
public void TakeDamage(Attack attacker)
{
    ...
    curHealth -= attacker.damage;
    ...
    RecordHealth();                     // ← 立刻记进内存存档
    onHealthChanged?.Invoke(this);
}

private void RecordHealth()
{
    if (DataManager.instance == null) return;
    var dif = GetDataID();
    if (dif == null) return;            // 没挂 DataDifination 就不参与存档
    DataManager.instance.MarkCharacterHealth(dif.ID, curHealth);
}
```

> **位置仍然走存档点快照**，没有改成每帧写入 —— 位置是高频变化的瞬态值，
> 且敌人回场景时出现在"上次存档的位置"是可接受的。如果以后需要，再单独对齐。

> **玩家不受影响**：玩家挂在 `Persistent` 上，切场景时不会被重新实例化，
> 所以 `Character.Start()` 不会重跑、血量不会被重置。
> 这也是 `Character.cs` 里那段「为什么是 `Start` 而不是 `OnEnable`」注释在防的事。

### ⚠️ 前置条件：宝箱**必须**挂 `DataDifination`，且 `ID` 非空

**这是实际踩到的坑**：`Forset.unity` 里那个宝箱**原本没有 `DataDifination` 组件**
（只有 `Transform` / `SpriteRenderer` / `CircleCollider2D` / `Chest.cs` / `AudioDefination.cs`）。

没有它 → `GetDataID()` 返回 `null` → `.ID` 直接 **NullReferenceException**。

所以本次一并给 `Forset` 的宝箱补了组件（`presistentType = ReadAndWrite`，生成了唯一 ID）。
代码里也加了 `null` 检查和明确的 `LogWarning` —— **不能静默**，
否则症状是"开箱后切场景回来又关上了"，而没有任何线索指向配置问题。

> 同理：`ID` 为空串的话，所有宝箱会共用 `""` 这一条记录。见坑 3。

---

## 六、🔴 四个必须知道的坑

### 坑 1：`JsonUtility` 完全不支持 `Dictionary`

它对字典字段会**静默跳过** —— **不报错、也不写进 JSON**。

```csharp
// 这样写，位置/血量/交互状态会【全部丢失】，而且读回来也不报错
public Dictionary<string, Vector3> characterPositionDictionary = ...;
```

所以 `Data.cs` 里每个字典都配了**影子列表**：序列化前摊平成 `List`，反序列化后装回字典。
**新增任何字典都必须照做**，否则你会得到一个「存档看起来正常、但字段全空」且**没有任何报错**的 bug。

> 验证方法：直接把 `Data` 走一遍 `ToJson` → `FromJsonOverwrite`，看条目数对不对。
> 本次修复就是靠这个验证的（`deadKeys`/`deadValues` 确实出现在 JSON 里）。

### 坑 2：场景必须 `Ctrl+S` 保存，否则 Inspector 改动进不去运行时

本项目的场景是 **Addressables 加载**的（`InitLoad` → `Addressables.LoadSceneAsync("Persistent")`），
按 Play 时读的是**磁盘上已保存的场景**。

而且 —— **给脚本新增序列化字段时，Unity 不会自动写进已有的场景实例**。
字段在场景里缺失时，反序列化一律回落**脚本默认值**。

> **真实踩过**：`wallSlideSpeed` 被改成 `0` 却毫无效果，因为 `Persistent.unity` 里当时连这个字段都不存在。
> **自检**：`git diff Assets/Scenes/Persistent.unity` —— 看不到你新加的字段就是没保存。

### 坑 3：`DataDifination.ID` 必须唯一（`OnValidate` 挡不住重复）

```csharp
// DataDifination.cs:10-23
private void OnValidate()
{
    if (presistentType == PresistentType.ReadAndWrite)
        if (ID == string.Empty)                    // ← 只判空，不判重
            ID = System.Guid.NewGuid().ToString();
    else
        ID = string.Empty;
}
```

**从 prefab 拖出来的实例直接继承了 prefab 里那个非空 ID**，永远不会生成新的。

后果：多个实例写进同一条存档记录 → 读档后**全部被传送到同一位置、共用同一血量**。

- 目前 `Cave.unity` 只有 **1 只**野猪，所以没暴露
- 以后从同一个 prefab 再加敌人时**必须处理**（见 issue #5）

### 坑 4：`if` 查一个字典、`else` 写两个字典 —— 字典一旦不同步就撞键

原来的 `Character.GetSaveData()`：

```csharp
if (data.characterPositionDictionary.ContainsKey(id))   // ← 只查 position
{
    data.characterPositionDictionary[id] = transform.position;
    data.characterHealthDictionary[id]   = curHealth;
}
else
{
    data.characterPositionDictionary.Add(id, transform.position);
    data.characterHealthDictionary.Add(id, curHealth);   // ← health 里可能已经有这条 → 💥
}
```

**它隐含假设「两个字典永远同步」**。这个假设原本成立（两者只在 `GetSaveData` 里一起写），
但一旦有别的入口单独写其中一个（比如 `MarkCharacterHealth()` 只写血量），就会炸：

```
打伤野猪 → characterHealthDictionary[boarID] = 30   （position 里没有）
  → 存档点按 E → Save() → 野猪的 GetSaveData
    → position 里没有 key  → 走 else
      → health.Add(同一个 key) → ArgumentException
```

**修法**：用**索引器**（存在则覆盖、不存在则新增），根本不需要分支：

```csharp
data.characterPositionDictionary[id] = transform.position;
data.characterHealthDictionary[id]   = curHealth;
```

#### 连带后果：整个存档**完全没写盘**

这个 bug 的真正杀伤面不在报错本身。看 `Save()` 的结构：

```csharp
foreach (var saveable in saveableList)
    saveable.GetSaveData(saveData);        // ← 循环在 try 【之外】

try { File.WriteAllText(SavePath, ...); }  // ← 上面一抛，这里根本不会执行
catch (Exception e) { ... }
```

异常一抛 → 后面的对象不再保存 → **`File.WriteAllText` 不执行 → 存档文件没被更新**。
玩家只看到控制台一行报错，还以为"存过了"。

**已顺带加固**：循环里逐个 `try/catch`，单个对象出错不再拖垮整次存档。

> **教训**：多个字典用同一个条件判断时，必须保证它们**永远同生共死**；
> 只要引入一个"只写其中一个"的入口，这个写法就从"能工作"变成"定时炸弹"。

---

## 七、相关 issue

| # | 标题 | 状态 |
|---|---|---|
| [#3](https://github.com/niminimin/2DAdventure/issues/3) | 场景切换返回时 BGM 会从头重播（**不属于存档系统**，见 `AudioManager.OnBGMEvent`） | Open（待验证关闭） |
| [#4](https://github.com/niminimin/2DAdventure/issues/4) | 敌人死亡未持久化（本次修复） | Open（待验证关闭） |
| [#5](https://github.com/niminimin/2DAdventure/issues/5) | 所有野猪共用同一个 `DataDifination.ID` | Closed（留作未来注意事项） |
| [#6](https://github.com/niminimin/2DAdventure/issues/6) | 宝箱开启状态未持久化（本次修复） | Open（待验证关闭） |

---

## 八、验收记录

| 项 | 结果 |
|---|---|
| 编译 | 0 错误 0 警告 |
| `Data` 序列化往返（敌人死亡） | `deadKeys:["boar-A","boar-B"]` / `deadValues:[true,true]` 正确写入并读回 ✅ |
| `Data` 序列化往返（宝箱） | `interactKeys:["1f8d9bd5-…"]` / `interactValues:[true]` 正确写入并读回 ✅ |
| 影子列表 | 未被 `JsonUtility` 静默丢弃 ✅ |
| `Chest` 实现 `ISaveable` | `IsAssignableFrom` = True，三个方法齐全 ✅ |
| `DataDifination.ID` 唯一性 | 全场景 + prefab 扫描，**全部唯一** ✅ |
| 场景改动 | `Forset.unity`（给宝箱补 `DataDifination`）；`Persistent.unity` 无需改动 |
