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
        playerInput.Enable();
    }
    private void OnEnable()
    {
        playerInput.GamePlay.Confirm.started += OnConfirm;
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!canPress) return;

        targetItem.TriggerAction();

        GetComponent<AudioDefination>()?.PlayAudioClip();

        
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
