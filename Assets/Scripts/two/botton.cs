using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class botton : MonoBehaviour
{
    public GameObject door;
    public float moveDistance = 3f;
    public float moveSpeed = 6f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        if (door == null)
        {
            Debug.LogError("botton: 没有指定 door 物体!", this);
            return;
        }

        // 记录门原来的位置，离开时才能精确恢复原位
        closedPosition = door.transform.position;
        // 向上移动 moveDistance 米
        openedPosition = closedPosition + Vector3.up * moveDistance;
        targetPosition = closedPosition;
    }

    private void Update()
    {
        if (door == null) return;

        // 用 MoveTowards 实现匀速移动，避免用 Lerp 造成的先快后慢
        door.transform.position = Vector3.MoveTowards(
            door.transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        opendoor();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Invoke("closedoor", 1f); 
    }

    // 触发：门匀速向上移动 2 米
    private void opendoor()
    {
        if (door == null) return;
        targetPosition = openedPosition;
    }

    // 离开：门匀速向下移动 2 米，恢复原位
    private void closedoor()
    {
        if (door == null) return;
        targetPosition = closedPosition;
    }

}
