using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class doorwithtrigger : MonoBehaviour
{
    public triggerfloor[] triggerfloors;
    private bool canOpen = false;

    public float moveDistance = 3f;
    public float moveSpeed = 6f;

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
        canOpen = true;
        for(int i = 0; i < triggerfloors.Length; i++)
        {
            if ( !triggerfloors[i].isPlayerOnFloor)
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
