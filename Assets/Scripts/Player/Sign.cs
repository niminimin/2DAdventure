using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Sign : MonoBehaviour
{
    private PlayerInputCtrl playerInput;
    public GameObject signSprite;
    private bool canPress;
    private Animator animator;
    public Transform playerTrans;
    private IInteractable targetItem;

    private void Awake()
    {
        animator = signSprite.GetComponent<Animator>();
        playerInput = new PlayerInputCtrl();
        // 这里【只创建, 不 Enable】——
        // Enable/Disable 必须成对放在 OnEnable/OnDisable 里。
        // 如果 Awake 里 Enable、OnDisable 里 Disable, 就是不对称的:
        // Sign 挂在 Player 层级下, SceneLoad 切场景时会 SetActive(false)/SetActive(true),
        // OnDisable 关掉之后 OnEnable 不会再打开 -> 进关卡后 E 键彻底失效。
    }

    private void OnEnable()
    {
        playerInput.Enable();
        playerInput.GamePlay.Confirm.started += OnConfirm;
    }

    private void OnDisable()
    {
        // 必须和 OnEnable 配对退订。Sign 挂在 Player 层级下, 而 SceneLoad 切场景时会
        // playerTrans.SetActive(false) / (true) —— 每次都会跑一遍 OnDisable/OnEnable。
        // 不退订的话每切一次场景就多一条订阅, 之后按一次 E 会调 OnConfirm 多次。
        playerInput.GamePlay.Confirm.started -= OnConfirm;

        // 和上面 OnEnable 里的 Enable() 配对, 否则对象被回收时
        // Input System 会报 "GamePlay.Disable() has not been called" 的泄漏断言。
        playerInput.Disable();
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!canPress) return;

        targetItem.TriggerAction();

        // 音效不在这里统一播 —— 交给被交互物自己：挂 AudioDefination 就响，不挂就静音。
        // 放在这里播的话，宝箱和传送门只能共用一个音，换场景也会带上开箱音。

        
        // 传送门则可能已经把这个场景整个换掉了。
        // 先做 Unity 的"假 null"判定，避免在已被销毁的组件上访问 CanInteract。
        if ((targetItem as UnityEngine.Object) == null)
        {
            canPress = false;
            targetItem = null;
            return;
        }
        // 立刻重判：宝箱可能刚被打开（图标要马上收起）
        canPress = targetItem.CanInteract;
        if (!canPress) targetItem = null;   
    }

    private void Update()
    {
        // 兜底：目标被销毁或禁用了 —— Unity 不会为此发 OnTriggerExit2D，
        // 不查的话 canPress 会永远卡在 true，图标收不起来。
        // 注意这里要转成 UnityEngine.Object 才走 Unity 的"假 null"判定。
        if (canPress && (targetItem as UnityEngine.Object) == null)
        {
            canPress = false;
            targetItem = null;
        }

        signSprite.SetActive(canPress);
        signSprite.transform.localScale = playerTrans.localScale;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.CompareTag("Interactable")) return;

        IInteractable item = collision.GetComponent<IInteractable>();
        if (item == null || !item.CanInteract) return;   // 已打开的箱子当作不存在

        targetItem = item;
        canPress = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // 比较「离开的是不是我当前的目标」，不要在这里判 tag ——
        // tag 可能被别处改掉，那样这行守卫会把本该执行的清空一起挡掉
        if (collision.GetComponent<IInteractable>() == targetItem)
        {
            canPress = false;
            targetItem = null;
        }
    }
}
