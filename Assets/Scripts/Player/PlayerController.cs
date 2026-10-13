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
    [Header("State")]
    public bool isWounded;
    public bool isDead;
    public bool isAttack;

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
        newGameEvent.OnEventRaised += PlayerInit;
        loadGameEvent.OnEventRaised += PlayerInit;
        backToMenuEvent.OnEventRaised += PlayerInitInMenu;
    }

    private void OnDisable()
    {
        inputControl.Disable();
        sceneLoadEvent.LoadRequestEvent -= OnLoadEvent;
        afterSceneLoadEvent.OnEventRaised -= OnAfterSceneLoadEvent;
        newGameEvent.OnEventRaised -= PlayerInit;
        loadGameEvent.OnEventRaised -= PlayerInit;
        backToMenuEvent.OnEventRaised -= PlayerInitInMenu;
    }


    private void Update()
    {
        inputDirection = inputControl.GamePlay.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if(!isWounded)
            move();
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
            
    }


    private void PlayerAttack(InputAction.CallbackContext context)
    {
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

    public void PlayerInit()
    {
        isDead = false;
        isWounded = false;      // 不清掉的话 FixedUpdate 里 move() 永远被 if(!isWounded) 锁住
        inputControl.GamePlay.Enable();

        // 回满血并广播给 UI。不做这一步, 复活后 curHealth 还是 0:
        // 血条停在空、下一帧再挨一下就直接又死。
        Character character = GetComponent<Character>();
        character.NewGame();            // 内部会把 isInvulnerable 清掉, 所以必须在下一行之前
        character.TrigInvulnerable();   // 复活后短暂无敌，防止刚站起来又被秒
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
