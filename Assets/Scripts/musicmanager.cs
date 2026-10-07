using UnityEngine;

public class musicmanager : MonoBehaviour
{

    public static musicmanager instance;
    public AudioSource bgm;
    public AudioSource shot;

    public AudioClip[] bgmclips;
    public AudioClip[] shotclips;

    private AudioClip currentbgmclip;


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return; // 重复实例本帧就会被销毁，不再做任何初始化
        }

        if (bgm != null)
        {
            // 背景音乐循环：当前音乐播完自动从头再播，不需要额外计时或回调
            bgm.loop = true;
        }
        else
        {
            Debug.LogError("[musicmanager] bgm AudioSource 未赋值");
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayBGM(0); // Play the first BGM clip at the start
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayBGM(int index)
    {
        if (bgm == null)
        {
            Debug.LogError("[musicmanager] bgm AudioSource 未赋值，无法播放背景音乐");
            return;
        }
        if (bgmclips == null || index < 0 || index >= bgmclips.Length)
        {
            Debug.LogWarning("Invalid BGM index: " + index);
            return;
        }
        AudioClip newClip = bgmclips[index];
        if (newClip == null)
        {
            Debug.LogWarning("BGM clip 为空, index: " + index);
            return;
        }

        if (newClip != currentbgmclip)
        {
            // 切换到另一首：从 0 开始播放，并保持循环
            bgm.clip = newClip;
            bgm.loop = true;
            bgm.Play();
            currentbgmclip = newClip;
        }
        else if (!bgm.isPlaying)
        {
            // 还是同一首，但已经播完或被外部停掉了：重新播一遍
            bgm.loop = true;
            bgm.Play();
        }
        // 同一首且正在播放：直接忽略，避免重复调用时把音乐从头打断
    }

    public void PlayShot(int index)
    {
        if (shot == null)
        {
            Debug.LogError("[musicmanager] shot AudioSource 未赋值，无法播放音效");
            return;
        }
        if (shotclips == null || index < 0 || index >= shotclips.Length)
        {
            Debug.LogWarning("Invalid Shot index: " + index);
            return;
        }
        AudioClip newClip = shotclips[index];
        if (newClip == null)
        {
            Debug.LogWarning("Shot clip 为空, index: " + index);
            return;
        }
        shot.PlayOneShot(newClip);
    }
}
