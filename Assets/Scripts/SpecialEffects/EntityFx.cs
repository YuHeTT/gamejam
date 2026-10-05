using System.Collections;
using UnityEngine;

public class EntityFx : MonoBehaviour
{
    private SpriteRenderer sr;

    [Header("FlashFx")]
    [SerializeField] private Material hitMat;
    [SerializeField] private Material originalMat;
    [SerializeField] private float originalTime;
    [SerializeField] private float fxTime;
    [SerializeField] private float totalTime;

    private Coroutine flashCoroutine;

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            originalMat = sr.material;
        }
    }

    public void PlayFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        flashCoroutine = StartCoroutine(FlashFx());
    }

    private IEnumerator FlashFx()
    {
        if (sr == null)
        {
            flashCoroutine = null;
            yield break;
        }

        if (originalMat == null || hitMat == null ||
            totalTime <= 0f || fxTime <= 0f || originalTime <= 0f)
        {
            if (originalMat != null)
            {
                sr.material = originalMat;
            }

            flashCoroutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < totalTime)
        {
            sr.material = hitMat;

            float waitTime = Mathf.Min(fxTime, totalTime - elapsedTime);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;

            if (elapsedTime >= totalTime)
            {
                break;
            }

            sr.material = originalMat;

            waitTime = Mathf.Min(originalTime, totalTime - elapsedTime);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        sr.material = originalMat;
        flashCoroutine = null;
    }

    public void RedColorBlink()
    {
        if(sr.color != Color.white)
        {
            sr.color = Color.white;
        }
        else
        {
            sr.color = Color.red;
        }
    }

    public void CancelBlink()
    {
        CancelInvoke();
        sr.color = Color.white;
    }
}
