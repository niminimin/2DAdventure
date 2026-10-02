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

    public LayerMask groundLayer;

    [Header("状态")]
    public bool isGround;
    public bool touchLeftWall;
    public bool touchRightWall;


    // Update is called once per frame
    void Update()
    {
        check();
    }

    public void check()
    {
        //地面判断
         isGround = Physics2D.OverlapCircle((Vector2)transform.position + bottomOffset, checkRadius, groundLayer);
        
        //墙体判断
        touchLeftWall = Physics2D.OverlapCircle((Vector2)transform.position + leftOffset, checkRadius, groundLayer);
        touchRightWall = Physics2D.OverlapCircle((Vector2)transform.position + rightOffset, checkRadius, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere((Vector2)transform.position + bottomOffset, checkRadius);
        Gizmos.DrawWireSphere((Vector2)transform.position + leftOffset, checkRadius);
        Gizmos.DrawWireSphere((Vector2)transform.position + rightOffset, checkRadius);
    }
}
