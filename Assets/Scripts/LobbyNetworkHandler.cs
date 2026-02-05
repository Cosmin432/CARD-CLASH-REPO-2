using Fusion;
using UnityEngine;

public class LobbyNetworkHandler : NetworkBehaviour
{
    private LobbyManager lobbyManager;

    // Stocăm selecția de deck pentru fiecare jucător
    [Networked, Capacity(2)]
    public NetworkDictionary<PlayerRef, int> PlayerDeckSelection => default;

    public void Initialize(LobbyManager manager)
    {
        lobbyManager = manager;
        Debug.Log("LobbyNetworkHandler initialized with LobbyManager");
    }

    public override void Spawned()
    {
        base.Spawned();

        // Păstrăm acest obiect când se schimbă scena
        if (Object.HasStateAuthority)
        {
            Debug.Log("LobbyNetworkHandler spawned with state authority");
            DontDestroyOnLoad(gameObject);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerReady(PlayerRef player, bool ready)
    {
        Debug.Log($"[SERVER RPC] Player {player} ready state: {ready}");
        RPC_BroadcastReadyState(player, ready);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastReadyState(PlayerRef player, bool ready)
    {
        Debug.Log($"[CLIENT RPC] Received ready state for Player {player}: {ready}");

        // Găsim LobbyManager doar dacă suntem în lobby scene
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
        }

        // Apelăm callback-ul doar dacă suntem în lobby
        if (lobbyManager != null)
        {
            lobbyManager.OnReadyStateChanged(player, ready);
        }
        else
        {
            Debug.Log("LobbyManager not found - probably in game scene");
        }
    }

    // RPC pentru a seta selecția de deck a unui jucător
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerDeck(PlayerRef player, int deckIndex)
    {
        Debug.Log($"[SERVER RPC] Player {player} selected deck: {deckIndex}");

        // Stocăm selecția în dicționarul networked
        PlayerDeckSelection.Set(player, deckIndex);
        Debug.Log($"[SERVER] Deck saved in dictionary. Total entries: {PlayerDeckSelection.Count}");

        // Broadcast către toți clienții
        RPC_BroadcastDeckSelection(player, deckIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastDeckSelection(PlayerRef player, int deckIndex)
    {
        Debug.Log($"[CLIENT RPC] Received deck selection for Player {player}: {deckIndex}");

        // Găsim LobbyManager doar dacă suntem în lobby scene
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
        }

        // Apelăm callback-ul doar dacă suntem în lobby
        if (lobbyManager != null)
        {
            lobbyManager.OnDeckSelectionChanged(player, deckIndex);
        }
        else
        {
            Debug.Log("LobbyManager not found - probably in game scene, deck selection stored in network dictionary");
        }
    }

    // Metodă helper pentru a obține selecția de deck a unui jucător
    public int GetPlayerDeckSelection(PlayerRef player)
    {
        // Verificăm dacă obiectul este valid înainte de a accesa dicționarul
        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning("LobbyNetworkHandler object is not valid yet!");
            return 0;
        }

        if (PlayerDeckSelection.TryGet(player, out int deckIndex))
        {
            Debug.Log($"Found deck selection for Player {player}: {deckIndex}");
            return deckIndex;
        }

        Debug.LogWarning($"No deck selection found for Player {player}, returning default");
        return 0; // Deck implicit
    }
}