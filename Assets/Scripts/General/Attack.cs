using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public int damage;
    public float attackRange;
    public float attackRate;

    private void OnTriggerStay2D(Collider2D collision)
    {
        collision.GetComponent<Character>()?.TakeDamage(this);
    }

    /// <summary>
    /// 攻击的「来源点」，被用来算击退方向。
    /// 不能直接用 attackerTrans.position：
    /// Tilemap 的 transform 永远在网格原点(0,0)，和它上面画的格子无关。
    /// 例如荆棘画在 x=49，transform 却在 0 —— 于是 (玩家x - 0) 恒为正，永远朝右弹。
    /// 这里改用碰撞体范围中心，它才反映物体真正的几何位置。
    /// </summary>
    public static Vector2 GetKnockbackOrigin(Transform attackerTrans)
    {
        if (attackerTrans == null)
            return Vector2.zero;

        Collider2D col = attackerTrans.GetComponent<Collider2D>();
        if (col != null && col.bounds.size.x > 0.001f)
            return col.bounds.center;

        return attackerTrans.position;
    }
}
