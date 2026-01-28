using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Müzikler")]
    public AudioClip menuMusic;
    public AudioClip levelMusic;

    [Header("Ayarlar")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public string menuSceneName = "MainMenu"; // Senin menü sahnennin adý neyse buraya yaz

    private AudioSource audioSource;

    private void Awake()
    {
        // --- SINGLETON (TEKÝL) YAPI ---
        // Bu objeden sadece 1 tane olsun, sahneler arasý yok olmasýn.
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Sahne deðiþince beni yok etme!

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.loop = true; // Müzik döngüye girsin
        }
        else
        {
            Destroy(gameObject); // Zaten bir tane var, ben fazlalýðým
        }
    }

    private void OnEnable()
    {
        // Sahne yüklendiðinde haberim olsun
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Oyun ilk açýldýðýnda da kontrol et
        PlayMusicForScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene);
    }

    private void PlayMusicForScene(Scene scene)
    {
        AudioClip musicToPlay = null;

        // Sahne ismine göre hangi müziðin çalacaðýna karar ver
        if (scene.name == menuSceneName)
        {
            musicToPlay = menuMusic;
        }
        else
        {
            // Menü deðilse oyun müziði çal (GameOver sahnesi hariç istersen buraya else if ekleyebilirsin)
            musicToPlay = levelMusic;
        }

        // --- KRÝTÝK NOKTA ---
        // Eðer çalmasý gereken müzik zaten þu an çalýyorsa DOKUNMA.
        // Böylece level resetlendiðinde müzik kesilmez.
        if (audioSource.clip == musicToPlay) return;

        // Yeni müziði çal
        if (musicToPlay != null)
        {
            audioSource.clip = musicToPlay;
            audioSource.volume = musicVolume;
            audioSource.Play();
        }
    }

    // Ses seviyesini dýþarýdan deðiþtirmek istersen (Ayarlar menüsü için)
    public void SetVolume(float volume)
    {
        musicVolume = volume;
        if (audioSource != null) audioSource.volume = volume;
    }
}