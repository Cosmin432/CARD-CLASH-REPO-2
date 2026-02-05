using System.Linq;
using Fusion;
using UnityEngine;

public class GameNetworkHandler : NetworkBehaviour
{
    [System.Serializable]
    public struct Stats : INetworkStruct
    {
        public int HP;
        public int Attack;
    }

    [Networked] public PlayerRef LastPlayerId { get; set; }
    [Networked] public int TurnCount { get; set; }
    [Networked] public int CurrentRound { get; set; }
    [Networked] public int Player1Energy { get; set; }
    [Networked] public int Player2Energy { get; set; }
    [Networked] public Stats Player1Stats { get; set; }
    [Networked] public Stats Player2Stats { get; set; }
    [Networked] public PlayerRef Player1Ref { get; set; }
    [Networked] public PlayerRef Player2Ref { get; set; }
    [Networked] public NetworkBool Player1UsedNonBurstCard { get; set; }
    [Networked] public NetworkBool Player2UsedNonBurstCard { get; set; }

    [Networked, Capacity(2)]
    public NetworkDictionary<PlayerRef, int> PlayerDeckSelection => default;

    private bool hasSpawned = false;
    private GameManager gameManager;
    private PlayerStatsManager statsManager;
    private TurnManager turnManager;
    private CardEffectsManager cardEffectsManager;

    private void Awake()
    {
        Debug.Log("GameNetworkHandler Awake!");
    }

    public override void Spawned()
    {
        hasSpawned = true;
        gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
        }

        if (Object.HasStateAuthority)
        {
            InitializeGame();
        }

        statsManager = new PlayerStatsManager(this);
        turnManager = new TurnManager(this);
        cardEffectsManager = new CardEffectsManager(this);

        Debug.Log($"GameNetworkHandler spawned! Authority: {Object.HasStateAuthority}");
    }

    // setare initiala a jocului
    private void InitializeGame()
    {
        LastPlayerId = PlayerRef.None;
        TurnCount = 0;
        CurrentRound = 1;
        Player1Energy = 1;
        Player2Energy = 1;
        Player1UsedNonBurstCard = false;
        Player2UsedNonBurstCard = false;

        var players = Runner.ActivePlayers.ToArray();
        Player1Ref = players[0];
        Player2Ref = players[1];

        TransferDeckSelectionsFromLobby();
        InitializePlayerStats();
    }

    public bool IsReady()
    {
        return hasSpawned && Object != null && Object.IsValid;
    }

    // copiaza deck-urile din lobby
    private void TransferDeckSelectionsFromLobby()
    {
        LobbyNetworkHandler lobbyHandler = FindObjectOfType<LobbyNetworkHandler>();

        if (lobbyHandler != null && lobbyHandler.Object != null && lobbyHandler.Object.IsValid)
        {
            foreach (var kvp in lobbyHandler.PlayerDeckSelection)
            {
                PlayerDeckSelection.Set(kvp.Key, kvp.Value);
            }
        }
    }

    // seteaza HP si attack pe baza deck-ului
    private void InitializePlayerStats()
    {
        foreach (var kvp in PlayerDeckSelection)
        {
            Stats stats = GetStatsForDeck(kvp.Value);

            if (kvp.Key == Player1Ref)
                Player1Stats = stats;
            else if (kvp.Key == Player2Ref)
                Player2Stats = stats;
        }
    }

    // stats diferite pentru fiecare deck
    private Stats GetStatsForDeck(int deckIndex)
    {
        Stats stats = new Stats();

        if (deckIndex == 0) { stats.HP = 25; stats.Attack = 3; }
        else if (deckIndex == 1) { stats.HP = 30; stats.Attack = 2; }
        else { stats.HP = 25; stats.Attack = 3; }

        return stats;
    }

    public Stats GetPlayerStats(PlayerRef player)
    {
        if (player == Player1Ref) return Player1Stats;
        if (player == Player2Ref) return Player2Stats;
        return default;
    }

    // verifica daca poate juca carti (FAST card restriction)
    public bool CanPlayerPlayCards(PlayerRef player)
    {
        if (player == Player1Ref) return !Player1UsedNonBurstCard;
        if (player == Player2Ref) return !Player2UsedNonBurstCard;
        return false;
    }

    public void SetPlayerUsedNonBurstCard(PlayerRef player, bool value)
    {
        if (!Object.HasStateAuthority) return;

        if (player == Player1Ref) Player1UsedNonBurstCard = value;
        else if (player == Player2Ref) Player2UsedNonBurstCard = value;
    }

    // skip tura
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SkipTurn(PlayerRef player, string playerRole)
    {
        if (LastPlayerId == player) return;

        TurnCount++;

        if (TurnCount % 2 == 0)
        {
            CurrentRound++;
            statsManager.TickDot(Player1Ref);
            statsManager.TickDot(Player2Ref);
            Player1Energy += CurrentRound + statsManager.GetEnergyPerRoundBonus(Player1Ref);
            Player2Energy += CurrentRound + statsManager.GetEnergyPerRoundBonus(Player2Ref);
        }

        LastPlayerId = player;
        statsManager.ClearAttackBonus(player);
        SetPlayerUsedNonBurstCard(Player1Ref, false);
        SetPlayerUsedNonBurstCard(Player2Ref, false);
    }

    // ataca adversarul
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AttackOpponent(PlayerRef attacker, string playerRole, int baseDamage)
    {
        if (LastPlayerId == attacker) return;

        PlayerRef opponent = GetOpponent(attacker);
        if (opponent != PlayerRef.None)
        {
            int finalDamage = statsManager.CalculateDamage(attacker, opponent);
            statsManager.ApplyDamage(opponent, finalDamage);
            statsManager.ClearAttackBonus(attacker);
            statsManager.ClearIncomingDamageReduction(opponent);
        }

        TurnCount++;

        if (TurnCount % 2 == 0)
        {
            CurrentRound++;
            statsManager.TickDot(Player1Ref);
            statsManager.TickDot(Player2Ref);
            Player1Energy += CurrentRound + statsManager.GetEnergyPerRoundBonus(Player1Ref);
            Player2Energy += CurrentRound + statsManager.GetEnergyPerRoundBonus(Player2Ref);
        }

        LastPlayerId = attacker;
        RPC_TriggerAttackAnimation(attacker);
        RPC_TriggerDamageAnimation(opponent);
        SetPlayerUsedNonBurstCard(Player1Ref, false);
        SetPlayerUsedNonBurstCard(Player2Ref, false);
        CheckGameOver();
    }

    // aplica efectele unei carti
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ApplyCardEffect(PlayerRef player, string cardID, string cardName,
        int playerHPChange, int opponentHPChange, int playerEnergyChange, int opponentEnergyChange,
        int characterOwner, int energyCost, int cardSpeed)
    {
        Debug.Log($"[SERVER] 🎴 CARD: '{cardName}' (ID: '{cardID}')");

        if (!turnManager.ValidateCardPlay(player, characterOwner, energyCost))
            return;

        ModifyEnergy(player, -energyCost);
        PlayerRef opponent = GetOpponent(player);

        if (playerHPChange != 0) statsManager.ApplyDamage(player, -playerHPChange);
        if (playerEnergyChange != 0) ModifyEnergy(player, playerEnergyChange);
        if (opponent != PlayerRef.None)
        {
            if (opponentHPChange != 0) statsManager.ApplyDamage(opponent, -opponentHPChange);
            if (opponentEnergyChange != 0) ModifyEnergy(opponent, opponentEnergyChange);
        }

        cardEffectsManager.ApplySpecialEffect(cardID, player, opponent);

        RPC_TriggerSkillAnimation(player);
        turnManager.HandleCardSpeed(player, cardSpeed, cardName);
        RPC_NotifyCardUsed(player, cardName, GetCharacterName(characterOwner), energyCost, cardSpeed);
        CheckGameOver();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerAttackAnimation(PlayerRef attacker)
    {
        if (gameManager == null) return;

        if (Runner.LocalPlayer == attacker)
            gameManager.TriggerPlayerAttackAnimation();
        else
            gameManager.TriggerOpponentAttackAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerDamageAnimation(PlayerRef victim)
    {
        if (gameManager == null) return;

        if (Runner.LocalPlayer == victim)
            gameManager.TriggerPlayerDamageAnimation();
        else
            gameManager.TriggerOpponentDamageAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerSkillAnimation(PlayerRef cardUser)
    {
        if (gameManager == null) return;

        if (Runner.LocalPlayer == cardUser)
            gameManager.TriggerPlayerSkillAnimation();
        else
            gameManager.TriggerOpponentSkillAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyCardUsed(PlayerRef player, string cardName, string characterName, int energyCost, int cardSpeed)
    {
        Debug.Log($"[CLIENT] ✅ Player {player.PlayerId} used card: {cardName}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyCannotPlayMoreCards(PlayerRef player, string attemptedCard)
    {
        Debug.LogWarning($"[CLIENT] ❌ Cannot play more cards");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyCardRejected(PlayerRef player, string cardName, string playerCharacter, string cardCharacter)
    {
        Debug.LogWarning($"[CLIENT] ❌ Card rejected");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyNotEnoughEnergy(PlayerRef player, string cardName, int required, int current)
    {
        Debug.LogWarning($"[CLIENT] ❌ Not enough energy");
    }

    private PlayerRef GetOpponent(PlayerRef player)
    {
        if (Runner == null) return PlayerRef.None;

        foreach (var activePlayer in Runner.ActivePlayers)
        {
            if (activePlayer != player) return activePlayer;
        }

        return PlayerRef.None;
    }

    // verifica daca jocul s-a terminat
    private void CheckGameOver()
    {
        if (Player1Stats.HP <= 0)
        { 
            Debug.Log("[SERVER] GAME OVER! Player 2 wins!");
            RPC_ShowGameOver(Player2Ref);
        }
        else if (Player2Stats.HP <= 0)
        { 
            Debug.Log("[SERVER] GAME OVER! Player 1 wins!");
            RPC_ShowGameOver(Player1Ref);
        }
    }

    // trimite game over la toti clientii
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGameOver(PlayerRef winner)
    {
        Debug.Log($"[CLIENT] RPC_ShowGameOver - Winner: {winner.PlayerId}, LocalPlayer: {Runner.LocalPlayer.PlayerId}");
        
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            // Determină mesajul bazat pe cine a câștigat
            string message = (Runner.LocalPlayer == winner) 
                ? "YOU WON - GAME OVER" 
                : "YOU LOST - GAME OVER";
            
            gm.ShowGameOver(message);
        }
        else
        {
            Debug.LogError("[CLIENT] GameManager not found!");
        }
    }

    public bool IsPlayerTurn(PlayerRef player)
    {
        if (LastPlayerId == PlayerRef.None) return true;
        return LastPlayerId != player;
    }

    public int GetPlayerEnergy(PlayerRef player)
    {
        if (player == Player1Ref) return Player1Energy;
        if (player == Player2Ref) return Player2Energy;
        return 0;
    }

    public int GetOpponentEnergy(PlayerRef player)
    {
        if (player == Player1Ref) return Player2Energy;
        if (player == Player2Ref) return Player1Energy;
        return 0;
    }

    public int GetPlayerDeckSelection(PlayerRef player)
    {
        if (!IsReady()) return 0;
        if (PlayerDeckSelection.TryGet(player, out int deckIndex)) return deckIndex;
        return 0;
    }

    private void ModifyEnergy(PlayerRef player, int amount)
    {
        if (!Object.HasStateAuthority) return;

        if (player == Player1Ref) Player1Energy = Mathf.Max(0, Player1Energy + amount);
        else if (player == Player2Ref) Player2Energy = Mathf.Max(0, Player2Energy + amount);
    }

    private string GetCharacterName(int deckIndex)
    {
        if (deckIndex == 0) return "Hornet";
        if (deckIndex == 1) return "Pure Vessel";
        return "Unknown";
    }

    public int GetTotalAttack(PlayerRef player)
    {
        int baseAttack = GetPlayerStats(player).Attack;
        int permanentBonus = statsManager.GetPermanentAttackBonus(player);
        return baseAttack + permanentBonus;
    }

    public PlayerStatsManager StatsManager => statsManager;
    public TurnManager TurnManagerRef => turnManager;
}