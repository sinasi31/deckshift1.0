using UnityEngine;

public enum GameState
{
    Playing,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public PlayerController player; // Player'� bulmak i�in hala �nemli

    public GameState currentState;

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }

        currentState = GameState.Playing;
    }

    public void SetGameState(GameState newState)
    {
        currentState = newState;
        Debug.Log("Game state changed to: " + newState);
    }

    private int pauseDepth = 0;

    // True while ANY UI is holding the game paused.
    //
    // This is the honest "is another screen already up?" test, and it is what PauseScreen uses to
    // decide whether Escape belongs to it. Every modal in the project routes through RequestPause —
    // the shop, the map, the forge, Blompo, chests, the quest board, the relic panels — so one
    // check covers all of them and cannot fall behind when a new screen is added. The alternative
    // (a hand-kept list of `SomeScreen.IsOpen` flags) is the exact pattern that has rotted twice
    // already in this project.
    //
    // HitStop and Adrenaline's slow-motion deliberately bypass the counter, so they do not register
    // here — which is correct: neither of them is a screen, and Escape should still work during one.
    public bool IsUIPaused => pauseDepth > 0;

    public void RequestPause()
    {
        pauseDepth++;
        if (pauseDepth == 1) Time.timeScale = 0f;
    }

    public void ReleasePause()
    {
        pauseDepth = Mathf.Max(0, pauseDepth - 1);
        if (pauseDepth == 0) Time.timeScale = 1f;
    }

    // --- BURADAK� T�M JUMP CHARGE KODLARI S�L�ND� ---
    // (Update, Start, AddCharges, ve de�i�kenler)
}