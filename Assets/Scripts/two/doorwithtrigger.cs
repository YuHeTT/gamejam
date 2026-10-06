using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class doorwithtrigger : MonoBehaviour
{
    public triggerfloor[] triggerfloors;
    private bool canOpen = false;

    public float moveDistance = 4f;
    public float moveSpeed = 8f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private Vector3 targetPosition;

    // Start is called before the first frame update
    void Start()
    {
        closedPosition = transform.position;
        // 向上移动 moveDistance 米
        openedPosition = closedPosition + Vector3.up * moveDistance;
        targetPosition = closedPosition;
    }

    // Update is called once per frame
    void Update()
    {
        // 空数组 / 空元素都要当成"没有踏板被踩"（门保持关闭），否则会每帧抛 NullReferenceException。
        // 场景里确实存在这种情况：预制体 triggerfloors 的默认值是"长度 1、元素为 null"，
        // 若某个门实例没有做数组覆盖（例如 game6 的 door_floor），就会直接踩中。
        // 与 updown.cs 的处理保持一致（那边一直有 triggerfloors[i] != null 的判空）。
        canOpen = triggerfloors != null && triggerfloors.Length > 0;
        for(int i = 0; i < triggerfloors.Length; i++)
        {
            if (triggerfloors[i] == null || !triggerfloors[i].isPlayerOnFloor)
            {
                canOpen = false;
                break;
            }
            
        }
        if (canOpen)
        {
            targetPosition = openedPosition;
        }
        else
        {
            targetPosition = closedPosition;
        }

        // 用 MoveTowards 实现匀速移动，避免用 Lerp 造成的先快后慢
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);
    }
}
