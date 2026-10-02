using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
{
    [Header("参数")]
    public float checkRadius;
    public Vector2 bottomOffset;
    public Vector2 leftOffset;
    public Vector2 rightOffset;

    [Header("边缘探测")]
    public float aheadDistance;      // 探测点从脚底中心往前伸多远

    public LayerMask groundLayer;

    [Header("状态")]
    public bool isGround;
    public bool touchLeftWall;
    public bool touchRightWall;
    [HideInInspector] public bool touchLedge;

    /// <summary>
    /// 朝前镜像后的脚底探测点偏移。
    /// x 随 localScale.x 翻转（transform.right 不受缩放影响，所以只能这样取朝向），
    /// 这样 bottomOffset 在 Inspector 里恒为「朝前的正距离」，无需手动维护正负。
    /// </summary>
    private Vector2 BottomOffsetFacing
    {
        get
        {
            float facing = -Mathf.Sign(transform.localScale.x);   // 与 Enemy.faceDir 同一算法
            return new Vector2(bottomOffset.x * facing, bottomOffset.y);
        }
    }

    /// <summary>
    /// 前方边缘探测点：脚底中心再沿朝向前移 aheadDistance。
    /// </summary>
    private Vector2 LedgeOffsetFacing
    {
        get
        {
            float facing = -Mathf.Sign(transform.localScale.x);
            return new Vector2(
                (bottomOffset.x + aheadDistance) * facing,
                bottomOffset.y);
        }
    }

    void Update()
    {
        check();
    }

    public void check()
    {
        Vector2 pos = transform.position;

        // 地面判断（脚底）
        isGround = Physics2D.OverlapCircle(pos + BottomOffsetFacing, checkRadius, groundLayer);

        // 墙体判断（世界方向，不随朝向翻转）
        touchLeftWall = Physics2D.OverlapCircle(pos + leftOffset, checkRadius, groundLayer);
        touchRightWall = Physics2D.OverlapCircle(pos + rightOffset, checkRadius, groundLayer);

        // 边缘判断：只在地面上才有意义；脱地时恒为 false，避免「掉进坑里还一直等」
        touchLedge = false;
        if (isGround)
        {
            touchLedge = !Physics2D.OverlapCircle(pos + LedgeOffsetFacing, checkRadius, groundLayer);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 pos = transform.position;

        Gizmos.color = Color.green;      // 脚底
        Gizmos.DrawWireSphere(pos + BottomOffsetFacing, checkRadius);

        Gizmos.color = Color.yellow;     // 左右墙
        Gizmos.DrawWireSphere(pos + leftOffset, checkRadius);
        Gizmos.DrawWireSphere(pos + rightOffset, checkRadius);

        Gizmos.color = Color.red;        // 前方边缘探测
        Gizmos.DrawWireSphere(pos + LedgeOffsetFacing, checkRadius);
    }
}
