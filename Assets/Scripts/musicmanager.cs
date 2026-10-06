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
        if (index < 0 || index >= bgmclips.Length)
        {
            Debug.LogWarning("Invalid BGM index: " + index);
            return;
        }
        AudioClip newClip = bgmclips[index];
        if (newClip != currentbgmclip)
        {
            bgm.clip = newClip;
            bgm.Play();
            currentbgmclip = newClip;
        }
    }

    public void PlayShot(int index)
    {
        if (index < 0 || index >= shotclips.Length)
        {
            Debug.LogWarning("Invalid Shot index: " + index);
            return;
        }
        AudioClip newClip = shotclips[index];
        shot.PlayOneShot(newClip);
    }
}
