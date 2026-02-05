using Fusion;
using UnityEngine;

// gestioneaza efectele speciale ale cartilor
public class CardEffectsManager
{
    private GameNetworkHandler handler;

    public CardEffectsManager(GameNetworkHandler h)
    {
        handler = h;
    }

    // aplica efectul special pe baza cardID
    public void ApplySpecialEffect(string cardID, PlayerRef player, PlayerRef opponent)
    {
        switch (cardID)
        {
            case "thread_dance":
                handler.StatsManager.AddAttackBonus(player, 1);
                Debug.Log($"[CARD] thread_dance: +1 temp attack");
                break;

            case "hunter_mark":
                handler.StatsManager.AddAttackBonus(player, 3);
                Debug.Log($"[CARD] hunter_mark: +3 temp attack");
                break;

            case "silk_bind":
                handler.StatsManager.AddIncomingDamageReduction(player, 1);
                Debug.Log($"[CARD] silk_bind: -1 incoming damage");
                break;

            case "ascending_light":
                handler.StatsManager.AddPermanentAttack(player, 2);
                handler.StatsManager.AddEnergyPerRoundBonus(player, 1);
                Debug.Log($"[CARD] ascending_light: +2 perm attack, +1 energy/round");
                break;

            case "weave_song":
                handler.StatsManager.ApplyDot(opponent, 2, 2);
                Debug.Log($"[CARD] weave_song: 2 dmg/round for 2 rounds");
                break;
        }
    }
}