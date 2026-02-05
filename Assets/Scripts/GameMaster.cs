using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameMaster : NetworkBehaviour
{
    public static GameMaster Instance { get; private set; }

    [Header("UI Elements")]
    public Button attackButton;
    public Button skipButton;
    public TextMeshProUGUI hostHpText;
    public TextMeshProUGUI clientHpText;
    public TextMeshProUGUI hostEnergyText;
    public TextMeshProUGUI clientEnergyText;
    public TextMeshProUGUI turnIndicatorText;

    [Header("Networked Stats")]
    [Networked] public int HostHP { get; set; }
    [Networked] public int ClientHP { get; set; }
    [Networked] public int HostEnergy { get; set; }
    [Networked] public int ClientEnergy { get; set; }
    [Networked] public int CurrentRound { get; set; }
    [Networked] public NetworkBool IsHostTurn { get; set; }
    [Networked] public int TurnsThisRound { get; set; }
    [Networked] public NetworkBool GameInitialized { get; set; }

    public override void Spawned()
    {
        Debug.Log("GameMaster Spawned!");
        
        // Set singleton instance
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameMasters detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Only the host initializes the game state
        if (Object.HasStateAuthority)
        {
            Debug.Log("Host initializing game...");
            HostHP = 100;
            ClientHP = 100;
            HostEnergy = 0;
            ClientEnergy = 0;
            CurrentRound = 1;
            TurnsThisRound = 0;
            
            PickRandomTurn();
            StartNewRound();
            
            GameInitialized = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // This runs after Spawned() is called, safe to access Networked properties
        if (GameInitialized)
        {
            UpdateUI();
        }
    }

    void PickRandomTurn()
    {
        IsHostTurn = Random.Range(0, 2) == 0;
        Debug.Log(IsHostTurn ? "HOST starts" : "CLIENT starts");
    }

    public void OnAttackClicked()
    {
        if (!CanPlay())
        {
            Debug.LogWarning("Cannot attack - not your turn!");
            return;
        }

        Debug.Log($"Attack button clicked by {(Runner.IsServer ? "HOST" : "CLIENT")}");
        RPC_ProcessAttack();
    }

    public void OnSkipClicked()
    {
        if (!CanPlay())
        {
            Debug.LogWarning("Cannot skip - not your turn!");
            return;
        }

        Debug.Log($"Skip button clicked by {(Runner.IsServer ? "HOST" : "CLIENT")}");
        RPC_SkipTurn();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_ProcessAttack()
    {
        Debug.Log($"[SERVER] Processing attack from {(IsHostTurn ? "HOST" : "CLIENT")}");
        
        if (IsHostTurn)
        {
            Debug.Log("HOST attacks CLIENT!");
            ClientHP -= 10;
            // Optional energy cost:
            // if (HostEnergy >= 1) HostEnergy -= 1;
        }
        else
        {
            Debug.Log("CLIENT attacks HOST!");
            HostHP -= 10;
            // if (ClientEnergy >= 1) ClientEnergy -= 1;
        }

        EndTurn();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_SkipTurn()
    {
        Debug.Log($"[SERVER] {(IsHostTurn ? "HOST" : "CLIENT")} SKIPS turn");
        EndTurn();
    }

    bool CanPlay()
    {
        if (!GameInitialized) return false;
        
        // Check if it's this player's turn
        if (Runner.IsServer && !IsHostTurn) return false;
        if (!Runner.IsServer && IsHostTurn) return false;
        return true;
    }

    void EndTurn()
    {
        TurnsThisRound++;
        Debug.Log($"Turn ended. Turns this round: {TurnsThisRound}");

        // Switch turn
        IsHostTurn = !IsHostTurn;

        // If both players played → next round
        if (TurnsThisRound >= 2)
        {
            CurrentRound++;
            StartNewRound();
        }
    }

    void StartNewRound()
    {
        Debug.Log($"--- ROUND {CurrentRound} START ---");

        // Add energy based on current round
        HostEnergy += CurrentRound;
        ClientEnergy += CurrentRound;

        TurnsThisRound = 0;
        
        Debug.Log($"Host Energy: {HostEnergy}, Client Energy: {ClientEnergy}");
    }

    void UpdateUI()
    {
        if (hostHpText != null) hostHpText.text = HostHP.ToString();
        if (clientHpText != null) clientHpText.text = ClientHP.ToString();
        if (hostEnergyText != null) hostEnergyText.text = HostEnergy.ToString();
        if (clientEnergyText != null) clientEnergyText.text = ClientEnergy.ToString();
        
        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = IsHostTurn ? "HOST's turn" : "CLIENT's turn";
        }

        bool canPlay = CanPlay();
        if (attackButton != null) attackButton.interactable = canPlay;
        if (skipButton != null) skipButton.interactable = canPlay;
    }
}