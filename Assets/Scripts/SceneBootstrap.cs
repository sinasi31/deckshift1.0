using UnityEngine;
using UnityEngine.SceneManagement;

// Re-creates self-bootstrapping singletons after EVERY scene load, not just the first one.
//
// ⚠️ THE TRAP THIS EXISTS FOR: [RuntimeInitializeOnLoadMethod] fires ONCE PER PLAY SESSION, not
// once per scene. The `AfterSceneLoad` load type says WHEN in the startup sequence it runs — it
// does NOT mean "after each scene load". Every reading of that attribute name suggests otherwise,
// which is exactly why this went unnoticed.
//
// The consequence, found 2026-08-09: dying goes SampleScene -> GameOverScene, and RESTART goes
// GameOverScene -> SampleScene. Both are scene loads, and a scene-local singleton is destroyed by
// the first one. Since the bootstrap never runs again, RunMapManager and ScrapHUD were gone for the
// REST OF THE SESSION — every run after the player's first death had no map (M did nothing, and
// LevelManager silently fell back to random room order) and no scrap counter.
//
// Managers that are PLACED in the scene do not have this problem: reloading the scene brings them
// back with it. Only the ones that create themselves are affected — which, ironically, is the
// pattern adopted to stop things going missing from scenes.
//
// USAGE — from a [RuntimeInitializeOnLoadMethod] method:
//
//     SceneBootstrap.Register(Create);
//
// `create` MUST be idempotent (return early if the object already exists), because it is called
// once immediately and then again on every sceneLoaded.
public static class SceneBootstrap
{
    public static void Register(System.Action create)
    {
        if (create == null) return;

        create();

        // One subscription per registering class, for the lifetime of the play session. Statics are
        // reset on domain reload, so these do not accumulate between editor play sessions.
        SceneManager.sceneLoaded += (scene, mode) => create();
    }
}
