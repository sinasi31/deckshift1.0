using UnityEngine;

// THE single source of truth for every player setting. Loaded from PlayerPrefs on boot, written
// back the moment a value changes, and read directly by the systems it affects.
//
// WHY THIS EXISTS RATHER THAN PlayerPrefs CALLS SPREAD AROUND: the old settings menu wrote
// PlayerPrefs.SetInt("ShowEnemyNumbers", ...) and fired a static event that exactly one class
// listened to, while AudioListener and SfxManager were poked directly from the menu's own
// callbacks. That works for three settings and falls apart at fifteen — the key strings live in two
// files at once, nothing tells you which settings exist, and a setting that is never re-read on
// scene load silently reverts. Every value now has ONE name, ONE default, and ONE place it is read.
//
// ⚠️ A SETTING MUST DO SOMETHING. Do not add a row to SettingsScreen without a consumer for it —
// a slider that moves and changes nothing is worse than an absent feature, because the player then
// distrusts the ones that DO work. Each property below names its consumer.
//
// ⚠️ Load() runs from SceneBootstrap, so it re-applies on every scene load. The audio settings in
// particular need this: AudioListener.volume is a static that survives a scene change, but
// MusicManager is re-created by the boot flow and would otherwise come back at its prefab volume.
public static class GameSettings
{
    // ---- keys ------------------------------------------------------------------------------------
    // Two of these are inherited from the old menu and MUST keep their spelling, or every existing
    // player's saved preference is silently discarded on update.
    private const string K_MASTER = "MasterVolume";
    private const string K_MUSIC = "MusicVolume";
    private const string K_SFX = "SfxVolume";            // written by SfxManager historically
    // ⚠️ A NEW KEY, not the inherited "ShowEnemyNumbers". That one meant "show the HP text on the
    // bar"; this one means "show the bar at all". Reusing it would have silently turned the bars OFF
    // for any existing player who had only turned the numbers off — a saved preference answering a
    // question nobody asked them. When a setting's MEANING changes, take a new key.
    private const string K_ENEMY_BARS = "ShowEnemyHealthBars";
    private const string K_DAMAGE_NUMBERS = "ShowDamageNumbers";
    private const string K_SHAKE = "ScreenShake";
    private const string K_HITSTOP = "HitStopStrength";
    private const string K_CARD_PREVIEW = "CardAimPreview";
    private const string K_DISPLAY_MODE = "DisplayMode";
    private const string K_VSYNC = "VSync";
    private const string K_FRAME_CAP = "FrameCap";

    // Raised after ANY value changes, so live systems can re-read without polling.
    public static event System.Action OnChanged;

    private static bool loaded;

    // ---- audio -----------------------------------------------------------------------------------

    private static float master = 1f;
    // Consumer: AudioListener.volume — the global mixer, so it scales music AND effects together.
    public static float MasterVolume
    {
        get { EnsureLoaded(); return master; }
        set { EnsureLoaded(); master = Mathf.Clamp01(value); AudioListener.volume = master; Save(K_MASTER, master); }
    }

    private static float music = 0.5f;
    // Consumer: MusicManager.SetVolume.
    public static float MusicVolume
    {
        get { EnsureLoaded(); return music; }
        set
        {
            EnsureLoaded();
            music = Mathf.Clamp01(value);
            if (MusicManager.instance != null) MusicManager.instance.SetVolume(music);
            Save(K_MUSIC, music);
        }
    }

    private static float sfx = 1f;
    // Consumer: SfxManager.Volume, which every PlayOn/PlayAtPoint multiplies by.
    public static float SfxVolume
    {
        get { EnsureLoaded(); return sfx; }
        set
        {
            EnsureLoaded();
            sfx = Mathf.Clamp01(value);
            if (SfxManager.instance != null) SfxManager.instance.SetVolume(sfx);
            Save(K_SFX, sfx);
        }
    }

    // ---- game feel -------------------------------------------------------------------------------

    private static float shake = 1f;
    // Consumer: CameraShake.Shake, which scales its intensity by this. 0 disables shake entirely.
    //
    // This is the single most requested accessibility control in an action game and this one shakes
    // a LOT — 23 call sites. Deliberately a slider, not a toggle: most people who can't tolerate
    // full shake are fine at 40%, and an on/off switch makes them choose between discomfort and
    // losing the feedback altogether.
    public static float ScreenShake
    {
        get { EnsureLoaded(); return shake; }
        set { EnsureLoaded(); shake = Mathf.Clamp01(value); Save(K_SHAKE, shake); }
    }

    private static float hitStop = 1f;
    // Consumer: HitStop.Stop, which scales the freeze duration. 0 skips the freeze completely.
    public static float HitStopStrength
    {
        get { EnsureLoaded(); return hitStop; }
        set { EnsureLoaded(); hitStop = Mathf.Clamp01(value); Save(K_HITSTOP, hitStop); }
    }

    private static bool damageNumbers = true;
    // Consumer: EnemyHealth's damage-popup spawn.
    public static bool DamageNumbers
    {
        get { EnsureLoaded(); return damageNumbers; }
        set { EnsureLoaded(); damageNumbers = value; Save(K_DAMAGE_NUMBERS, value); }
    }

    private static bool enemyHealthBars = true;
    // Consumer: EnemyHealthBar — switches its whole Canvas. On, the bar and its HP text are visible
    // the entire time the enemy is alive; off, nothing draws at all.
    public static bool EnemyHealthBars
    {
        get { EnsureLoaded(); return enemyHealthBars; }
        set { EnsureLoaded(); enemyHealthBars = value; Save(K_ENEMY_BARS, value); }
    }

    private static bool cardPreview = true;
    // Consumer: CardAimIndicator — the world-space preview of what the selected card will do.
    public static bool CardAimPreview
    {
        get { EnsureLoaded(); return cardPreview; }
        set { EnsureLoaded(); cardPreview = value; Save(K_CARD_PREVIEW, value); }
    }

    // ---- video -----------------------------------------------------------------------------------

    // 0 = Fullscreen, 1 = Borderless, 2 = Windowed. Stored as an index rather than as the Unity enum
    // so the saved value can't shift if Unity ever renumbers FullScreenMode.
    private static int displayMode;
    public static int DisplayMode
    {
        get { EnsureLoaded(); return displayMode; }
        set { EnsureLoaded(); displayMode = Mathf.Clamp(value, 0, 2); ApplyDisplayMode(); Save(K_DISPLAY_MODE, displayMode); }
    }

    public static readonly string[] DisplayModeNames = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };

    private static bool vSync = true;
    public static bool VSync
    {
        get { EnsureLoaded(); return vSync; }
        set { EnsureLoaded(); vSync = value; ApplyFrameRate(); Save(K_VSYNC, value); }
    }

    // Index into FrameCaps. Only has any effect with VSync off, which the screen says out loud.
    private static int frameCap;
    public static readonly int[] FrameCaps = { 60, 120, 144, 240, -1 };
    public static readonly string[] FrameCapNames = { "60", "120", "144", "240", "UNLIMITED" };

    public static int FrameCap
    {
        get { EnsureLoaded(); return frameCap; }
        set { EnsureLoaded(); frameCap = Mathf.Clamp(value, 0, FrameCaps.Length - 1); ApplyFrameRate(); Save(K_FRAME_CAP, frameCap); }
    }

    // ---- load / apply ----------------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneBootstrap.Register(Load);
    }

    // Idempotent: safe to call on every scene load, which is the point — see the header.
    public static void Load()
    {
        master = PlayerPrefs.GetFloat(K_MASTER, 1f);
        music = PlayerPrefs.GetFloat(K_MUSIC, 0.5f);
        sfx = PlayerPrefs.GetFloat(K_SFX, 1f);

        shake = PlayerPrefs.GetFloat(K_SHAKE, 1f);
        hitStop = PlayerPrefs.GetFloat(K_HITSTOP, 1f);
        damageNumbers = PlayerPrefs.GetInt(K_DAMAGE_NUMBERS, 1) == 1;
        enemyHealthBars = PlayerPrefs.GetInt(K_ENEMY_BARS, 1) == 1;
        cardPreview = PlayerPrefs.GetInt(K_CARD_PREVIEW, 1) == 1;

        displayMode = PlayerPrefs.GetInt(K_DISPLAY_MODE, 0);
        vSync = PlayerPrefs.GetInt(K_VSYNC, 1) == 1;
        frameCap = PlayerPrefs.GetInt(K_FRAME_CAP, 0);

        loaded = true;
        Apply();
    }

    // Pushes every value out to the system that owns it.
    public static void Apply()
    {
        AudioListener.volume = master;
        if (MusicManager.instance != null) MusicManager.instance.SetVolume(music);
        if (SfxManager.instance != null) SfxManager.instance.SetVolume(sfx);

        ApplyFrameRate();
        ApplyDisplayMode();

        OnChanged?.Invoke();
    }

    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        // With VSync on, targetFrameRate is ignored by Unity anyway; -1 keeps it out of the way
        // rather than fighting it.
        Application.targetFrameRate = vSync ? -1 : FrameCaps[Mathf.Clamp(frameCap, 0, FrameCaps.Length - 1)];
    }

    private static void ApplyDisplayMode()
    {
        // ⚠️ Deliberately does nothing in the editor. Screen.fullScreenMode there resizes the actual
        // EDITOR WINDOW, which is alarming and has to be undone by hand — and the setting is
        // meaningless until there's a build anyway.
#if !UNITY_EDITOR
        switch (displayMode)
        {
            case 1: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
            case 2: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            default: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
        }
#endif
    }

    // Restores every default and writes them, so "RESET TO DEFAULTS" is one call and can't drift
    // out of step with the defaults used at load.
    public static void ResetToDefaults()
    {
        MasterVolume = 1f;
        MusicVolume = 0.5f;
        SfxVolume = 1f;
        ScreenShake = 1f;
        HitStopStrength = 1f;
        DamageNumbers = true;
        EnemyHealthBars = true;
        CardAimPreview = true;
        DisplayMode = 0;
        VSync = true;
        FrameCap = 0;
    }

    private static void EnsureLoaded()
    {
        if (!loaded) Load();
    }

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    private static void Save(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    private static void Save(string key, bool value)
    {
        Save(key, value ? 1 : 0);
    }
}
