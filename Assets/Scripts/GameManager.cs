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