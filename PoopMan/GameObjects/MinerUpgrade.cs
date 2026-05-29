using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace PoopMan.GameObjects;

/// <summary>Tutti i potenziamenti selezionabili dal giocatore.</summary>
public enum UpgradeType
{
    // ── Vita ─────────────────────────────────────────────────────────────
    ExtraLife, // +1 vita immediata (sempre disponibile se vite < max)
    MaxLifeUp, // aumenta il cap max delle vite
    SlowRegen, // recupera 1 vita ogni 5 livelli

    // ── Offensivi ─────────────────────────────────────────────────────────
    IncreasedDamage, // raggio esplosione +1 tile (max 4)
    ExplosionDamage, // danno esplosioni: +1 danno per ogni livello upgrade (max 5)
    FasterBomb, // timer bomba -0.4 s (max 4 livelli)
    ExtraBomb, // +1 bomba simultanea (max 3 livelli)
    ChainExplosion, // esplosioni a catena tra nemici uccisi (max 4)

    // ── Movimento ────────────────────────────────────────────────────────
    FasterMovement, // velocità +20 px/s (max 6 livelli)
    DashAfterHit, // velocità +40% per 3 s dopo danni subiti (1 livello)

    // ── Difensivi ────────────────────────────────────────────────────────
    ExplosionResistance, // invincibilità dopo respawn +1 s (max cumulativo)
    DamageReduction, // +0.5 s invincibilità (max cumulativo)
    Shield, // assorbe 1 colpo ogni 3 livelli (1 livello)

    // ── Speciali ─────────────────────────────────────────────────────────
    MultiHit, // esplosioni ignorano i breakable (1 livello)
    CriticalChance, // 20% critico al contatto (1 livello)
    Magnet, // raccoglie item entro 3 tile (1 livello)
    StunOnHit, // bat storditi vicino all'esplosione (1 livello)
    SlowOnHit, // bat rallentati vicino all'esplosione (1 livello)
    BonusLoot, // +15% probabilità bonus casse (max 4)
    DoubleDrop // bat uccisi lasciano sempre item (max 4)
}

/// <summary>Dati di presentazione di un upgrade.</summary>
public sealed class UpgradeDef
{
    public Color Color;
    public string Description;
    public string Name;
    public UpgradeType Type;

    public UpgradeDef(UpgradeType type, string name, string description, Color color)
    {
        Type = type;
        Name = name;
        Description = description;
        Color = color;
    }
}

/// <summary>Registro statico di tutti gli upgrade disponibili.</summary>
public static class UpgradeRegistry
{
    // ── Frequenza menu ────────────────────────────────────────────────────
    public const int EveryNLevels = 3;

    // ── Limiti massimi per upgrade cumulativi ─────────────────────────────
    public const int MaxExplosionRange = 4;
    public const int MaxExplosionDamageSteps = 5;
    public const int MaxExtraBombs = 3;
    public const int MaxFasterBombSteps = 4;
    public const int MaxMoveSteps = 6;
    public const int MaxInvincibility = 7;
    public const int MaxLifeSteps = 3;
    public const int MaxChainSteps = 4;
    public const int MaxDoubleDropSteps = 4;
    public const int MaxBonusLootSteps = 4;

    private static readonly UpgradeDef[] All =
    {
        // ── Vita ──────────────────────────────────────────────────────────
        new(UpgradeType.ExtraLife,
            "+1 VITA",
            "Guadagni subito una vita extra.",
            Color.LightGreen),

        new(UpgradeType.MaxLifeUp,
            "VITA MAX+",
            $"Il limite max di vite sale di 1\ne guadagni 1 vita. Max {MaxLifeSteps} lv.",
            Color.MediumSpringGreen),

        new(UpgradeType.SlowRegen,
            "RIGENERAZIONE",
            "Recuperi 1 vita ogni 5 livelli.",
            Color.PaleGreen),

        // ── Offensivi ─────────────────────────────────────────────────────
        new(UpgradeType.IncreasedDamage,
            "DANNO +",
            $"Raggio esplosione +1 tile.\nMax {MaxExplosionRange} lv.",
            Color.OrangeRed),

        new(UpgradeType.ExplosionDamage,
            "POTENZA",
            $"Le bombe normali fanno piu' danni.\n+1 danno per ogni lv (max {MaxExplosionDamageSteps} lv).",
            new Color(255, 90, 0)),

        new(UpgradeType.FasterBomb,
            "MICCIA CORTA",
            $"Bombe esplodono prima (-0.4 s).\nMax {MaxFasterBombSteps} lv.",
            Color.Gold),

        new(UpgradeType.ExtraBomb,
            "BOMBA +",
            $"+1 bomba simultanea.\nMax {MaxExtraBombs} lv.",
            Color.Orange),

        new(UpgradeType.ChainExplosion,
            "CATENA",
            $"+15% probabilita' che un bat ucciso\ngeneri una mini-esplosione. Max {MaxChainSteps} lv.",
            Color.Coral),

        // ── Movimento ─────────────────────────────────────────────────────
        new(UpgradeType.FasterMovement,
            "VELOCITA'",
            $"Miner piu' veloce (+20 px/s).\nMax {MaxMoveSteps} lv.",
            Color.DeepSkyBlue),

        new(UpgradeType.DashAfterHit,
            "ADRENALINA",
            "Dopo un colpo subito,\nvelocita' +40% per 3 secondi.",
            Color.CornflowerBlue),

        // ── Difensivi ─────────────────────────────────────────────────────
        new(UpgradeType.ExplosionResistance,
            "RESISTENZA",
            $"Invincibilita' respawn +1 s.\nMax {MaxInvincibility} s totali.",
            Color.Violet),

        new(UpgradeType.DamageReduction,
            "ARMATURA",
            $"Riduce danni: invincibilita'\n+0.5 s. Max {MaxInvincibility} s totali.",
            Color.SteelBlue),

        new(UpgradeType.Shield,
            "SCUDO",
            "Assorbe 1 colpo senza danni.\nSi ricarica ogni 3 livelli.",
            Color.Silver),

        // ── Speciali ──────────────────────────────────────────────────────
        new(UpgradeType.MultiHit,
            "PASS-THROUGH",
            "Esplosioni attraversano\ni blocchi distruttibili.",
            Color.Cyan),

        new(UpgradeType.CriticalChance,
            "CRITICO",
            "20% probabilita' di uccidere\nil bat al contatto (doppi punti).",
            Color.Yellow),

        new(UpgradeType.Magnet,
            "CALAMITA",
            "Raccoglie item entro 3 tile\nautomaticamente.",
            Color.HotPink),

        new(UpgradeType.StunOnHit,
            "SHOCKWAVE",
            "Pipistrelli vicini all'esplosione\nsono storditi per 1.5 s.",
            Color.Goldenrod),

        new(UpgradeType.SlowOnHit,
            "RALLENTA",
            "Pipistrelli vicini all'esplosione\nrallentano del 40% per 3 s.",
            Color.MediumPurple),

        new(UpgradeType.BonusLoot,
            "FORTUNA",
            $"+15% probabilita' di trovare\nbonus nelle casse. Max {MaxBonusLootSteps} lv.",
            Color.Gold),

        new(UpgradeType.DoubleDrop,
            "BOTTINO",
            $"+15% probabilita' che un bat ucciso\nlasci un item. Max {MaxDoubleDropSteps} lv.",
            Color.LightYellow)
    };

    /// <summary>
    ///     Restituisce il numero massimo di livelli per un dato upgrade.
    ///     Gli upgrade a livello 1 sono "unici": una volta presi non ricompaiono.
    /// </summary>
    public static int MaxLevel(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.ExtraLife => int.MaxValue, // sempre disponibile se vite < max
            UpgradeType.MaxLifeUp => MaxLifeSteps,
            UpgradeType.SlowRegen => 1,
            UpgradeType.IncreasedDamage => MaxExplosionRange,
            UpgradeType.ExplosionDamage => MaxExplosionDamageSteps,
            UpgradeType.FasterBomb => MaxFasterBombSteps,
            UpgradeType.ExtraBomb => MaxExtraBombs,
            UpgradeType.ChainExplosion => MaxChainSteps,
            UpgradeType.FasterMovement => MaxMoveSteps,
            UpgradeType.DashAfterHit => 1,
            UpgradeType.ExplosionResistance => MaxInvincibility,
            UpgradeType.DamageReduction => MaxInvincibility,
            UpgradeType.Shield => 1,
            UpgradeType.MultiHit => 1,
            UpgradeType.CriticalChance => 1,
            UpgradeType.Magnet => 1,
            UpgradeType.StunOnHit => 1,
            UpgradeType.SlowOnHit => 1,
            UpgradeType.BonusLoot => MaxBonusLootSteps,
            UpgradeType.DoubleDrop => MaxDoubleDropSteps,
            _ => 1
        };
    }

    /// <summary>
    ///     Restituisce <paramref name="count" /> upgrade casuali distinti,
    ///     escludendo quelli già al livello massimo.
    ///     currentLevels: dizionario livello attuale per tipo (0 = mai preso).
    /// </summary>
    public static List<UpgradeDef> PickRandom(
        int count,
        IReadOnlyDictionary<UpgradeType, int> currentLevels)
    {
        var rng = new Random();
        var available = All
            .Where(def =>
            {
                var cur = currentLevels.TryGetValue(def.Type, out var v) ? v : 0;
                var max = MaxLevel(def.Type);
                return cur < max;
            })
            .OrderBy(_ => rng.Next())
            .Take(Math.Min(count, All.Length))
            .ToList();
        return available;
    }

    /// <summary>Overload senza livelli (retrocompatibilità): considera tutti disponibili.</summary>
    public static List<UpgradeDef> PickRandom(int count = 3)
    {
        return PickRandom(count, new Dictionary<UpgradeType, int>());
    }
}