using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloneSkill : Skill
{
    
    [Header("Clone Info")]
    [SerializeField] private GameObject clonePrefab;
    [SerializeField] private float cloneDuration;
    [Space]
    [SerializeField] private bool canAttack;
    public GameObject newClone;

    protected override void Update()
    {
        base.Update();
    }

    public void CreateClone(Transform _clonePosition)
    {
        newClone = Instantiate(clonePrefab);
        newClone.GetComponent<CloneSkillController>().SetUpClone(_clonePosition,cloneDuration,canAttack);
    }

    public void DestroyClone()
    {
        Destroy(newClone);
    }
}
