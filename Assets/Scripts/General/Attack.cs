using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public int damage;
    public float attackRange;
    public float attackRate;

    private Collider2D col;
    private bool colSearched;

    private void OnTriggerStay2D(Collider2D collision)
    {
        collision.GetComponent<Character>()?.TakeDamage(this);
    }

    /// <summary>
    /// 攻击者身上「离受害者最近的点」（世界坐标），用来算击退方向。
    /// 两个都不能用：
    ///   1. transform.position —— Tilemap 的 transform 永远在网格原点(0,0)，和画的格子无关；
    ///   2. Collider2D.bounds.center —— 一个 Tilemap 上互不相邻的两组瓦片会被框成一个大包围盒。
    ///      本项目实例：坑底横排 (19,-18)~(22,-18) 与远处竖柱 (49,-12)~(49,-5)
    ///      被框成 x 19..50，中心 x=34.5 落在没有任何瓦片的空点上，
    ///      导致撞竖柱时 (48.65 - 34.5) = +14.15 → 反而被推进刺里。
    /// ClosestPoint 给的是真实几何上最近的位置，对多段 Tilemap 也成立。
    /// 另外：若受害者中心已在该碰撞体内部，Unity 会原样返回该点，
    /// 于是 delta = 0 —— 正好表示「你正站在它上面/里面」。
    /// </summary>
    public Vector2 ClosestPointTo(Vector2 victimPosition)
    {
        // 惰性缓存：Attack 也可能挂在未激活的攻击判定盒上，用 Awake 缓存会漏掉
        if (!colSearched)
        {
            col = GetComponent<Collider2D>();
            colSearched = true;
        }

        if (col != null && col.bounds.size.x > 0.001f)
            return col.ClosestPoint(victimPosition);

        return transform.position;
    }
}
