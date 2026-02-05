using Fusion;
using Fusion.Sockets;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI Screens")]
    public GameObject runnerPrefab;
    public GameObject mainScreen;
    public GameObject lobbyScreen;
    public GameObject popupScreen;
    public TMP_Text myReadyText;
    public TMP_Text lobbyStatusText;
    public TMP_Dropdown deck;
    public TMP_Text opponentDeckText; // Text pentru a afișa deck-ul adversarului

    [Header("Network Prefab")]
    public GameObject lobbyNetworkHandlerPrefab;

    [Header("Game Scene Network Prefab")]
    public GameObject gameNetworkHandlerPrefab;

    [Header("Assign your Game Scene")]
    public SceneRef gameScene;

    [Header("Lobby Settings")]
    public string lobbyName = "MyRoom";

    private Dictionary<PlayerRef, bool> playerReady = new Dictionary<PlayerRef, bool>();
    private Dictionary<PlayerRef, int> playerDeckSelections = new Dictionary<PlayerRef, int>();
    private NetworkRunner runner;
    private bool isRunnerActive = false;
    private LobbyNetworkHandler networkHandler;
    private bool gameHandlerSpawned = false;

    private void Start()
    {
        // Adăugăm listener pentru dropdown
        if (deck != null)
        {
            deck.onValueChanged.AddListener(OnDeckSelectionChanged);
        }
    }

    public void EditPopup(string message)
    {
        if (popupScreen == null) return;

        TMP_Text text = popupScreen.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = message;
        else
            Debug.LogWarning("PopupScreen has no TMP_Text child!");
    }

    private NetworkRunner GetOrCreateRunner()
    {
        if (runner != null && runner.gameObject != null)
        {
            return runner;
        }

        runner = FindObjectOfType<NetworkRunner>();

        if (runner == null)
        {
            if (runnerPrefab != null)
            {
                GameObject runnerObj = Instantiate(runnerPrefab);
                runner = runnerObj.GetComponent<NetworkRunner>();
                DontDestroyOnLoad(runnerObj);
            }
            else
            {
                GameObject runnerObj = new GameObject("NetworkRunner");
                runner = runnerObj.AddComponent<NetworkRunner>();
                DontDestroyOnLoad(runnerObj);
            }
        }

        return runner;
    }

    public async void CreateLobby()
    {
        if (isRunnerActive)
        {
            Debug.LogWarning("Runner is already active!");
            return;
        }

        runner = GetOrCreateRunner();

        if (runner == null)
        {
            Debug.LogError("Failed to create NetworkRunner!");
            return;
        }

        popupScreen.SetActive(true);
        EditPopup("Creating lobby...");

        runner.ProvideInput = false;
        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = lobbyName,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            isRunnerActive = true;

            if (lobbyNetworkHandlerPrefab != null)
            {
                var handlerObj = runner.Spawn(lobbyNetworkHandlerPrefab);
                networkHandler = handlerObj.GetComponent<LobbyNetworkHandler>();
                networkHandler.Initialize(this);
            }

            EditPopup("Lobby created!");
            mainScreen.SetActive(false);
            lobbyScreen.SetActive(true);
            popupScreen.SetActive(false);

            // Setăm deck-ul implicit pentru jucătorul local
            if (deck != null)
            {
                OnDeckSelectionChanged(deck.value);
            }
        }
        else
        {
            EditPopup("Failed to create lobby:\n" + result.ShutdownReason);
            isRunnerActive = false;
        }
    }

    public async void JoinLobby()
    {
        if (isRunnerActive)
        {
            Debug.LogWarning("Runner is already active!");
            return;
        }

        runner = GetOrCreateRunner();

        if (runner == null)
        {
            Debug.LogError("Failed to create NetworkRunner!");
            return;
        }

        popupScreen.SetActive(true);
        EditPopup("Joining lobby...");

        runner.ProvideInput = false;
        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = lobbyName,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            isRunnerActive = true;
            EditPopup("Joined lobby!");
            mainScreen.SetActive(false);
            lobbyScreen.SetActive(true);
            popupScreen.SetActive(false);

            // Așteaptă puțin pentru ca networkHandler să fie disponibil
            StartCoroutine(SendInitialDeckSelection());
        }
        else
        {
            EditPopup("Failed to join lobby:\n" + result.ShutdownReason);
            popupScreen.SetActive(true);
            isRunnerActive = false;
        }
    }

    private System.Collections.IEnumerator SendInitialDeckSelection()
    {
        yield return new WaitForSeconds(0.5f);

        if (deck != null)
        {
            OnDeckSelectionChanged(deck.value);
        }
    }

    /// <summary>
    /// Callback pentru când jucătorul local schimbă selecția de deck din dropdown
    /// </summary>
    private void OnDeckSelectionChanged(int deckIndex)
    {
        Debug.Log($"=== OnDeckSelectionChanged called with index: {deckIndex} ===");

        if (runner == null || !isRunnerActive)
        {
            Debug.LogWarning("Cannot change deck selection: not connected");
            return;
        }

        if (networkHandler == null)
        {
            networkHandler = FindObjectOfType<LobbyNetworkHandler>();
            if (networkHandler == null)
            {
                Debug.LogError("LobbyNetworkHandler not found!");
                return;
            }
        }

        Debug.Log($"Local player {runner.LocalPlayer} selected deck: {deckIndex}");
        Debug.Log($"Runner is server: {runner.IsServer}");

        // Trimitem selecția către server
        networkHandler.RPC_SetPlayerDeck(runner.LocalPlayer, deckIndex);
    }

    /// <summary>
    /// Callback apelat când un jucător își schimbă selecția de deck
    /// </summary>
    public void OnDeckSelectionChanged(PlayerRef player, int deckIndex)
    {
        Debug.Log($"[CALLBACK] Player {player} selected deck: {deckIndex}");

        playerDeckSelections[player] = deckIndex;

        // Actualizăm UI-ul doar pentru adversar
        if (runner != null && player != runner.LocalPlayer && opponentDeckText != null)
        {
            string deckName = GetDeckName(deckIndex);
            opponentDeckText.text = $"Opponent's Deck: {deckName}";
        }
    }

    /// <summary>
    /// Helper pentru a obține numele deck-ului pe baza indexului
    /// </summary>
    private string GetDeckName(int index)
    {
        if (deck != null && index >= 0 && index < deck.options.Count)
        {
            return deck.options[index].text;
        }
        return $"Deck {index}";
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("Player joined: " + player);

        if (!playerReady.ContainsKey(player))
            playerReady[player] = false;

        if (!playerDeckSelections.ContainsKey(player))
            playerDeckSelections[player] = 0; // Deck implicit

        UpdateLobbyStatus();
    }

    public void SetReady()
    {
        Debug.Log($"SetReady called. Runner null: {runner == null}, Active: {isRunnerActive}");

        if (runner == null)
        {
            Debug.LogError("Runner is null!");
            return;
        }

        if (!isRunnerActive)
        {
            Debug.LogError("Runner is not active!");
            return;
        }

        if (networkHandler == null)
        {
            networkHandler = FindObjectOfType<LobbyNetworkHandler>();
            if (networkHandler == null)
            {
                Debug.LogError("LobbyNetworkHandler not found!");
                return;
            }
        }

        bool currentReady = false;
        playerReady.TryGetValue(runner.LocalPlayer, out currentReady);

        bool newReady = !currentReady;

        Debug.Log($"Local player {runner.LocalPlayer} setting ready to: {newReady}");

        networkHandler.RPC_SetPlayerReady(runner.LocalPlayer, newReady);
    }

    public void OnReadyStateChanged(PlayerRef player, bool ready)
    {
        Debug.Log($"[CALLBACK] Player {player} ready state: {ready}");

        playerReady[player] = ready;

        if (runner != null && player == runner.LocalPlayer && myReadyText != null)
        {
            myReadyText.text = ready ? "READY" : "NOT READY";
            myReadyText.color = ready ? Color.green : Color.red;
        }

        UpdateLobbyStatus();
        CheckIfAllReady();
    }

    private void CheckIfAllReady()
    {
        if (!runner || !runner.IsServer) return;

        bool allReady = playerReady.Count >= 2 && playerReady.Values.All(v => v);
        if (allReady)
        {
            Debug.Log("All players ready! Loading game scene...");
            runner.LoadScene(gameScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void UpdateLobbyStatus()
    {
        if (lobbyStatusText == null) return;

        int playerCount = playerReady.Count;

        if (playerCount < 2)
        {
            lobbyStatusText.text = "Waiting for players to join...";
        }
        else
        {
            lobbyStatusText.text = "Waiting for both players to ready";
        }
    }

    public async void DisconnectLobby()
    {
        if (runner == null)
        {
            Debug.LogWarning("Runner is null, cannot disconnect.");
            return;
        }

        if (!isRunnerActive)
        {
            Debug.LogWarning("Runner is not active.");
            return;
        }

        Debug.Log("Disconnecting from lobby...");

        playerReady.Clear();
        playerDeckSelections.Clear();
        isRunnerActive = false;
        networkHandler = null;

        await runner.Shutdown(shutdownReason: ShutdownReason.Ok);

        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }

        mainScreen.SetActive(true);
        lobbyScreen.SetActive(false);
        popupScreen.SetActive(false);

        Debug.Log("Disconnected successfully.");
    }

    public void ExitApplication()
    {
        Debug.Log("Exiting application...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");
        playerReady.Remove(player);
        playerDeckSelections.Remove(player);

        UpdateLobbyStatus();

        if (runner.IsServer && runner.ActivePlayers.Count() == 0)
        {
            Debug.Log("No players left. Closing lobby…");
            DisconnectLobby();
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner shutdown: {shutdownReason}");

        playerReady.Clear();
        playerDeckSelections.Clear();
        isRunnerActive = false;
        networkHandler = null;
        gameHandlerSpawned = false;

        if (mainScreen != null && lobbyScreen != null)
        {
            mainScreen.SetActive(true);
            lobbyScreen.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }

        if (deck != null)
        {
            deck.onValueChanged.RemoveListener(OnDeckSelectionChanged);
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("Scene load completed!");

        // Simple check: if we're not in the lobby anymore and haven't spawned the handler yet
        // Only the Host/Server spawns the network handler
        if (runner.IsServer && gameNetworkHandlerPrefab != null && !gameHandlerSpawned)
        {
            // Small delay to ensure scene is fully loaded
            StartCoroutine(SpawnGameHandlerDelayed(runner));
        }
    }

    private System.Collections.IEnumerator SpawnGameHandlerDelayed(NetworkRunner runner)
    {
        yield return new WaitForSeconds(0.5f);

        // Check if we're in the game scene by checking if lobby UI is inactive
        if (lobbyScreen != null && !lobbyScreen.activeSelf)
        {
            var handlerObj = runner.Spawn(gameNetworkHandlerPrefab, Vector3.zero, Quaternion.identity);
            gameHandlerSpawned = true;
            Debug.Log("GameNetworkHandler spawned by Host!");
        }
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}