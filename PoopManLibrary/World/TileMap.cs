using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using GameTime = Microsoft.Xna.Framework.GameTime;

namespace PoopManLibrary.World;

public class TileMap
{
    public enum MapTheme
    {
        Forest,
        Cave,
        Lava,
        Ice,
        Swamp,
        Ruins
    }

    private const float LiquidFrameInterval = 0.55f; // secondi per frame

    public const int TileSize = 32;

    /// <summary>Ogni quanti livelli si passa al bioma successivo.</summary>
    public const int LevelsPerBiome = 5;
    public const int Rows = 23;
    public const int Cols = 39;

    private static readonly Dictionary<MapTheme, (string wall, string[] breakable, string[] empty, string border)>
        ThemeTiles = new()
        {
            [MapTheme.Forest] = ("brick_wall0", new[] { "log0", "log1", "plank0", "plank1" },
                new[] { "glass0", "glass1", "glass2" }, "brick_wall1"),
            [MapTheme.Cave] = ("stone_brick_wall", new[] { "cobblestone", "mossy_cobblestone", "dirt_stone" },
                new[] { "ground2" }, "stone_brick_wall"),
            [MapTheme.Lava] = ("magma_brick_wall", new[] { "cobblestone"},
                new[] { "magma_stone1", "magma_stone0" }, "stone_brick_wall"),
            [MapTheme.Ice] = ("ice_brick_wall", new[] { "log0", "log1", "plank0", "plank1" },
                new[] { "ice_glass0", "ice_glass1", "ice_glass2" }, "ice_brick_wall"),
            [MapTheme.Swamp] = ("mossy_brick_wall", new[] { "log0", "log1", "plank0", "plank1" },
                new[] { "ground2", "ground0" }, "mossy_brick_wall"),
            [MapTheme.Ruins] = ("mossy_brick_wall",
                new[] { "dirt_stone", "cobblestone", "mossy_cobblestone" },
                new[] { RuinsSand, RuinsPaving }, "stone_brick_wall")
        };

    // ── Rovine: linguaggio visivo ─────────────────────────────────────────
    // Mattoni = indistruttibili, pietre tonde = distruttibili, sabbia/lastricato = pavimento.
    private const string RuinsSand = "sand0";
    private const string RuinsPaving = "ground2";
    private const string RuinsRubble = "dirt_stone";
    private const string RuinsMasonry = "cobblestone";
    private const string RuinsMossyMasonry = "mossy_cobblestone";
    private const string RuinsTempleColumn = "brick_wall1";
    private const string RuinsStump = "log1";
    private static readonly string[] RuinsWeeds = { "glass0", "glass1", "glass2" };

    private enum RuinsZone
    {
        Open,
        Avenue,
        Plaza,
        Room,
        RoomWall
    }

    private static readonly Dictionary<MapTheme, Color> ThemeBackground = new()
    {
        [MapTheme.Forest] = new Color(8, 22, 8),
        [MapTheme.Cave] = new Color(8, 6, 14),
        [MapTheme.Lava] = new Color(28, 6, 4),
        [MapTheme.Ice] = new Color(4, 16, 38),
        [MapTheme.Swamp] = new Color(4, 12, 6),
        [MapTheme.Ruins] = new Color(18, 14, 10)
    };

    private static readonly Point[] SpawnCorners =
    {
        new(1, 1), new(37, 1), new(1, 21), new(37, 21)
    };

    private readonly Point _playerSpawn;
    private readonly TileAtlas atlas;
    private readonly TileType[,] map;
    private readonly string[,] tileVariant;

    // Pavimento "sotto" ogni tile (solo Rovine): rompendo un blocco riappare il pavimento
    // coerente con la zona (sabbia / lastricato) invece di una variante casuale.
    private string[,] floorVariant;

    // ── Animazione liquidi ────────────────────────────────────────────────
    private float _liquidAnimTimer;
    private int _liquidFrame;
    private int currentLevel;

    // -------------------------------------------------------------------------
    // Costruttore principale.
    // Algoritmo:
    //   1) Bordi + pilastri fissi + riempimento uniforme
    //   2) Zona 3x3 libera solo attorno allo spawn del miner
    //   3) Elementi ambientali bioma
    //   4) Protezione pericoli attorno allo spawn attivo
    //   5) Re-enforce zona spawn dopo ambiente
    // -------------------------------------------------------------------------
    public TileMap(TileAtlas atlas, int rows, int cols, int level, Point? playerSpawn = null)
    {
        this.atlas = atlas;
        currentLevel = level;
        map = new TileType[rows, cols];
        tileVariant = new string[rows, cols];

        Theme = (level / LevelsPerBiome % 6) switch
        {
            0 => MapTheme.Forest,
            1 => MapTheme.Cave,
            2 => MapTheme.Lava,
            3 => MapTheme.Ice,
            4 => MapTheme.Swamp,
            _ => MapTheme.Ruins
        };

        var rand = new Random();
        var t = ThemeTiles[Theme];

        var breakableChance = BreakableChance(level);

        // 1) Riempimento completo uniforme con pavimento decorato
        // Genera una "mappa di zona" 2D con Perlin-like noise per variare i floor tile
        var noiseMap = GenerateNoiseMap(rows, cols, rand);

        for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++)
                if (y == 0 || y == rows - 1 || x == 0 || x == cols - 1)
                {
                    map[y, x] = TileType.Wall;
                    tileVariant[y, x] = t.border;
                }
                else if (y % 2 == 0 && x % 2 == 0)
                {
                    map[y, x] = TileType.Wall;
                    tileVariant[y, x] = t.wall;
                }
                else if (rand.Next(100) < breakableChance)
                {
                    map[y, x] = TileType.Breakable;
                    // Varia il breakable tile in base alla zona (noise)
                    var n = noiseMap[y, x];
                    var breakIdx = n < 0.4f ? 0 :
                        n < 0.75f ? t.breakable.Length > 1 ? 1 : 0 :
                        t.breakable.Length > 2 ? 2 : 0;
                    tileVariant[y, x] = t.breakable[breakIdx % t.breakable.Length];
                }
                else
                {
                    map[y, x] = TileType.Empty;
                    // Scegli il floor tile in base alla zona noise per varietà visiva
                    var n = noiseMap[y, x];
                    var emptyIdx = n < 0.35f ? 0 : n < 0.70f ? t.empty.Length > 1 ? 1 : 0 : t.empty.Length > 2 ? 2 : 0;
                    tileVariant[y, x] = t.empty[emptyIdx % t.empty.Length];
                }

        var chosenSpawn = playerSpawn ?? SpawnCorners[rand.Next(SpawnCorners.Length)];
        _playerSpawn = chosenSpawn;

        // 2) Zona 7x7 libera solo attorno allo spawn del miner
        ClearSpawnZone(chosenSpawn, rows, cols, rand);

        // 3) Elementi ambientali bioma
        AddBiomeEnvironment(rows, cols, rand, level);

        // 4) Protegge lo spawn attivo da pericoli liquidi
        ProtectSpawnFromHazards(rows, cols, chosenSpawn);

        // 5) Re-enforce zona spawn (l'ambiente potrebbe averla sovrascritta)
        ClearSpawnZone(chosenSpawn, rows, cols, rand);
    }

    public MapTheme Theme { get; }

    // Densita breakable: 35% base, cresce con il livello fino a 75
    private static int BreakableChance(int level)
    {
        return Math.Clamp(35 + level * 2, 35, 75);
    }

    private string FloorAt(int y, int x, Random rand)
    {
        var empty = ThemeTiles[Theme].empty;
        return floorVariant?[y, x] ?? empty[rand.Next(empty.Length)];
    }

    public Color BackgroundColor => ThemeBackground[Theme];

    public event Action<Point>? TileBroken;

    // -------------------------------------------------------------------------
    // Svuota una zona 5x5 (raggio 2) attorno allo spawn del miner.
    // -------------------------------------------------------------------------
    private void ClearSpawnZone(Point spawn, int rows, int cols, Random rand)
    {
        for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var cy = spawn.Y + dy;
                var cx = spawn.X + dx;
                if (cy <= 0 || cy >= rows - 1 || cx <= 0 || cx >= cols - 1) continue;
                if (cy % 2 == 0 && cx % 2 == 0) continue; // pilastri fissi: indistruttibili
                if (map[cy, cx] != TileType.Empty)
                {
                    map[cy, cx] = TileType.Empty;
                    tileVariant[cy, cx] = FloorAt(cy, cx, rand);
                }
            }
    }

    // -------------------------------------------------------------------------
    // Rimuove pericoli (acqua/lava) in un raggio 5x5 attorno allo spawn attivo.
    // -------------------------------------------------------------------------
    private void ProtectSpawnFromHazards(int rows, int cols, Point spawn)
    {
        var t = ThemeTiles[Theme];
        for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var y = spawn.Y + dy;
                var x = spawn.X + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Wall && IsHazardVariant(tileVariant[y, x]))
                {
                    map[y, x] = TileType.Empty;
                    tileVariant[y, x] = floorVariant?[y, x] ?? t.empty[0];
                }
            }
    }

    private static bool IsHazardVariant(string variant)
    {
        return variant.StartsWith("water") || variant.StartsWith("lava") || variant.StartsWith("swamp_water");
    }

    // -------------------------------------------------------------------------
    // Aggiunge elementi ambientali specifici per ogni bioma.
    // -------------------------------------------------------------------------
    private void AddBiomeEnvironment(int rows, int cols, Random rand, int level)
    {
        var intensity = Math.Clamp(level / 5, 0, 4);

        switch (Theme)
        {
            case MapTheme.Forest:
                for (var i = 0; i < rand.Next(2, 4 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(2, rows - 2), rand.Next(2, cols - 2),
                        rand.Next(6, 12 + intensity * 2), "water", rows, cols, rand);
                AddSandNearLiquid(rows, cols, rand);
                for (var i = 0; i < rand.Next(2, 4 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 4), rows, cols, rand);
                break;

            case MapTheme.Cave:
                for (var i = 0; i < rand.Next(3, 5 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(3, 6 + intensity), rows, cols, rand);
                for (var i = 0; i < rand.Next(3, 6 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);
                break;

            case MapTheme.Lava:
                for (var i = 0; i < rand.Next(2, 4 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(2, rows - 2), rand.Next(2, cols - 2),
                        rand.Next(6, 12 + intensity * 2), "lava", rows, cols, rand);
                for (var i = 0; i < rand.Next(2, 4 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 5), rows, cols, rand);
                break;

            case MapTheme.Ice:
                // 1) Lastre di ghiaccio aperte (aree sgombrate per permettere movimento)
                for (var i = 0; i < rand.Next(5, 8 + intensity); i++)
                    CarveOpenArea(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(3, 6 + intensity), rows, cols, rand);

                // 2) Stagni di ghiaccio (water tinted azzurro in Draw)
                for (var i = 0; i < rand.Next(4, 7 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(6, 12 + intensity), "water", rows, cols, rand);

                // 3) Stalattiti/cristalli di ghiaccio (molti cluster colonnari)
                for (var i = 0; i < rand.Next(10, 16 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);

                // 4) Rocce ghiacciate sparse (stone cluster → aspetto grigio-blu grazie al tint)
                for (var i = 0; i < rand.Next(4, 7 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(1, 3), rows, cols, rand);

                // 5) Pavimento ghiacciato: quasi tutto ice_glass (superficie scivolosa)
                for (var y = 1; y < rows - 1; y++)
                    for (var x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Empty)
                            tileVariant[y, x] = "ice_glass" + rand.Next(3);

                break;

            case MapTheme.Swamp:
                // Grandi pozze di acqua melmosa
                for (var i = 0; i < rand.Next(4, 7 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(10, 20 + intensity * 2), "swamp_water", rows, cols, rand);
                // Transizione fango attorno all'acqua
                AddSandNearLiquid(rows, cols, rand);
                // Alberi morti e vegetazione palustre (rock cluster = tronchi/radici)
                for (var i = 0; i < rand.Next(4, 6 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 4 + intensity), rows, cols, rand);
                // Piante palustri verticali (cluster colonnari)
                for (var i = 0; i < rand.Next(3, 6 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);
                // Pavimento fangoso: colora i tile empty verso ground2/ground0 (più scuri)
                for (var y = 1; y < rows - 1; y++)
                    for (var x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Empty && rand.Next(100) < 45)
                            tileVariant[y, x] = rand.Next(2) == 0 ? "ground2" : "ground0";
                break;

            case MapTheme.Ruins:
                BuildRuins(rows, cols, rand, level);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Rovine: ricostruisce l'intera mappa come una città sepolta nella sabbia.
    //   1) Viale orizzontale + verticale lastricati, piazza con fontana all'incrocio
    //   2) 4-6 edifici in rovina: mura di pietra distruttibili con crolli e porte,
    //      pavimento lastricato e colonne di arenaria all'interno
    //   3) Fuori dagli edifici: dune di sabbia con campi di macerie (noise),
    //      lastre affioranti qua e là
    // Tutte le pareti nuove sono Breakable: la raggiungibilità non cambia.
    // -------------------------------------------------------------------------
    private void BuildRuins(int rows, int cols, Random rand, int level)
    {
        floorVariant = new string[rows, cols];
        var zone = new RuinsZone[rows, cols];
        var pavingNoise = GenerateNoiseMap(rows, cols, rand);
        var rubbleNoise = GenerateNoiseMap(rows, cols, rand);
        var density = BreakableChance(level) / 100f;

        // 1) Viali su righe/colonne dispari (niente pilastri sul percorso)
        var avenueRow = 9 + 2 * rand.Next(3); // 9, 11, 13
        var avenueCol = 15 + 2 * rand.Next(5); // 15..23
        for (var x = 1; x < cols - 1; x++) zone[avenueRow, x] = RuinsZone.Avenue;
        for (var y = 1; y < rows - 1; y++) zone[y, avenueCol] = RuinsZone.Avenue;
        for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
                zone[avenueRow + dy, avenueCol + dx] = RuinsZone.Plaza;

        // 2) Edifici: 1-2 per ciascuno dei 4 quartieri delimitati dai viali.
        //    Angoli su coordinate dispari, così i muri sono linee continue
        //    e i pilastri fissi finiscono all'interno come colonnato.
        var rooms = new List<(int x0, int y0, int x1, int y1)>();
        var quarters = new[]
        {
            (xMin: 3, xMax: avenueCol - 2, yMin: 3, yMax: avenueRow - 2),
            (xMin: avenueCol + 2, xMax: cols - 4, yMin: 3, yMax: avenueRow - 2),
            (xMin: 3, xMax: avenueCol - 2, yMin: avenueRow + 2, yMax: rows - 4),
            (xMin: avenueCol + 2, xMax: cols - 4, yMin: avenueRow + 2, yMax: rows - 4)
        };
        foreach (var q in quarters)
        {
            var target = rand.Next(1, 3);
            var placed = 0;
            for (var attempt = 0; attempt < 30 && placed < target; attempt++)
            {
                var w = 6 + 2 * rand.Next(3); // 6, 8, 10
                var h = 4 + 2 * rand.Next(2); // 4, 6
                if (w > q.xMax - q.xMin || h > q.yMax - q.yMin) continue;
                var x0 = q.xMin + 2 * rand.Next((q.xMax - q.xMin - w) / 2 + 1);
                var y0 = q.yMin + 2 * rand.Next((q.yMax - q.yMin - h) / 2 + 1);
                var x1 = x0 + w;
                var y1 = y0 + h;

                // Non invadere la piazza
                if (x0 <= avenueCol + 2 && x1 >= avenueCol - 2 && y0 <= avenueRow + 2 && y1 >= avenueRow - 2) continue;
                // Lontano dallo spawn del miner
                if (x0 <= _playerSpawn.X + 4 && x1 >= _playerSpawn.X - 4 &&
                    y0 <= _playerSpawn.Y + 4 && y1 >= _playerSpawn.Y - 4) continue;
                // Almeno un corridoio tra due edifici
                if (rooms.Any(r => x0 <= r.x1 + 2 && x1 >= r.x0 - 2 && y0 <= r.y1 + 2 && y1 >= r.y0 - 2)) continue;

                rooms.Add((x0, y0, x1, y1));
                placed++;
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                        zone[y, x] = y == y0 || y == y1 || x == x0 || x == x1 ? RuinsZone.RoomWall : RuinsZone.Room;
            }
        }

        // 3) Crolli: su gran parte dei lati un intero tratto di muro è venuto giù
        var collapsed = new bool[rows, cols];
        foreach (var (x0, y0, x1, y1) in rooms)
        {
            foreach (var y in new[] { y0, y1 })
            {
                if (rand.Next(100) >= 50) continue;
                var len = rand.Next(2, 5);
                var start = rand.Next(x0 + 1, x1 - len + 1);
                for (var i = 0; i < len; i++) collapsed[y, start + i] = true;
            }

            foreach (var x in new[] { x0, x1 })
            {
                if (rand.Next(100) >= 50) continue;
                var len = rand.Next(2, Math.Min(5, y1 - y0));
                var start = rand.Next(y0 + 1, y1 - len + 1);
                for (var i = 0; i < len; i++) collapsed[start + i, x] = true;
            }
        }

        bool NextToCollapse(int y, int x)
        {
            return collapsed[y - 1, x] || collapsed[y + 1, x] || collapsed[y, x - 1] || collapsed[y, x + 1];
        }

        // 4) Pavimento + contenuto di ogni tile in base alla zona
        var driftNoise = GenerateNoiseMap(rows, cols, rand);
        var weedNoise = GenerateNoiseMap(rows, cols, rand);
        for (var y = 1; y < rows - 1; y++)
            for (var x = 1; x < cols - 1; x++)
            {
                var z = zone[y, x];

                // Lastricato solo dove c'era la città, mezzo sepolto dalla sabbia portata dal vento;
                // erbacce e ciuffi d'erba spuntano tra le pietre nei punti riparati.
                var built = z != RuinsZone.Open;
                var floor = built
                    ? driftNoise[y, x] > 0.7f ? RuinsSand : RuinsPaving
                    : pavingNoise[y, x] > 0.75f ? RuinsPaving : RuinsSand;
                if (built && weedNoise[y, x] > 0.6f && rand.Next(100) < 22)
                    floor = RuinsWeeds[rand.Next(RuinsWeeds.Length)];
                floorVariant[y, x] = floor;

                if (y % 2 == 0 && x % 2 == 0)
                {
                    // Colonne di arenaria negli edifici, in parte invase dall'edera; fuori solo edera
                    tileVariant[y, x] = z is RuinsZone.Room or RuinsZone.Plaza && driftNoise[y, x] < 0.55f
                        ? RuinsTempleColumn
                        : ThemeTiles[Theme].wall;
                    continue;
                }

                var rubbleChance = z switch
                {
                    RuinsZone.Plaza => 0.08f,
                    RuinsZone.Room => 0.16f,
                    RuinsZone.Avenue => 0.25f,
                    RuinsZone.RoomWall => collapsed[y, x] ? 0.45f : 0.92f,
                    // Macerie riversate fuori dai muri crollati
                    _ when NextToCollapse(y, x) => 0.45f,
                    // Dune aperte dove il noise è basso, campi di macerie dove è alto
                    _ => Math.Min(0.95f, density * (0.15f + 1.2f * rubbleNoise[y, x]))
                };

                if (rand.NextDouble() < rubbleChance)
                {
                    map[y, x] = TileType.Breakable;
                    // Pietra squadrata solo per i tratti di muro ancora in piedi;
                    // dentro gli edifici qualche ceppo d'albero cresciuto tra le rovine.
                    tileVariant[y, x] = z switch
                    {
                        RuinsZone.RoomWall when !collapsed[y, x] => rand.Next(100) < 60 ? RuinsMossyMasonry : RuinsMasonry,
                        RuinsZone.Room when rand.Next(100) < 25 => RuinsStump,
                        _ => RuinsRubble
                    };
                }
                else
                {
                    map[y, x] = TileType.Empty;
                    tileVariant[y, x] = floorVariant[y, x];
                }
            }

        // 5) Porte: una per lato, sempre su riga/colonna dispari (mai davanti a un pilastro)
        foreach (var (x0, y0, x1, y1) in rooms)
        {
            var doorX = x0 + 2 * rand.Next(1, (x1 - x0) / 2);
            var doorY = y0 + 2 * rand.Next(1, (y1 - y0) / 2);
            foreach (var (dx, dy) in new[] { (doorX, y0), (doorX, y1), (x0, doorY), (x1, doorY) })
            {
                map[dy, dx] = TileType.Empty;
                tileVariant[dy, dx] = floorVariant[dy, dx];
            }
        }

        // 6) Fontana abbandonata al centro della piazza: acqua stagnante tra le 4 colonne
        map[avenueRow, avenueCol] = TileType.Wall;
        tileVariant[avenueRow, avenueCol] = "swamp_water0";
    }

    // -------------------------------------------------------------------------
    // Genera una noise map smooth (bilinear interpolation su griglia grossolana)
    // per variare i tile di floor e breakable in zone coerenti.
    // -------------------------------------------------------------------------
    private static float[,] GenerateNoiseMap(int rows, int cols, Random rand)
    {
        // Griglia di controllo grossolana (ogni ~6 tile)
        var gCols = cols / 6 + 2;
        var gRows = rows / 6 + 2;
        var coarse = new float[gRows, gCols];
        for (var y = 0; y < gRows; y++)
            for (var x = 0; x < gCols; x++)
                coarse[y, x] = (float)rand.NextDouble();

        var result = new float[rows, cols];
        for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++)
            {
                var gx = x / 6f;
                var gy = y / 6f;
                var x0 = (int)gx;
                var y0 = (int)gy;
                var x1 = Math.Min(x0 + 1, gCols - 1);
                var y1 = Math.Min(y0 + 1, gRows - 1);
                var tx = gx - x0;
                var ty = gy - y0;
                var v = coarse[y0, x0] * (1 - tx) * (1 - ty)
                        + coarse[y0, x1] * tx * (1 - ty)
                        + coarse[y1, x0] * (1 - tx) * ty
                        + coarse[y1, x1] * tx * ty;
                result[y, x] = v;
            }

        return result;
    }

    // -------------------------------------------------------------------------
    // Genera un blob di liquido con crescita organica BFS.
    // -------------------------------------------------------------------------
    private void GenerateLiquidBlob(int startY, int startX, int size, string variant, int rows, int cols, Random rand)
    {
        // Non generare liquido entro 7 tile dallo spawn del miner
        if (Math.Abs(startY - _playerSpawn.Y) <= 3 && Math.Abs(startX - _playerSpawn.X) <= 3) return;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startY, startX));
        visited.Add((startY, startX));

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        while (queue.Count > 0 && visited.Count < size)
        {
            var (y, x) = queue.Dequeue();
            for (var i = 3; i > 0; i--)
            {
                var j = rand.Next(i + 1);
                (dy[i], dy[j]) = (dy[j], dy[i]);
                (dx[i], dx[j]) = (dx[j], dx[i]);
            }

            for (var i = 0; i < 4; i++)
            {
                var ny = y + dy[i];
                var nx = x + dx[i];
                if (ny <= 0 || ny >= rows - 1 || nx <= 0 || nx >= cols - 1) continue;
                if (visited.Contains((ny, nx))) continue;
                if (ny % 2 == 0 && nx % 2 == 0) continue;
                if (Math.Abs(ny - _playerSpawn.Y) <= 3 && Math.Abs(nx - _playerSpawn.X) <= 3) continue;
                if (rand.Next(100) < 65)
                {
                    visited.Add((ny, nx));
                    queue.Enqueue((ny, nx));
                }
            }
        }

        foreach (var (y, x) in visited)
        {
            map[y, x] = TileType.Wall;
            tileVariant[y, x] = variant + rand.Next(2);
        }
    }

    private void AddColumnCluster(int cy, int cx, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];
        int[] oys = { 0, 1, -1 };
        var count = rand.Next(1, 3);
        for (var i = 0; i < count; i++)
        {
            var y = cy + oys[i];
            var x = cx;
            if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
            if (y % 2 == 0 && x % 2 == 0) continue;
            if (map[y, x] == TileType.Empty)
            {
                map[y, x] = TileType.Breakable;
                tileVariant[y, x] = t.breakable[rand.Next(t.breakable.Length)];
            }
            // Skip liquid tiles — never place breakable over water/lava/swamp
        }
    }

    private void AddRockCluster(int cy, int cx, int radius, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dy * dy + dx * dx > radius * radius) continue;
                var y = cy + dy;
                var x = cx + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Empty && rand.Next(100) < 60)
                {
                    map[y, x] = TileType.Breakable;
                    tileVariant[y, x] = t.breakable[rand.Next(t.breakable.Length)];
                }
                else if (map[y, x] == TileType.Wall && IsHazardVariant(tileVariant[y, x]))
                {
                    // Never place breakable on liquid tiles
                }
            }
    }

    private void CarveOpenArea(int cy, int cx, int radius, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dy * dy + dx * dx > radius * radius) continue;
                var y = cy + dy;
                var x = cx + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Breakable && rand.Next(100) < 70)
                {
                    map[y, x] = TileType.Empty;
                    tileVariant[y, x] = t.empty[rand.Next(t.empty.Length)];
                }
            }
    }

    private void AddSandNearLiquid(int rows, int cols, Random rand)
    {
        for (var y = 1; y < rows - 1; y++)
            for (var x = 1; x < cols - 1; x++)
                if (map[y, x] == TileType.Empty && HasAdjacentLiquid(y, x, rows, cols))
                    if (rand.Next(2) == 0)
                        tileVariant[y, x] = "sand0";
    }

    private bool HasAdjacentLiquid(int y, int x, int rows, int cols)
    {
        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };
        for (var i = 0; i < 4; i++)
        {
            var ny = y + dy[i];
            var nx = x + dx[i];
            if (ny > 0 && ny < rows - 1 && nx > 0 && nx < cols - 1)
                if (IsHazardVariant(tileVariant[ny, nx]))
                    return true;
        }

        return false;
    }

    private HashSet<Point> FloodFill(Point start, int rows, int cols)
    {
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        if (!IsWalkable(start)) return visited;

        queue.Enqueue(start);
        visited.Add(start);

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            for (var i = 0; i < 4; i++)
            {
                var next = new Point(p.X + dx[i], p.Y + dy[i]);
                if (!visited.Contains(next) && IsWalkable(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

    // -------------------------------------------------------------------------
    // Restituisce tutte le tile raggiungibili da <start> camminando sulle tile
    // Empty e (opzionalmente) attraversando i Breakable, che il giocatore può
    // distruggere con le bombe. I Wall (bordi, pilastri, acqua, lava) bloccano.
    // Usato per garantire che porta e chiave non finiscano in zone isolate.
    // -------------------------------------------------------------------------
    public HashSet<Point> GetReachableTiles(Point start, bool throughBreakables = true)
    {
        var visited = new HashSet<Point>();
        if (!IsInside(start) || map[start.Y, start.X] == TileType.Wall) return visited;

        var queue = new Queue<Point>();
        queue.Enqueue(start);
        visited.Add(start);

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            for (var i = 0; i < 4; i++)
            {
                var next = new Point(p.X + dx[i], p.Y + dy[i]);
                if (visited.Contains(next) || !IsInside(next)) continue;
                var tt = map[next.Y, next.X];
                if (tt == TileType.Wall) continue;
                if (tt == TileType.Breakable && !throughBreakables) continue;
                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return visited;
    }

    // -------------------------------------------------------------------------
    // Restituisce un tile Empty casuale lontano dagli spawn.
    // -------------------------------------------------------------------------
    public Point? GetRandomWalkableTile(Random rand, int minDistFromSpawn = 4)
    {
        var candidates = new List<Point>();
        for (var y = 1; y < map.GetLength(0) - 1; y++)
            for (var x = 1; x < map.GetLength(1) - 1; x++)
            {
                if (map[y, x] != TileType.Empty) continue;
                var p = new Point(x, y);
                var tooClose = SpawnCorners.Any(c =>
                    Math.Abs(c.X - x) + Math.Abs(c.Y - y) < minDistFromSpawn);
                if (!tooClose) candidates.Add(p);
            }

        if (candidates.Count == 0) return null;
        return candidates[rand.Next(candidates.Count)];
    }

    public void Update(GameTime gameTime)
    {
        _liquidAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_liquidAnimTimer >= LiquidFrameInterval)
        {
            _liquidAnimTimer -= LiquidFrameInterval;
            _liquidFrame = 1 - _liquidFrame; // alterna 0 ↔ 1
        }
    }

    private string AnimatedVariant(string variant)
    {
        if (variant == "water0" || variant == "water1")
            return $"water{_liquidFrame}";
        if (variant == "swamp_water0" || variant == "swamp_water1")
            return $"swamp_water{_liquidFrame}";
        if (variant == "lava0" || variant == "lava1")
            return $"lava{_liquidFrame}";
        return variant;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        for (var y = 0; y < map.GetLength(0); y++)
            for (var x = 0; x < map.GetLength(1); x++)
            {
                var variant = AnimatedVariant(tileVariant[y, x]);
                var sourceRect = atlas.GetTile(variant);
                var destRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                spriteBatch.Draw(atlas.Texture, destRect, sourceRect, Color.White);
            }
    }

    public bool IsWalkable(Point tile)
    {
        if (tile.X < 0 || tile.X >= map.GetLength(1) ||
            tile.Y < 0 || tile.Y >= map.GetLength(0))
            return false;
        return map[tile.Y, tile.X] == TileType.Empty;
    }

    public bool IsInside(Point tile)
    {
        return tile.X >= 0 && tile.X < map.GetLength(1) &&
               tile.Y >= 0 && tile.Y < map.GetLength(0);
    }

    /// <summary>True per acqua / lava / palude (tile Wall ma "piatte": non proiettano ombra).</summary>
    public bool IsLiquid(Point tile)
    {
        return IsInside(tile) && IsHazardVariant(tileVariant[tile.Y, tile.X]);
    }

    /// <summary>True se la tile è lava (sorgente di luce).</summary>
    public bool IsLava(Point tile)
    {
        return IsInside(tile) && tileVariant[tile.Y, tile.X].StartsWith("lava");
    }

    /// <summary>
    ///     True per le tile "alte" (muri, pilastri, blocchi distruttibili) che proiettano
    ///     ombra sul pavimento. Liquidi e pavimento sono piatti.
    /// </summary>
    public bool IsRaised(Point tile)
    {
        if (!IsInside(tile)) return false;
        var t = map[tile.Y, tile.X];
        return t == TileType.Breakable || (t == TileType.Wall && !IsHazardVariant(tileVariant[tile.Y, tile.X]));
    }

    public TileType GetTile(Point tile)
    {
        if (!IsInside(tile)) return TileType.Wall;
        return map[tile.Y, tile.X];
    }

    public void BreakTile(Point tile)
    {
        if (!IsInside(tile)) return;
        if (map[tile.Y, tile.X] != TileType.Breakable) return;

        map[tile.Y, tile.X] = TileType.Empty;

        var t = ThemeTiles[Theme];
        tileVariant[tile.Y, tile.X] = floorVariant?[tile.Y, tile.X] ??
                                      (HasAdjacentLiquid(tile.Y, tile.X, map.GetLength(0), map.GetLength(1))
                                          ? t.empty[0]
                                          : t.empty[new Random().Next(t.empty.Length)]);

        TileBroken?.Invoke(tile);
    }
}