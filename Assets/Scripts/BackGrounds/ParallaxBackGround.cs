using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxBackGround : MonoBehaviour
{
    protected GameObject cam;
    public float deviate;
    [SerializeField] protected float ParallaxEffect;
    [SerializeField] protected float xPosition;
    [SerializeField] protected float distanceToMove;
    [SerializeField] protected float startCamX;
    [SerializeField] protected float camX;
    protected virtual void Start()
    {
        cam = GameObject.Find("Main Camera");
        startCamX = cam.transform.position.x;
        xPosition = transform.position.x;
    }

    protected virtual void LateUpdate()
    {
        camX = cam.transform.position.x;
        distanceToMove = (camX - startCamX) * ParallaxEffect;
        transform.position = new Vector2(xPosition + distanceToMove,cam.transform.position.y + deviate);
    }
}
