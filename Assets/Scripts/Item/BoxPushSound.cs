using UnityEngine;

/// <summary>挂在 box 预制体上：玩家真正推动箱子时播放推箱音效。</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(AudioSource))]
public class BoxPushSound : MonoBehaviour
{
    [Tooltip("推箱子的音效")]
    public AudioClip pushClip;

    [Tooltip("两次推箱音效之间的最短间隔（秒）")]
    [Min(0f)]
    public float cooldown = 0.18f;

    private Rigidbody2D boxBody;
    private AudioSource source;
    private float nextPlayTime;
    private bool pushingThisFixedFrame;

    private void Awake()
    {
        boxBody = GetComponent<Rigidbody2D>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = musicmanager.EffectiveShotVolume;
    }

    private void FixedUpdate()
    {
        //OnCollisionStay2D 会在本次物理步之后设置为 true；若本步没有有效推动，
        //Update 会停止仍在播放的音效。
        pushingThisFixedFrame = false;
    }

    private void Update()
    {
        if (source != null)
            source.volume = musicmanager.EffectiveShotVolume;

        if (!pushingThisFixedFrame && source != null && source.isPlaying)
            source.Stop();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (pushClip == null || boxBody == null || source == null)
            return;

        //只响应 Player 碰撞；箱子和墙、地面、其它箱子碰撞时不播放。
        Player player = collision.collider.GetComponentInParent<Player>();
        if (player == null)
            return;

        //必须有朝向箱子的水平输入。只看箱子速度会把玩家松键后的惯性误判成继续推动。
        float input = PlayerState.xInput;
        float playerToBox = transform.position.x - player.transform.position.x;
        bool pushesTowardBox = Mathf.Abs(input) > 0.01f && playerToBox * input > 0f;
        if (!pushesTowardBox || Mathf.Abs(boxBody.velocity.x) < 0.01f)
        {
            source.Stop();
            return;
        }

        pushingThisFixedFrame = true;

        if (Time.time < nextPlayTime)
            return;

        //使用可停止的普通播放，而不是 PlayOneShot；停止推动时可以立即截断剩余音频。
        if (!source.isPlaying)
        {
            source.clip = pushClip;
            source.Play();
        }
        nextPlayTime = Time.time + cooldown;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<Player>() != null && source != null)
        {
            pushingThisFixedFrame = false;
            source.Stop();
        }
    }
}
