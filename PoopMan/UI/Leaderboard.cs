using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoopMan.UI;

/// <summary>Singolo record della classifica.</summary>
public sealed class LeaderboardEntry
{
    public string Name { get; set; } = "???";
    public int Score { get; set; }
    public int Level { get; set; }
    public string Date { get; set; } = "";
}

/// <summary>Gestisce la persistenza della classifica in un file JSON locale.</summary>
public static class LeaderboardManager
{
    private static readonly string FilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "leaderboard.json");

    private const int MaxEntries = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // Indice dell'ultima entry salvata (-1 se nessuna)
    public static int LastSavedIndex { get; private set; } = -1;

    private static List<LeaderboardEntry> _entries = new();

    // Record predefiniti sempre presenti in classifica
    private static readonly LeaderboardEntry[] SeedEntries =
    {
        new() { Name = "KJ-Ash", Score = 999999, Level = 999, Date = "26/12/1893" },
        new() { Name = "Criton", Score = 676767, Level = 67,  Date = "11/09/2001" },
    };

    /// <summary>Carica la classifica dal disco. Da chiamare all'avvio del gioco.</summary>
    public static void Load()
    {
        _entries = new List<LeaderboardEntry>();
        LastSavedIndex = -1;

        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, JsonOptions);
                if (loaded != null)
                    _entries = loaded;
            }
            catch
            {
                // File corrotto o illeggibile: si reimposta la classifica
                _entries = new List<LeaderboardEntry>();
            }
        }

        // Aggiunge i seed solo se non sono già presenti (confronto per nome)
        bool changed = false;
        foreach (var seed in SeedEntries)
        {
            if (!_entries.Any(e => e.Name == seed.Name))
            {
                _entries.Add(seed);
                changed = true;
            }
        }

        if (changed)
        {
            _entries = _entries
                .OrderByDescending(e => e.Score)
                .ThenByDescending(e => e.Level)
                .Take(MaxEntries)
                .ToList();
            Save();
        }
    }

    /// <summary>Restituisce la classifica ordinata dal punteggio più alto.</summary>
    public static IReadOnlyList<LeaderboardEntry> GetEntries() =>
        _entries.OrderByDescending(e => e.Score).ThenByDescending(e => e.Level).ToList();

    /// <summary>
    /// Aggiunge un nuovo record, ordina e mantiene al massimo <see cref="MaxEntries"/> voci.
    /// Aggiorna <see cref="LastSavedIndex"/> con la posizione del record appena inserito.
    /// </summary>
    public static void AddEntry(string name, int score, int level)
    {
        var entry = new LeaderboardEntry
        {
            Name = string.IsNullOrWhiteSpace(name) ? "???" : name.Trim(),
            Score = score,
            Level = level,
            Date = DateTime.Now.ToString("dd/MM/yyyy"),
        };

        _entries.Add(entry);
        _entries = _entries
            .OrderByDescending(e => e.Score)
            .ThenByDescending(e => e.Level)
            .Take(MaxEntries)
            .ToList();

        LastSavedIndex = _entries.IndexOf(entry);
        Save();
    }

    /// <summary>Cancella tutti i record e sovrascrive il file.</summary>
    public static void Clear()
    {
        _entries = new List<LeaderboardEntry>();
        LastSavedIndex = -1;
        Save();
    }

    private static void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // In caso di errore di scrittura non si crasha il gioco
        }
    }
}
