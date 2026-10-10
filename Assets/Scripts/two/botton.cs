using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按钮：玩家（或其它未被排除的物体）压在触发区上时把 <see cref="door"/> 匀速顶起来，离开 1 秒后落回。<br/>
/// 谁能触发由 <see cref="MechanismQuery"/> 统一决定：道具与墓碑默认<b>不参与</b>按钮互动
/// （需要让某个道具压按钮时，勾上该道具的 canTriggerPedal）。
/// </summary>
public class botton : MonoBehaviour
{
    public GameObject door;
    public float moveDistance = 4f;
    public float moveSpeed = 8f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private Vector3 targetPosition;

    // 踩按钮音效的边沿判断：OnTriggerStay2D 每帧都会回调，不判边沿会疯狂重复播放
    private bool _pressed;

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

    private void OnTriggerStay2D(Collider2D collision)
    {
        //道具/墓碑默认不参与：与 triggerfloor、PlatformSensor 用同一套过滤规则
        if (!MechanismQuery.CanTriggerPedal(collision)) return;

        // 踩上去的那一刻播一次音效（元素8「踩压力板音效」），与踏板用同一个
        if (!_pressed)
        {
            _pressed = true;
            musicmanager.PlayShotSound(musicmanager.ShotIndexPressurePlate);
        }

        opendoor();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!MechanismQuery.CanTriggerPedal(collision)) return;
        _pressed = false;
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
