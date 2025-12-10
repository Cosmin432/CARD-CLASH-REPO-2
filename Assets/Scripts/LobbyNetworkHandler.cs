using Fusion;
using UnityEngine;

public class LobbyNetworkHandler : NetworkBehaviour
{
    private LobbyManager lobbyManager;

    public void Initialize(LobbyManager manager)
    {
        lobbyManager = manager;
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
        
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
        }

        if (lobbyManager != null)
        {
            lobbyManager.OnReadyStateChanged(player, ready);
        }
    }
}