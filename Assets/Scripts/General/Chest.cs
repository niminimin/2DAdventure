using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    private SpriteRenderer spriteRenderer;
    public Sprite openSprite;
    public Sprite closeSprite;
    public bool isDone;

    // 「已打开」这个状态用 CanInteract 表达，不要去改 tag。
    // tag 是 Sign 用来"发现"物体的依据，改掉它会让 Sign.OnTriggerExit2D 里的
    // CompareTag 一起失效 —— 症状就是图标在离开宝箱后收不起来。
    public bool CanInteract { get { return !isDone; } }

    public void TriggerAction()
    {
        //Debug.Log("open chest");
        if (!isDone)
        {
            OpenChest();
        }
    }
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void OnEnable()
    {
        spriteRenderer.sprite = isDone ? openSprite : closeSprite;
    }
    private void OpenChest()
    {
        spriteRenderer.sprite = openSprite;
        isDone = true;
    }
}
