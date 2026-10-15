# 存档系统（Save / Load）

> 记录存档系统的架构、三条触发路径、以及踩过的坑。
> 本文由「修复敌人死亡未持久化」这个 bug 的排查过程整理而来。

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

## 谁实现了 ISaveable

| 对象 | 存什么 |
|---|---|
| `Character`（玩家 / 敌人） | 位置 + 血量 |
| `SavePoint` | `interactHasTriggered[ID] = isDone` |
| `Chest` | `interactHasTriggered[ID] = isDone` |
| `SceneLoad` | 当前场景（`GameObject` 序列化成 JSON）+ 玩家位置 |

---

## 二、三条触发路径

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

---

## 三、敌人死亡持久化（本次新增）

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

## 四、宝箱开启状态持久化

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

## 五、🔴 三个必须知道的坑

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

---

## 六、相关 issue

| # | 标题 | 状态 |
|---|---|---|
| [#3](https://github.com/niminimin/2DAdventure/issues/3) | 场景切换返回时 BGM 会从头重播（**不属于存档系统**，见 `AudioManager.OnBGMEvent`） | Open（待验证关闭） |
| [#4](https://github.com/niminimin/2DAdventure/issues/4) | 敌人死亡未持久化（本次修复） | Open（待验证关闭） |
| [#5](https://github.com/niminimin/2DAdventure/issues/5) | 所有野猪共用同一个 `DataDifination.ID` | Closed（留作未来注意事项） |
| [#6](https://github.com/niminimin/2DAdventure/issues/6) | 宝箱开启状态未持久化（本次修复） | Open（待验证关闭） |

---

## 七、验收记录

| 项 | 结果 |
|---|---|
| 编译 | 0 错误 0 警告 |
| `Data` 序列化往返（敌人死亡） | `deadKeys:["boar-A","boar-B"]` / `deadValues:[true,true]` 正确写入并读回 ✅ |
| `Data` 序列化往返（宝箱） | `interactKeys:["1f8d9bd5-…"]` / `interactValues:[true]` 正确写入并读回 ✅ |
| 影子列表 | 未被 `JsonUtility` 静默丢弃 ✅ |
| `Chest` 实现 `ISaveable` | `IsAssignableFrom` = True，三个方法齐全 ✅ |
| `DataDifination.ID` 唯一性 | 全场景 + prefab 扫描，**全部唯一** ✅ |
| 场景改动 | `Forset.unity`（给宝箱补 `DataDifination`）；`Persistent.unity` 无需改动 |
