using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloneSkillController : MonoBehaviour
{   
    public SpriteRenderer sr;
    private Animator anim;
    [SerializeField] private float colorLosingSpeed;
    [SerializeField] private int facingDir = 1;
    public float cloneTimer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        if(cloneTimer > 0)
            cloneTimer -= Time.deltaTime;

        else
        {
            sr.color = new Color(1,1,1,sr.color.a - (Time.deltaTime * colorLosingSpeed));
        }
        if(sr.color.a <= 0)
        {
            Destroy(gameObject);
        }
    }


    public void SetUpClone(Transform _newTransform,float _cloneDuration, bool _canAttack)
    {
        if (_canAttack)
        {
            anim.SetInteger("AttackNumber",Random.Range(1,4));
        }


        transform.position = _newTransform.position;
        if(facingDir != PlayerManager.instance.player.facingDir)
        {
            transform.Rotate(0, 180, 0);
        }
        cloneTimer = _cloneDuration;
    }
}
