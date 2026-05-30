using Microsoft.Xna.Framework;

namespace PoopMan.GameObjects;

/// <summary>Dati di testo e colore per le card dell'enciclopedia degli upgrade del Miner.</summary>
internal readonly record struct MinerUpgradeEntry(
    string Name,
    string Category,
    Color Color,
    string EffectType,
    string Description,
    string[] Effects,
    int MaxLevel,
    bool IsMythic = false);

/// <summary>
///     Catalogo delle descrizioni degli upgrade del Miner per l'enciclopedia.
///     Separato dalla logica di gioco: modificare qui non influenza il gameplay.
/// </summary>
internal static class MinerUpgradeDescriptions
{
    // --------------------------------------------------------------------
    // UPGRADE STANDARD
    // --------------------------------------------------------------------
    internal static readonly MinerUpgradeEntry[] Standard =
    {
        // -- Vita ------------------------------------
        new("+1 VITA", "VITA", new Color(100, 220, 100), "PASSIVA",
            "Guadagni subito una vita extra. Sempre disponibile se non sei al massimo.",
            new[] { "Lv 1: +1 vita immediata" }, int.MaxValue),
        new("VITA MAX+", "VITA", new Color(50, 200, 80), "PASSIVA",
            $"Aumenta il limite massimo di vite e guadagni subito 1 vita extra. Max {UpgradeRegistry.MaxLifeSteps} lv.",
            new[] { "Ogni lv: cap vite +1 e +1 vita immediata" }, UpgradeRegistry.MaxLifeSteps),
        new("RIGENERAZIONE", "VITA", new Color(80, 240, 120), "PASSIVA",
            "Recuperi automaticamente 1 vita ogni 5 livelli completati.",
            new[] { "Lv 1: +1 vita ogni 5 livelli" }, 1),

        // -- Offensivi -------------------------------
        new("DANNO +", "OFFENSIVO", new Color(255, 80, 40), "BOMBA",
            $"Aumenta il raggio esplosione di 1 tile in ogni direzione. Max {UpgradeRegistry.MaxExplosionRange} lv.",
            new[] { "Ogni lv: raggio +1 tile" }, UpgradeRegistry.MaxExplosionRange),
        new("POTENZA", "OFFENSIVO", new Color(255, 120, 0), "BOMBA",
            $"Le bombe normali infliggono piu danni per colpo ai pipistrelli. Max {UpgradeRegistry.MaxExplosionDamageSteps} lv.",
            new[] { "Ogni lv: +1 danno per esplosione" }, UpgradeRegistry.MaxExplosionDamageSteps),
        new("MICCIA CORTA", "OFFENSIVO", new Color(255, 200, 0), "BOMBA",
            $"Le bombe esplodono prima riducendo il tempo di attesa. Max {UpgradeRegistry.MaxFasterBombSteps} lv.",
            new[] { "Ogni lv: -0.4 s timer bomba" }, UpgradeRegistry.MaxFasterBombSteps),
        new("BOMBA +", "OFFENSIVO", new Color(255, 160, 0), "BOMBA",
            $"Puoi posizionare una bomba in piu contemporaneamente. Max {UpgradeRegistry.MaxExtraBombs} lv.",
            new[] { "Ogni lv: +1 bomba simultanea" }, UpgradeRegistry.MaxExtraBombs),
        new("CATENA", "OFFENSIVO", new Color(255, 100, 80), "SPECIALE",
            $"Ogni kill ha una probabilita di generare una mini-esplosione a catena. Max {UpgradeRegistry.MaxChainSteps} lv.",
            new[] { "Ogni lv: +15% chance esplosione catena" }, UpgradeRegistry.MaxChainSteps),

        // -- Movimento -------------------------------
        new("VELOCITA'", "MOVIMENTO", new Color(80, 180, 255), "PASSIVA",
            $"Il Miner si muove piu rapidamente sulla mappa. Max {UpgradeRegistry.MaxMoveSteps} lv.",
            new[] { "Ogni lv: +20 px/s velocita" }, UpgradeRegistry.MaxMoveSteps),
        new("ADRENALINA", "MOVIMENTO", new Color(100, 200, 255), "REATTIVA",
            "Dopo aver subito un colpo la velocita aumenta temporaneamente.",
            new[] { "Lv 1: velocita +40% per 3 s dopo danno subito" }, 1),

        // -- Difensivi -------------------------------
        new("RESISTENZA", "DIFENSIVO", new Color(180, 100, 255), "PASSIVA",
            $"La durata dell'invincibilita dopo il respawn aumenta. Max cumulativo {UpgradeRegistry.MaxInvincibility} s.",
            new[] { "Ogni lv: +1 s invincibilita dopo respawn" }, UpgradeRegistry.MaxInvincibility),
        new("ARMATURA", "DIFENSIVO", new Color(100, 150, 255), "PASSIVA",
            $"Riduce i danni ricevuti aumentando la finestra di invincibilita. Max cumulativo {UpgradeRegistry.MaxInvincibility} s.",
            new[] { "Ogni lv: +0.5 s invincibilita dopo danno" }, UpgradeRegistry.MaxInvincibility),
        new("SCUDO", "DIFENSIVO", new Color(200, 200, 200), "ATTIVA",
            "Assorbe 1 colpo senza subire danni. Si ricarica ogni 3 livelli superati.",
            new[] { "Lv 1: scudo attivo che assorbe 1 colpo" }, 1),

        // -- Speciali --------------------------------
        new("PASS-THROUGH", "SPECIALE", new Color(0, 220, 220), "BOMBA",
            "Le esplosioni attraversano i blocchi distruttibili senza fermarsi.",
            new[] { "Lv 1: esplosioni ignorano i blocchi breakable" }, 1),
        new("CRITICO", "SPECIALE", new Color(255, 220, 0), "PASSIVA",
            "Probabilita di uccidere istantaneamente un pipistrello al contatto.",
            new[] { "Lv 1: 20% chance kill al contatto (doppi punti)" }, 1),
        new("CALAMITA", "SPECIALE", new Color(255, 100, 200), "PASSIVA",
            "Raccoglie automaticamente gli item nelle vicinanze senza doverci camminare sopra.",
            new[] { "Lv 1: raccoglie item entro 3 tile automaticamente" }, 1),
        new("SHOCKWAVE", "SPECIALE", new Color(220, 180, 0), "BOMBA",
            "I pipistrelli vicini all'esplosione vengono storditi brevemente.",
            new[] { "Lv 1: stordimento 1.5 s sui bat adiacenti all'area" }, 1),
        new("RALLENTA", "SPECIALE", new Color(160, 80, 220), "BOMBA",
            "I pipistrelli vicini all'esplosione vengono rallentati temporaneamente.",
            new[] { "Lv 1: rallentamento 40% per 3 s sui bat adiacenti" }, 1),
        new("FORTUNA", "SPECIALE", new Color(255, 215, 0), "LOOT",
            $"Aumenta la probabilita di trovare bonus nelle casse distrutte. Max {UpgradeRegistry.MaxBonusLootSteps} lv.",
            new[] { "Ogni lv: +15% chance item dalle casse" }, UpgradeRegistry.MaxBonusLootSteps),
        new("BOTTINO", "SPECIALE", new Color(240, 240, 180), "LOOT",
            $"I pipistrelli uccisi hanno una probabilita di lasciare un item. Max {UpgradeRegistry.MaxDoubleDropSteps} lv.",
            new[] { "Ogni lv: +15% chance drop da kill" }, UpgradeRegistry.MaxDoubleDropSteps),

        // -- Mythic ----------------------------------
        new("IMMORTALITA MISTICA", "MYTHIC", new Color(220, 180, 30), "DIFESA",
            "Upgrade Mythic rarissimo (0,5%). Il Miner diventa immune a tutte le esplosioni delle proprie bombe.",
            new[]
            {
                "Immunita totale alle esplosioni delle bombe piccole del Miner",
                "Immunita totale alle esplosioni delle bombe grandi del Miner",
                "Immunita totale alle esplosioni speciali originate dal Miner",
                "LIMITE: non protegge da pipistrelli, Walid, Nuke o nemici speciali",
                "LIMITE: pericoli ambientali continuano a infliggere danno normalmente"
            }, 1, IsMythic: true),
        new("KILL ISTANTANEO", "MYTHIC", new Color(255, 80, 80), "BOMBA",
            "Upgrade Mythic rarissimo (0,5%). Le bombe piccole eliminano istantaneamente qualsiasi pipistrello.",
            new[]
            {
                "Le bombe piccole uccidono all'istante qualsiasi bat normale o speciale",
                "L'effetto ignora completamente i punti vita del nemico",
                "Si applica a tutti i pipistrelli colpiti dall'area di esplosione",
                "LIMITE: l'effetto vale solo per le bombe piccole",
                "LIMITE: bomba grande, Walid, Nuke e attacchi speciali NON sono influenzati"
            }, 1, IsMythic: true),
    };

    // Mythic entries are now included at the end of Standard above.
}
