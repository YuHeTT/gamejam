using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class EndLessBackGrounds : ParallaxBackGround
{
    private float length;
    protected override void Start()
    {
        base.Start();
        length = GetComponent<SpriteRenderer>().bounds.size.x;
    }
    protected override void LateUpdate()
    {
        base.LateUpdate();
        float distanceMoved = (camX - startCamX) * (1 - ParallaxEffect);
        if(distanceMoved > xPosition + length)
        {
            xPosition += length;
        }
        else if(distanceMoved < xPosition - length)
        {
            xPosition -= length;
        }
    }
}
