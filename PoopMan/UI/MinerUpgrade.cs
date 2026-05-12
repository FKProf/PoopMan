using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace PoopMan.UI;

/// <summary>Tutti i potenziamenti selezionabili dal giocatore.</summary>
public enum UpgradeType
{
    // ── Vita ─────────────────────────────────────────────────────────────
    ExtraLife,          // +1 vita immediata
    MaxLifeUp,          // aumenta il cap max delle vite
    SlowRegen,          // recupera 1 vita ogni 10 livelli

    // ── Offensivi ─────────────────────────────────────────────────────────
    IncreasedDamage,    // raggio esplosione +1 tile   (max 4)
    BiggerBlast,        // alias visivo di IncreasedDamage
    FasterBomb,         // timer bomba -0.4 s           (max 1.6 s)
    ExtraBomb,          // +1 bomba simultanea          (max 6)
    ChainExplosion,     // esplosioni a catena tra nemici uccisi

    // ── Movimento ────────────────────────────────────────────────────────
    FasterMovement,     // velocità +20 px/s            (max 280)
    DashAfterHit,       // velocità +40% per 3 s dopo danni subiti

    // ── Difensivi ────────────────────────────────────────────────────────
    ExplosionResistance,// invincibilità dopo respawn +1 s
    DamageReduction,    // +0.5 s invincibilità
    Shield,             // assorbe 1 colpo ogni N livelli

    // ── Speciali ─────────────────────────────────────────────────────────
    MultiHit,           // esplosioni ignorano i breakable (pass-through)
    CriticalChance,     // 20% critico al contatto
    Magnet,             // raccoglie item entro 3 tile automaticamente
    StunOnHit,          // bat adiacenti all'esplosione vengono storditi 1.5 s
    SlowOnHit,          // bat adiacenti all'esplosione rallentano del 40% per 3 s
    BonusLoot,          // +15% probabilità di trovare bonus nelle casse
    DoubleDrop,         // i bat uccisi lasciano sempre un item (invece che a caso)
}

/// <summary>Dati di presentazione di un upgrade.</summary>
public sealed class UpgradeDef
{
    public UpgradeType Type;
    public string      Name;
    public string      Description;
    public Color       Color;

    public UpgradeDef(UpgradeType type, string name, string description, Color color)
    {
        Type        = type;
        Name        = name;
        Description = description;
        Color       = color;
    }
}

/// <summary>Registro statico di tutti gli upgrade disponibili.</summary>
public static class UpgradeRegistry
{
    // ── Frequenza menu ────────────────────────────────────────────────────
    public const int EveryNLevels = 3;

    // ── Limiti massimi per upgrade cumulativi ─────────────────────────────
    public const int MaxExplosionRange  = 4;   // bonus tile raggio (base 1 piccola / 2 grande)
    public const int MaxExtraBombs      = 3;   // bombe extra oltre le 3 base → max 6
    public const int MaxFasterBombSteps = 4;   // 4 × 0.4 s = 1.6 s di riduzione max
    public const int MaxMoveSteps       = 6;   // 6 × 20 px/s = +120 → cap 280
    public const int MaxInvincibility   = 7;   // secondi totali invincibilità
    public const int MaxLifeSteps       = 3;   // max 3 aumenti vita max (default 5 → max 8)
    public const int MaxChainSteps      = 4;   // 4 × 15% → 60% max probabilità catena
    public const int MaxDoubleDropSteps = 4;   // 4 × 15% → 60% max probabilità doppio loot

    private static readonly UpgradeDef[] All =
    {
        // ── Vita ──────────────────────────────────────────────────────────
        new(UpgradeType.ExtraLife,
            "+1 VITA",
            "Guadagni subito una vita extra.",
            Color.LightGreen),

        new(UpgradeType.MaxLifeUp,
            "VITA MAX+",
            $"Il limite max di vite sale di 1\ne guadagni 1 vita. Max {MaxLifeSteps}x.",
            Color.MediumSpringGreen),

        new(UpgradeType.SlowRegen,
            "RIGENERAZIONE",
            "Recuperi 1 vita ogni 10 livelli.",
            Color.PaleGreen),

        // ── Offensivi ─────────────────────────────────────────────────────
        new(UpgradeType.IncreasedDamage,
            "DANNO +",
            $"Raggio esplosione +1 tile.\nMax {MaxExplosionRange} volte.",
            Color.OrangeRed),

        new(UpgradeType.BiggerBlast,
            "DEFLAGRAZIONE",
            $"Tutte le esplosioni +1 raggio.\nMax {MaxExplosionRange} volte.",
            Color.Tomato),

        new(UpgradeType.FasterBomb,
            "MICCIA CORTA",
            $"Bombe esplodono prima (-0.4 s).\nMax {MaxFasterBombSteps} volte.",
            Color.Gold),

        new(UpgradeType.ExtraBomb,
            "BOMBA +",
            $"+1 bomba simultanea.\nMax {MaxExtraBombs} volte (tot. 6).",
            Color.Orange),

        new(UpgradeType.ChainExplosion,
            "CATENA",
            $"+15% probabilita' che un bat ucciso\ngeneri una mini-esplosione. Max {MaxChainSteps}x.",
            Color.Coral),

        // ── Movimento ─────────────────────────────────────────────────────
        new(UpgradeType.FasterMovement,
            "VELOCITA'",
            $"Miner piu' veloce (+20 px/s).\nMax {MaxMoveSteps} volte.",
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
            "Assorbe 1 colpo senza danni.\nSi ricarica ogni 5 livelli.",
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
            "Bat vicini all'esplosione\nsono storditi per 1.5 s.",
            Color.Goldenrod),

        new(UpgradeType.SlowOnHit,
            "RALLENTA",
            "Bat vicini all'esplosione\nrallentano del 40% per 3 s.",
            Color.MediumPurple),

        new(UpgradeType.BonusLoot,
            "FORTUNA",
            "+15% probabilita' di trovare\nbonus nelle casse.",
            Color.Gold),

        new(UpgradeType.DoubleDrop,
            "BOTTINO",
            $"+15% probabilita' che un bat ucciso\nlasci un item. Max {MaxDoubleDropSteps}x.",
            Color.LightYellow),
    };

    /// <summary>Restituisce <paramref name="count"/> upgrade casuali distinti.</summary>
    public static List<UpgradeDef> PickRandom(int count = 3)
    {
        var rng = new Random();
        return All.OrderBy(_ => rng.Next()).Take(Math.Min(count, All.Length)).ToList();
    }
}
