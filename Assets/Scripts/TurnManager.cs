using Fusion;
using UnityEngine;

// gestioneaza turele si card speed logic
public class TurnManager
{
    private GameNetworkHandler handler;

    public TurnManager(GameNetworkHandler h)
    {
        handler = h;
    }

    // valideaza daca poate juca carte
    public bool ValidateCardPlay(PlayerRef player, int characterOwner, int energyCost)
    {
        if (!handler.CanPlayerPlayCards(player))
        {
            handler.RPC_NotifyCannotPlayMoreCards(player, "");
            return false;
        }

        int playerDeck = handler.GetPlayerDeckSelection(player);
        if (playerDeck != characterOwner)
        {
            handler.RPC_NotifyCardRejected(player, "", "", "");
            return false;
        }

        int currentEnergy = handler.GetPlayerEnergy(player);
        if (currentEnergy < energyCost)
        {
            handler.RPC_NotifyNotEnoughEnergy(player, "", energyCost, currentEnergy);
            return false;
        }

        return true;
    }

    // gestioneaza turele in functie de card speed
    public void HandleCardSpeed(PlayerRef player, int cardSpeed, string cardName)
    {
        switch (cardSpeed)
        {
            case 0: // SLOW - termina tura
                handler.TurnCount++;

                if (handler.TurnCount % 2 == 0)
                {
                    handler.CurrentRound++;
                    handler.StatsManager.TickDot(handler.Player1Ref);
                    handler.StatsManager.TickDot(handler.Player2Ref);
                    handler.Player1Energy += handler.CurrentRound + handler.StatsManager.GetEnergyPerRoundBonus(handler.Player1Ref);
                    handler.Player2Energy += handler.CurrentRound + handler.StatsManager.GetEnergyPerRoundBonus(handler.Player2Ref);
                }

                handler.LastPlayerId = player;
                handler.SetPlayerUsedNonBurstCard(handler.Player1Ref, false);
                handler.SetPlayerUsedNonBurstCard(handler.Player2Ref, false);
                break;

            case 1: // FAST - poate ataca/skip dar nu mai poate juca carti
                handler.SetPlayerUsedNonBurstCard(player, true);
                break;

            case 2: // BURST - poate face orice
                break;
        }
    }
}