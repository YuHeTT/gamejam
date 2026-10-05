using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CameraShaker : MonoBehaviour
{
    private readonly List<ShakeRequest> requests = new();
    private CinemachineBasicMultiChannelPerlin _noise;
    public static CameraShaker Instance { get ; private set ; }

    private void Awake()
    {
        Instance = this;
        _noise = GetComponent<CinemachineVirtualCamera>()
            .GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void LateUpdate()
    {
        if (_noise == null)
            return;

        for (int i = requests.Count - 1; i >= 0; i--)
        {
            requests[i].remaining -= Time.unscaledDeltaTime;
            if (requests[i].remaining <= 0f)
                requests.RemoveAt(i);
        }

        _noise.m_AmplitudeGain = requests.Count > 0 ? requests.Max(request => request.amount *request.remaining / request.duration) : 0f;
    }

    public void RequestShake(float amount, float time)
    {
        if (_noise == null || amount <= 0f || time <= 0f)
            return;

        requests.Add(new ShakeRequest(amount, time));
        _noise.m_AmplitudeGain = Mathf.Max(_noise.m_AmplitudeGain, amount);
    }

    private class ShakeRequest
    {
        public readonly float amount;
        public readonly float duration;
        public float remaining;

        public ShakeRequest(float amount, float duration)
        {
            this.amount = amount;
            this.duration = duration;
            remaining = duration;
        }
    }
}
