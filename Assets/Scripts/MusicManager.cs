using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("M�zikler")]
    public AudioClip menuMusic;
    public AudioClip levelMusic;
    public AudioClip bossMusic; // Boss savaşında çalar (PlayBossMusic ile devreye girer)

    [Header("Ayarlar")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public string menuSceneName = "MainMenu"; // Senin men� sahnennin ad� neyse buraya yaz

    private AudioSource audioSource;

    private void Awake()
    {
        // --- SINGLETON (TEK�L) YAPI ---
        // Bu objeden sadece 1 tane olsun, sahneler aras� yok olmas�n.
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Sahne de�i�ince beni yok etme!

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.loop = true; // M�zik d�ng�ye girsin
        }
        else
        {
            Destroy(gameObject); // Zaten bir tane var, ben fazlal���m
        }
    }

    private void OnEnable()
    {
        // Sahne y�klendi�inde haberim olsun
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Oyun ilk a��ld���nda da kontrol et
        PlayMusicForScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene);
    }

    private void PlayMusicForScene(Scene scene)
    {
        AudioClip musicToPlay = null;

        // Sahne ismine g�re hangi m�zi�in �alaca��na karar ver
        if (scene.name == menuSceneName)
        {
            musicToPlay = menuMusic;
        }
        else
        {
            // Men� de�ilse oyun m�zi�i �al (GameOver sahnesi hari� istersen buraya else if ekleyebilirsin)
            musicToPlay = levelMusic;
        }

        // --- KR�T�K NOKTA ---
        // E�er �almas� gereken m�zik zaten �u an �al�yorsa DOKUNMA.
        // B�ylece level resetlendi�inde m�zik kesilmez.
        if (audioSource.clip == musicToPlay) return;

        // Yeni m�zi�i �al
        if (musicToPlay != null)
        {
            audioSource.clip = musicToPlay;
            audioSource.volume = musicVolume;
            audioSource.Play();
        }
    }

    // Ses seviyesini d��ar�dan de�i�tirmek istersen (Ayarlar men�s� i�in)
    public void SetVolume(float volume)
    {
        musicVolume = volume;
        if (audioSource != null) audioSource.volume = volume;
    }

    // Overrides the scene's default track with the boss theme until StopBossMusic() is called.
    // Called from MossKnightBoss.Start() when the boss room loads.
    public void PlayBossMusic()
    {
        if (bossMusic == null || audioSource == null) return;
        if (audioSource.clip == bossMusic) return; // already playing, don't restart

        audioSource.clip = bossMusic;
        audioSource.volume = musicVolume;
        audioSource.Play();
    }

    // Returns to the current scene's default music. Called when the boss dies.
    public void StopBossMusic()
    {
        PlayMusicForScene(SceneManager.GetActiveScene());
    }
}