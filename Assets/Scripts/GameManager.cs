using UnityEngine;

public enum GameState
{
    Playing,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public PlayerController player; // Player'ý bulmak için hala önemli

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

    // --- BURADAKÝ TÜM JUMP CHARGE KODLARI SÝLÝNDÝ ---
    // (Update, Start, AddCharges, ve deðiþkenler)
}