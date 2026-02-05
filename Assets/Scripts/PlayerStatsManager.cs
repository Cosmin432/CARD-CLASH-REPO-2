using Fusion;
using UnityEngine;

// gestioneaza HP, energie, buffs si debuffs
public class PlayerStatsManager
{
    private GameNetworkHandler handler;

    [Networked] public int Player1AttackBonus { get; set; }
    [Networked] public int Player2AttackBonus { get; set; }
    [Networked] public int Player1IncomingDamageReduction { get; set; }
    [Networked] public int Player2IncomingDamageReduction { get; set; }
    [Networked] public int Player1PermanentAttackBonus { get; set; }
    [Networked] public int Player2PermanentAttackBonus { get; set; }
    [Networked] public int Player1EnergyPerRoundBonus { get; set; }
    [Networked] public int Player2EnergyPerRoundBonus { get; set; }
    [Networked] public int Player1DotDamage { get; set; }
    [Networked] public int Player2DotDamage { get; set; }
    [Networked] public int Player1DotRoundsLeft { get; set; }
    [Networked] public int Player2DotRoundsLeft { get; set; }

    public PlayerStatsManager(GameNetworkHandler h)
    {
        handler = h;
    }

    // aplica damage
    public void ApplyDamage(PlayerRef player, int damage)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref)
        {
            var s = handler.Player1Stats;
            s.HP -= damage;
            handler.Player1Stats = s;
        }
        else if (player == handler.Player2Ref)
        {
            var s = handler.Player2Stats;
            s.HP -= damage;
            handler.Player2Stats = s;
        }
    }

    // calculeaza damage-ul final cu bonusuri
    public int CalculateDamage(PlayerRef attacker, PlayerRef opponent)
    {
        int baseAttack = handler.GetPlayerStats(attacker).Attack;
        int attackBonus = GetPlayerAttackBonus(attacker);
        int permanentBonus = GetPermanentAttackBonus(attacker);
        int rawDamage = baseAttack + attackBonus + permanentBonus;

        int damageReduction = GetIncomingDamageReduction(opponent);
        int finalDamage = Mathf.Max(0, rawDamage - damageReduction);

        return finalDamage;
    }

    public int GetPlayerAttackBonus(PlayerRef player)
    {
        if (player == handler.Player1Ref) return Player1AttackBonus;
        if (player == handler.Player2Ref) return Player2AttackBonus;
        return 0;
    }

    public void AddAttackBonus(PlayerRef player, int amount)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1AttackBonus += amount;
        else if (player == handler.Player2Ref) Player2AttackBonus += amount;
    }

    public void ClearAttackBonus(PlayerRef player)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1AttackBonus = 0;
        else if (player == handler.Player2Ref) Player2AttackBonus = 0;
    }

    public int GetIncomingDamageReduction(PlayerRef player)
    {
        if (player == handler.Player1Ref) return Player1IncomingDamageReduction;
        if (player == handler.Player2Ref) return Player2IncomingDamageReduction;
        return 0;
    }

    public void AddIncomingDamageReduction(PlayerRef player, int amount)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1IncomingDamageReduction += amount;
        else if (player == handler.Player2Ref) Player2IncomingDamageReduction += amount;
    }

    public void ClearIncomingDamageReduction(PlayerRef player)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1IncomingDamageReduction = 0;
        else if (player == handler.Player2Ref) Player2IncomingDamageReduction = 0;
    }

    public void AddPermanentAttack(PlayerRef player, int amount)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1PermanentAttackBonus += amount;
        else if (player == handler.Player2Ref) Player2PermanentAttackBonus += amount;
    }

    public int GetPermanentAttackBonus(PlayerRef player)
    {
        if (player == handler.Player1Ref) return Player1PermanentAttackBonus;
        if (player == handler.Player2Ref) return Player2PermanentAttackBonus;
        return 0;
    }

    public void AddEnergyPerRoundBonus(PlayerRef player, int amount)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (player == handler.Player1Ref) Player1EnergyPerRoundBonus += amount;
        else if (player == handler.Player2Ref) Player2EnergyPerRoundBonus += amount;
    }

    public int GetEnergyPerRoundBonus(PlayerRef player)
    {
        if (player == handler.Player1Ref) return Player1EnergyPerRoundBonus;
        if (player == handler.Player2Ref) return Player2EnergyPerRoundBonus;
        return 0;
    }

    // aplica DOT (damage over time)
    public void ApplyDot(PlayerRef target, int damagePerRound, int rounds)
    {
        if (!handler.Object.HasStateAuthority) return;

        if (target == handler.Player1Ref)
        {
            Player1DotDamage = damagePerRound;
            Player1DotRoundsLeft += rounds;
        }
        else if (target == handler.Player2Ref)
        {
            Player2DotDamage = damagePerRound;
            Player2DotRoundsLeft += rounds;
        }
    }

    // proceseaza DOT la inceputul rundei
    public void TickDot(PlayerRef player)
    {
        if (!handler.Object.HasStateAuthority) return;

        int damage = 0;
        int roundsLeft = 0;

        if (player == handler.Player1Ref)
        {
            damage = Player1DotDamage;
            roundsLeft = Player1DotRoundsLeft;
        }
        else if (player == handler.Player2Ref)
        {
            damage = Player2DotDamage;
            roundsLeft = Player2DotRoundsLeft;
        }

        if (roundsLeft <= 0 || damage <= 0) return;

        ApplyDamage(player, damage);
        roundsLeft--;

        if (player == handler.Player1Ref)
        {
            Player1DotRoundsLeft = roundsLeft;
            if (roundsLeft == 0) Player1DotDamage = 0;
        }
        else
        {
            Player2DotRoundsLeft = roundsLeft;
            if (roundsLeft == 0) Player2DotDamage = 0;
        }
    }
}