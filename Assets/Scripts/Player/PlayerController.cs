using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("事件监听")]
    public SceneLoadEventSO sceneLoadEvent;
    public VoidEventSO afterSceneLoadEvent;
    public VoidEventSO newGameEvent;
    public VoidEventSO loadGameEvent;
    public VoidEventSO backToMenuEvent;

    public PlayerInputCtrl inputControl;
    private Rigidbody2D rb;
    private PhysicsCheck phyCheck;
    public Vector2 inputDirection;
    [Header("基本参数")]
    public float speed;
    public float jumpForce;
    public float woundForce;
    [Tooltip("垂直弹起力度。要能爬出你设计的坑深: v = sqrt(2 * g * 坑深), g = 9.81 * gravityScale")]
    public float bounceUpForce = 20;
    [Header("扒墙 / 蹬墙跳")]
    [Tooltip("贴墙时的下滑速度。调成 0 就变成完全扒住不动")]
    public float wallSlideSpeed = 2f;
    [Tooltip("蹬墙跳的垂直初速。建议与 jumpForce 保持一致，这样蹬墙跳和普通跳一样高")]
    public float wallJumpYForce = 17f;
    [Tooltip("蹬墙跳的横向初速。要明显大于 speed，弹开感才够")]
    public float wallJumpXForce = 8f;
    [Tooltip("蹬墙后锁定横向输入的时间。太短的话 move() 会立刻把横向速度抹掉，人会贴回墙上")]
    public float wallJumpLockTime = 0.2f;

    [Header("State")]
    public bool isWounded;
    public bool isDead;
    public bool isAttack;
    /// <summary>当前是否正在扒墙。PlayerAnimation 读它来切动画。</summary>
    public bool isWallSliding;

    private float wallSlideDir;        // -1 = 墙在左, +1 = 墙在右
    private float wallJumpLockTimer;   // 蹬墙后横向输入锁定的剩余时间

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputControl = new PlayerInputCtrl();
        phyCheck = GetComponent<PhysicsCheck>();
        inputControl.GamePlay.Jump.started += Jump;
        inputControl.GamePlay.Attack.started += PlayerAttack;
    }



    private void OnEnable()
    {
        // 这里【绝对不能】inputControl.Enable()。
        //
        // SceneLoad 切场景时会 playerTrans.SetActive(false) 再 SetActive(true)
        // (SceneLoad.cs:111 和 :136), 每次重新激活都会跑一遍 OnEnable。
        // 无条件 Enable 会把菜单场景里刚关掉的 GamePlay 又打开 ——
        // 症状就是"死亡后返回菜单, 人物还能动", 而直接启动游戏进菜单却没事
        // (因为启动时 curLoadScene == null, 直接 LoadNewScene, 没有 SetActive 循环)。
        //
        // 输入的开/关统一由"目标场景是不是 Location"决定, 见下面两个回调:
        //   OnLoadEvent           任何加载请求 -> 先关
        //   OnAfterSceneLoadEvent 只有 Location 场景加载完成才开
        sceneLoadEvent.LoadRequestEvent += OnLoadEvent;
        afterSceneLoadEvent.OnEventRaised += OnAfterSceneLoadEvent;
        // 这三条路径的语义是【不一样】的, 不能共用一个处理函数:
        //   新游戏 / 回菜单 -> 复位状态 + 回满血
        //   读档            -> 只复位状态, 血量必须保留存档里的值
        newGameEvent.OnEventRaised += PlayerInit;           // 回满血
        loadGameEvent.OnEventRaised += PlayerRevive;        // 不碰血量
        backToMenuEvent.OnEventRaised += PlayerInitInMenu;  // 回满血
    }

    private void OnDisable()
    {
        inputControl.Disable();
        sceneLoadEvent.LoadRequestEvent -= OnLoadEvent;
        afterSceneLoadEvent.OnEventRaised -= OnAfterSceneLoadEvent;
        newGameEvent.OnEventRaised -= PlayerInit;
        loadGameEvent.OnEventRaised -= PlayerRevive;
        backToMenuEvent.OnEventRaised -= PlayerInitInMenu;
    }


    private void Update()
    {
        inputDirection = inputControl.GamePlay.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        UpdateWallSlide();

        if (isWounded) return;

        // 蹬墙后的锁定期内【绝对不能】调 move()：
        // move() 每帧无条件把 velocity.x 写成 inputDirection.x * speed，
        // 而蹬墙瞬间玩家往往是没按方向键的 —— 横向冲量会在下一帧被抹成 0，
        // 表现就是「蹬了墙却原地起跳，然后贴回同一面墙」。
        if (wallJumpLockTimer > 0f)
        {
            wallJumpLockTimer -= Time.fixedDeltaTime;
            return;
        }

        if (isWallSliding)
        {
            // 横向照常给 speed（被墙挡住 = 贴住），纵向锁定为缓慢下滑
            rb.velocity = new Vector2(inputDirection.x * speed, -wallSlideSpeed);
        }
        else
        {
            move();
        }
    }

    /// <summary>
    /// 判定当前是否处于「扒墙」状态。五个条件必须同时满足。
    /// 取舍理由（为什么上升中不扒、为什么必须按住方向）见 docs/wall-slide-jump.md
    /// </summary>
    private void UpdateWallSlide()
    {
        isWallSliding = false;

        // 1. 落地 / 受伤 / 死亡 → 立刻脱离
        if (phyCheck.isGround || isWounded || isDead) return;

        // 2. 墙在左还是右。
        //    PhysicsCheck 的左右探测是【世界方向】（不随 localScale 翻转），
        //    所以这里拿到的是"世界左边有墙"，而不是"人物面朝方向的左边有墙"。
        float dir = phyCheck.touchLeftWall  ? -1f
                  : phyCheck.touchRightWall ?  1f
                  : 0f;
        if (dir == 0f) return;

        // 3. 只在【下落中】扒。上升途中扒住会把跳跃硬生生截停，手感很怪
        if (rb.velocity.y > 0.01f) return;

        // 4. 必须【按住朝墙方向】。否则跑图时蹭到墙边会被莫名其妙地吸住
        if (inputDirection.x * dir <= 0.01f) return;

        isWallSliding = true;
        wallSlideDir  = dir;
    }

    private void move()
    {
        rb.velocity = new Vector2(inputDirection.x * speed, rb.velocity.y);
        int curDir = (int)transform.localScale.x;
        if(inputDirection.x > 0)
        {
            curDir = 1;
        }
        if(inputDirection.x < 0)
        {
            curDir = -1;
        }
        transform.localScale = new Vector3(curDir, 1, 1);
    }
    private void Jump(InputAction.CallbackContext context)
    {
        //Debug.Log("jump");
        if (phyCheck.isGround)
        {
            rb.AddForce(transform.up * jumpForce, ForceMode2D.Impulse);
            GetComponent<AudioDefination>()?.PlayAudioClip();
        }
        else if (isWallSliding)
        {
            WallJump();
        }
    }

    /// <summary>
    /// 蹬墙跳。
    ///
    /// 这里【直接赋值 velocity】而不是像普通跳那样 AddForce：
    /// AddForce 是叠加在现有速度上的，而扒墙时 vy 恒为 -wallSlideSpeed，
    /// 用 AddForce 的话蹬墙高度会随下滑速度漂移。直接赋值才能保证每次一样高。
    /// </summary>
    private void WallJump()
    {
        isWallSliding     = false;
        wallJumpLockTimer = wallJumpLockTime;

        float away = -wallSlideDir;                        // 背离墙的方向
        rb.velocity = new Vector2(away * wallJumpXForce, wallJumpYForce);
        transform.localScale = new Vector3(away, 1, 1);    // 人物转向背对墙

        GetComponent<AudioDefination>()?.PlayAudioClip();
    }


    private void PlayerAttack(InputAction.CallbackContext context)
    {
        // 扒墙时禁止攻击 —— 手抓着墙腾不出手。
        //
        // 不拦的话会出问题：Attack Layer 是 weight=1 / Override / 【没有 Avatar Mask】的
        // 全身层，一进攻击状态就把 Base Layer 的 BlueWallSlide 整个盖掉（2D 纯 Sprite 动画
        // 没法用 Avatar Mask 只遮上半身，Mask 只对骨骼/Transform 层级有效，管不了 m_Sprite 曲线）。
        // 更糟的是 UpdateWallSlide() 的守卫里没有 isAttack，物理上仍在扒墙下滑，
        // 于是变成"一边挥刀一边贴墙下滑"，动画和物理对不上。
        //
        // Animator 的 Attack Layer 里还有一道 isWallSlide=IfNot 的转场保险，
        // 但【不能只靠它】：那样 isAttack 永远不会被 AttackBehaviour.OnStateExit 复位。
        if (isWallSliding) return;

        GetComponent<PlayerAnimation>().PlayerAttack();
        isAttack = true;
    }

    #region UnityEvent
    public void GetWounded(Attack attacker)
    {
        isWounded = true;

        Vector2 v = rb.velocity;             // 必须在清零之前读

        // 水平：远离攻击者身上离你最近的那个点
        Vector2 cp = attacker.ClosestPointTo(transform.position);
        float delta = transform.position.x - cp.x;

        // delta == 0 表示你正站在它上面/里面（ClosestPoint 在内部会原样返回你的位置）
        bool standingOnIt = Mathf.Abs(delta) < 0.01f;

        float hx = standingOnIt
            ? -Mathf.Sign(transform.localScale.x)   // 正上方 → 朝背后推
            : Mathf.Sign(delta);

        // 垂直：站在地刺上、或正在下落时，向上弹
        // 这是爬出地刺坑的唯一手段（Jump() 要求 isGround，空中不能二段跳）
        // 在地面上跑着撞竖刺柱时 v.y ≈ 0 且 delta != 0 → vy = 0，柱子不会被弹飞
        float vy = (standingOnIt || v.y < -0.5f) ? 1f : 0f;

        // 只有真要把你往上弹时才清掉 y：否则下落速度会抵消向上的冲量
        // （下落 -15.3 再加 +20 只剩 +4.7，弹起高度从 5.1 格缩到 0.28 格）
        // 其余情况保留 y，避免「跳起来蹭到刺就把跳跃也取消了」
        rb.velocity = new Vector2(0, vy > 0f ? 0f : v.y);

        rb.AddForce(new Vector2(hx * woundForce, vy * bounceUpForce), ForceMode2D.Impulse);
    }

    public void GetDied()
    {
        isDead = true;
        inputControl.GamePlay.Disable();
    }

    /// <summary>新游戏 / 回菜单: 复位状态 + 回满血。</summary>
    public void PlayerInit()
    {
        PlayerRevive();

        // 回满血并广播给 UI。不做这一步, 复活后 curHealth 还是 0:
        // 血条停在空、下一帧再挨一下就直接又死。
        Character character = GetComponent<Character>();
        character.NewGame();            // 内部会把 isInvulnerable 清掉, 所以必须在下一行之前
        character.TrigInvulnerable();   // 复活后短暂无敌，防止刚站起来又被秒
    }

    /// <summary>
    /// 【读档专用】只复位状态开关, 绝不动血量。
    ///
    /// 症状: 读档后 curHealth 永远是 maxHealth(100), 存进去的 90 读不出来。
    /// 原因: 原来 loadGameEvent 直接连着 PlayerInit, 而 PlayerInit 里的
    ///       character.NewGame() 会把 curHealth 直接写成 maxHealth ——
    ///       Character.LoadData 刚恢复的 90 立刻被覆盖掉。
    ///
    /// 这里有一条【隐式执行顺序】依赖, 改动前请先看懂:
    ///   DataManager 标了 [DefaultExecutionOrder(-100)], 它的 OnEnable 先跑,
    ///   所以同一个 LoadGameEventSO 的委托链上 DataManager.Load 排在
    ///   本函数前面 —— 必须先 LoadData 恢复, 再进来复位状态。
    /// </summary>
    private void PlayerRevive()
    {
        isDead = false;
        isWounded = false;      // 不清掉的话 FixedUpdate 里 move() 永远被 if(!isWounded) 锁住
        // 复位扒墙状态：否则在墙边死亡/读档后，残留的锁定期会把横向输入一直吃掉
        isWallSliding     = false;
        wallJumpLockTimer = 0f;
        inputControl.GamePlay.Enable();

        // 只补无敌帧, 不碰 curHealth
        GetComponent<Character>().TrigInvulnerable();
    }

    private void PlayerInitInMenu()
    {
        PlayerInit();
        inputControl.GamePlay.Disable();
    }

    /// <summary>
    /// 场景加载事件触发
    /// 加载过程中人物控制失效
    /// </summary>
    /// <param name="arg0"></param>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnLoadEvent(GameSceneSO arg0, Vector3 arg1, bool arg2)
    {
        inputControl.GamePlay.Disable();
    }

    /// <summary>
    /// 加载场景完成后事件触发
    /// 人物控制生效
    /// </summary>
    private void OnAfterSceneLoadEvent()
    {
        inputControl.GamePlay.Enable();
    }

    #endregion


}
