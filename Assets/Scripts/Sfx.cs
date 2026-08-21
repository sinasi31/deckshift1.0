using System.Collections.Generic;
using UnityEngine;

// The one call every sound in the game goes through:  Sfx.Play("Enemy.Zombie.Swing", position);
//
// It resolves the id against the SoundBank, picks a variant that is not the one it just played,
// jitters pitch and volume, refuses to machine-gun, ducks stacked copies, and hands the result to a
// pooled AudioSource.
//
// ⚠️ WHY POOLED SOURCES AND NOT `AudioSource.PlayClipAtPoint` — this is the important one.
// PlayClipAtPoint gives you NO WAY TO SET PITCH. Everything in the project used it, so every sound
// in the game played at exactly pitch 1.000 every single time: every sword swing byte-identical,
// every footstep byte-identical. That is a large part of why the audio reads as "the same stuff
// everywhere", and no amount of better source material fixes it. It also creates and destroys a
// GameObject per sound, which is pure garbage in a game that spawns scrap shards by the handful.
//
// The pool is created on the SfxManager (already a DontDestroyOnLoad singleton), so this needs no
// scene setup and survives scene loads.
public static class Sfx
{
    private const string BankPath = "SoundBank";     // Assets/Resources/SoundBank.asset
    private const float StackWindow = 0.14f;         // "already sounding" window for the duck
    private const int PoolSize = 24;

    private static SoundBank bank;
    private static AudioSource[] pool;
    private static int nextSource;
    private static bool warnedMissingBank;
    private static readonly HashSet<string> warnedIds = new HashSet<string>();

    public static SoundBank Bank
    {
        get
        {
            if (bank == null) bank = Resources.Load<SoundBank>(BankPath);
            return bank;
        }
    }

    /// <summary>Play a bank event at a world position.</summary>
    public static void Play(string id, Vector3 position)
    {
        var e = Resolve(id);
        if (e == null) return;
        PlayEvent(e, position, true);
    }

    /// <summary>Play a bank event in 2D — always equally loud, wherever it happened.</summary>
    public static void Play(string id)
    {
        var e = Resolve(id);
        if (e == null) return;
        PlayEvent(e, Vector3.zero, false);
    }

    private static SoundEvent Resolve(string id)
    {
        if (Bank == null)
        {
            // ⚠️ Warn ONCE, not per call. A missing bank in a busy scene would otherwise produce
            // thousands of identical console lines and bury whatever the real problem was.
            if (!warnedMissingBank)
            {
                warnedMissingBank = true;
                Debug.LogWarning("Sfx: no SoundBank at Resources/" + BankPath + " — all bank sounds are silent.");
            }
            return null;
        }
        var e = Bank.Find(id);
        if (e == null && warnedIds.Add(id))
            Debug.LogWarning("Sfx: no event '" + id + "' in the SoundBank. Add it, or fix the caller.");
        return e;
    }

    private static void PlayEvent(SoundEvent e, Vector3 position, bool positionalOverride)
    {
        float now = Time.unscaledTime;

        // Hard throttle. A burst arriving faster than this is not information the player can hear
        // as separate events — it is a buzz.
        if (now - e.lastPlayed < e.minInterval) return;

        AudioClip clip = e.NextVariant();
        if (clip == null) return;

        // Stack duck: how many of THIS event are still sounding right now?
        if (e.recent == null) e.recent = new List<float>(8);
        for (int i = e.recent.Count - 1; i >= 0; i--)
            if (now - e.recent[i] > StackWindow) e.recent.RemoveAt(i);
        float duck = Mathf.Pow(Mathf.Clamp(e.stackDuck, 0.3f, 1f), e.recent.Count);

        e.recent.Add(now);
        e.lastPlayed = now;

        float vol = Random.Range(e.volume.x, e.volume.y) * duck * SfxManager.Volume;
        float pitch = Random.Range(e.pitch.x, e.pitch.y);
        bool spatial = e.positional && positionalOverride;

        var src = Take();
        if (src == null) return;
        src.clip = clip;
        src.volume = Mathf.Clamp01(vol);
        src.pitch = pitch;
        src.spatialBlend = spatial ? 1f : 0f;
        src.transform.position = spatial ? position : Vector3.zero;
        src.Play();
    }

    // Round-robin over the pool. Prefer a free source; if every one is busy, steal the oldest —
    // dropping the sound entirely would be worse than clipping one that is already ending.
    private static AudioSource Take()
    {
        EnsurePool();
        if (pool == null) return null;

        for (int i = 0; i < pool.Length; i++)
        {
            var s = pool[(nextSource + i) % pool.Length];
            if (s != null && !s.isPlaying)
            {
                nextSource = (nextSource + i + 1) % pool.Length;
                return s;
            }
        }
        var steal = pool[nextSource];
        nextSource = (nextSource + 1) % pool.Length;
        return steal;
    }

    private static void EnsurePool()
    {
        // Statics survive a scene load but the GameObjects they point at may not, so this checks the
        // sources are still alive rather than trusting a null check on the array.
        if (pool != null && pool.Length > 0 && pool[0] != null) return;
        if (SfxManager.instance == null) return;

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("SfxVoice_" + i);
            go.transform.SetParent(SfxManager.instance.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 6f;
            s.maxDistance = 34f;
            pool[i] = s;
        }
    }

    /// <summary>Editor/testing hook — forget the cached bank and pool.</summary>
    public static void Reset()
    {
        bank = null; pool = null; nextSource = 0;
        warnedMissingBank = false; warnedIds.Clear();
    }
}
