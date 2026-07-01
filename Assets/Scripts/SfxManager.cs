using UnityEngine;

// Central sound-effect volume so a single settings slider can scale every SFX in the game.
// AudioSource.PlayClipAtPoint cannot route to an AudioMixer, so instead of a mixer group we
// route all SFX through here and multiply by the saved SFX volume.
//
// All game SFX should play via SfxManager.PlayAtPoint(...) or SfxManager.PlayOn(...) instead of
// calling AudioSource.PlayClipAtPoint / AudioSource.PlayOneShot directly. Music is NOT affected
// (it is owned by MusicManager and has its own volume).
//
// Self-bootstraps before the first scene loads, so it always exists without any scene setup —
// if the manager is somehow missing, SFX still play at full volume (Volume falls back to 1).
public class SfxManager : MonoBehaviour
{
    public static SfxManager instance;

    const string PrefKey = "SfxVolume";

    [Range(0f, 1f)] public float sfxVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("SfxManager");
        go.AddComponent<SfxManager>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            sfxVolume = PlayerPrefs.GetFloat(PrefKey, 1f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Current global SFX volume (0..1). Safe to call before the instance exists.
    public static float Volume => instance != null ? instance.sfxVolume : 1f;

    // Set + persist the global SFX volume. Called by the settings slider.
    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKey, sfxVolume);
        PlayerPrefs.Save();
    }

    // One-shot at a world position. Replaces AudioSource.PlayClipAtPoint.
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float localVolume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, localVolume * Volume);
    }

    // One-shot on an existing AudioSource. Replaces source.PlayOneShot.
    public static void PlayOn(AudioSource source, AudioClip clip, float localVolume = 1f)
    {
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, localVolume * Volume);
    }
}
