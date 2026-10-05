using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class triggerfloor : MonoBehaviour
{
    [HideInInspector]public bool isPlayerOnFloor = false;

    private void OnTriggerStay2D(Collider2D collision)
    {
        isPlayerOnFloor = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        isPlayerOnFloor = false;
    }
}
