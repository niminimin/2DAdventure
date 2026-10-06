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
    /// 攻击的几何中心（世界坐标），用来算击退方向。
    /// 不能用 transform.position：Tilemap 的 transform 永远在网格原点(0,0)，
    /// 和它上面画的格子无关。荆棘画在 x=49、transform 却在 0，
    /// 会让 (受害者x - 0) 恒为正 —— 无论从哪边撞都朝右弹。
    /// 碰撞体 bounds 才反映物体实际的几何范围。
    /// </summary>
    public Vector2 KnockbackOrigin
    {
        get
        {
            // 惰性缓存：Attack 也可能挂在未激活的攻击判定盒上，用 Awake 缓存会漏掉
            if (!colSearched)
            {
                col = GetComponent<Collider2D>();
                colSearched = true;
            }

            if (col != null && col.bounds.size.x > 0.001f)
                return col.bounds.center;

            return transform.position;
        }
    }
}
