using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill : MonoBehaviour
{
    [SerializeField] protected float coolDown;
    [SerializeField] protected float buffer;
    protected float coolDownTimer;
    protected float bufferTimer;

    protected virtual void Start()
    {
        coolDownTimer = coolDown;
    }

    protected virtual void Update()
    {
        if(coolDownTimer > 0)
            coolDownTimer -= Time.deltaTime;
    }

    public virtual bool CanUseSkill()
    {
        if(coolDownTimer <= 0)
        {
            UseSkill();
            coolDownTimer = coolDown;
            return true;
        }

        Debug.Log("Skill is on coolDown");
        return false;
    }
     
    public virtual void UseSkill()
    {
        
    }
}
