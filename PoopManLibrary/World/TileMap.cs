using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopManLibrary.World;
public class TileMap
{
    // ── Struttura dati mappa ─────────────────────────────────────────────
    private TileAtlas atlas;
    private TileType[,] map;            // Tipo logico di ogni tile (Wall/Empty/Breakable)
    private string[,] tileVariant;      // Nome sprite da disegnare (es. "glass0", "log1")

    public const int TileSize = 32;     // Dimensione in pixel di ogni tile
    public const int Rows = 23;         // Altezza mappa in tile
    public const int Cols = 39;         // Larghezza mappa in tile
    private int currentLevel;

    // ── Tema mappa ────────────────────────────────────────────────────────
    public enum MapTheme { Forest, Cave, Stone, Desert }
    public MapTheme Theme { get; private set; }

    // Nomi tile per ogni tema: { wall (pilastri), breakable[], empty[], border }
    // Forest  → tronchi/legno su erba (glass = erba chiara)
    // Cave    → pietra pesante su pavimento scuro (ground2)
    // Stone   → mix pietra/plank su suolo neutro (ground0/1)
    // Desert  → plank/log su sabbia (sand0)
    private static readonly Dictionary<MapTheme, (string wall, string[] breakable, string[] empty, string border)> ThemeTiles = new()
    {
        [MapTheme.Forest]  = ("wall2",  new[]{"log0","log1","plank0","plank1"}, new[]{"glass0","glass1","glass2"}, "wall1"),
        [MapTheme.Cave]    = ("wall2",  new[]{"stone0","stone1","stone2"},        new[]{"ground2"},                  "wall2"),
        [MapTheme.Stone]   = ("wall0",  new[]{"stone0","stone1","plank0"},        new[]{"ground0","ground1"},        "wall1"),
        [MapTheme.Desert]  = ("wall1",  new[]{"plank0","plank1","log0"},          new[]{"sand0"},                   "wall0"),
    };

    // Colore sfondo (clear) per ogni tema
    private static readonly Dictionary<MapTheme, Color> ThemeBackground = new()
    {
        [MapTheme.Forest] = new Color(  8, 22,  8),
        [MapTheme.Cave]   = new Color(  8,  6, 14),
        [MapTheme.Stone]  = new Color( 16, 14, 20),
        [MapTheme.Desert] = new Color( 28, 20,  8),
    };

    public Color BackgroundColor => ThemeBackground[Theme];

    /// <summary>
    /// Evento lanciato ogni volta che un tile Breakable viene distrutto.
    /// Game1 si iscrive per gestire i drop delle casse.
    /// </summary>
    public event Action<Point>? TileBroken;

    // ────────────────────────────────────────────────────────────────────
    // Costruttore: genera la mappa procedurale per il livello dato.
    // Pulisce tutti e 4 gli angoli per garantire spawn sempre liberi.
    // ────────────────────────────────────────────────────────────────────
    public TileMap(TileAtlas atlas, int rows, int cols, int level)
    {
        this.atlas = atlas;
        this.currentLevel = level;
        map = new TileType[rows, cols];
        tileVariant = new string[rows, cols];

        // Tema: cambia ogni 3 livelli (0-2=Forest, 3-5=Cave, 6-8=Stone, 9+=Desert)
        Theme = (level / 3) switch
        {
            0 => MapTheme.Forest,
            1 => MapTheme.Cave,
            2 => MapTheme.Stone,
            _ => MapTheme.Desert,
        };

        Random rand = new Random();

        // Genera il layout base della mappa
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                GenerateTile(y, x, rows, cols, rand);

        // Pulisce tutti e 4 gli angoli: il miner può spawnare in qualsiasi angolo
        Point[] corners = {
                new Point(1, 1),
                new Point(37, 1),
                new Point(1, 21),
                new Point(37, 21)
            };
        foreach (var corner in corners)
            ClearSpawnArea(rows, cols, rand, corner);

        // Aggiunge elementi ambientali dopo il cleanup (per non sovrascrivere gli angoli)
        if (Theme == MapTheme.Forest)
        {
            AddWaterZones(rows, cols, rand);
            AddSandNearWater(rows, cols, rand);
        }
        else if (Theme == MapTheme.Desert)
        {
            AddDesertDunes(rows, cols, rand);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Pulisce un'area 3×3 centrata su spawn, rimuovendo i breakable.
    // I muri della griglia fissa (y%2==0 && x%2==0) non vengono toccati.
    // ────────────────────────────────────────────────────────────────────
    private void ClearSpawnArea(int rows, int cols, Random rand, Point spawn)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int y = spawn.Y + dy;
                int x = spawn.X + dx;

                // Ignora tile fuori dalla mappa o sul bordo
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;

                // Non sovrascrivere i pilastri fissi della griglia Bomberman
                if (y % 2 == 0 && x % 2 == 0) continue;

                if (map[y, x] == TileType.Breakable)
                {
                    map[y, x] = TileType.Empty;
                    var t = ThemeTiles[Theme];
                    tileVariant[y, x] = t.empty[new Random().Next(t.empty.Length)];
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Genera il tipo di un singolo tile seguendo le regole classiche
    // di Bomberman: bordi e griglia fissa sono muri, il resto è
    // 60% breakable (log) e 40% empty (glass).
    // ────────────────────────────────────────────────────────────────────
    private void GenerateTile(int y, int x, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];

        // Bordo esterno: sempre muro indistruttibile
        if (y == 0 || y == rows - 1 || x == 0 || x == cols - 1)
        {
            map[y, x] = TileType.Wall;
            tileVariant[y, x] = t.border;
            return;
        }

        // Griglia fissa di pilastri (stile Bomberman classico)
        if (y % 2 == 0 && x % 2 == 0)
        {
            map[y, x] = TileType.Wall;
            tileVariant[y, x] = t.wall;
            return;
        }

        // Tile interni: 60% ostacolo distruttibile, 40% spazio libero
        if (rand.Next(100) < 60)
        {
            map[y, x] = TileType.Breakable;
            tileVariant[y, x] = t.breakable[rand.Next(t.breakable.Length)];
        }
        else
        {
            map[y, x] = TileType.Empty;
            tileVariant[y, x] = t.empty[rand.Next(t.empty.Length)];
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Aggiunge 2-4 zone d'acqua con forma organica casuale,
    // lontano dalla zona di spawn (partenza da y>=4, x>=4).
    // ────────────────────────────────────────────────────────────────────
    private void AddWaterZones(int rows, int cols, Random rand)
    {
        int waterZones = rand.Next(2, 5);
        for (int i = 0; i < waterZones; i++)
        {
            int startY = rand.Next(4, rows - 3);
            int startX = rand.Next(4, cols - 3);
            int waterSize = rand.Next(8, 20);
            GenerateWaterBlob(startY, startX, waterSize, rows, cols, rand);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Genera un blob d'acqua con crescita casuale a partire da un punto.
    // I pilastri fissi della griglia non vengono sovrascritti.
    // ────────────────────────────────────────────────────────────────────
    private void GenerateWaterBlob(int startY, int startX, int size, int rows, int cols, Random rand)
    {
        HashSet<(int, int)> waterTiles = new();
        Queue<(int, int)> toProcess = new();

        toProcess.Enqueue((startY, startX));
        waterTiles.Add((startY, startX));

        while (toProcess.Count > 0 && waterTiles.Count < size)
        {
            var (y, x) = toProcess.Dequeue();

            int[] dy = { -1, 1, 0, 0 };
            int[] dx = { 0, 0, -1, 1 };

            // Mescola le direzioni per forme organiche
            for (int i = 0; i < 4; i++)
            {
                int swapIdx = rand.Next(4);
                (dy[i], dy[swapIdx]) = (dy[swapIdx], dy[i]);
                (dx[i], dx[swapIdx]) = (dx[swapIdx], dx[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                int ny = y + dy[i];
                int nx = x + dx[i];

                if (ny > 0 && ny < rows - 1 && nx > 0 && nx < cols - 1
                    && !waterTiles.Contains((ny, nx)))
                {
                    if (rand.Next(100) < 65) // 65% di espansione
                    {
                        waterTiles.Add((ny, nx));
                        toProcess.Enqueue((ny, nx));
                    }
                }
            }
        }

        // Applica i tile d'acqua (tratta come Wall non distruttibile)
        foreach (var (y, x) in waterTiles)
        {
            if (!(y % 2 == 0 && x % 2 == 0)) // Non sovrascrivere pilastri
            {
                map[y, x] = TileType.Wall;
                tileVariant[y, x] = "water" + rand.Next(2);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Aggiunge sabbia sui tile empty adiacenti all'acqua (50% probabilità).
    // Solo il tileVariant viene cambiato, il tipo logico rimane Empty.
    // ────────────────────────────────────────────────────────────────────
    private void AddSandNearWater(int rows, int cols, Random rand)
    {
        for (int y = 1; y < rows - 1; y++)
            for (int x = 1; x < cols - 1; x++)
                if (map[y, x] == TileType.Empty && HasAdjacentWater(y, x, rows, cols))
                    if (rand.Next(2) == 0)
                        tileVariant[y, x] = "sand0";
    }

    /// <summary>Controlla se almeno uno dei 4 tile adiacenti è acqua.</summary>
    private bool HasAdjacentWater(int y, int x, int rows, int cols)
    {
        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int ny = y + dy[i];
            int nx = x + dx[i];
            if (ny > 0 && ny < rows - 1 && nx > 0 && nx < cols - 1)
                if (tileVariant[ny, nx].StartsWith("water"))
                    return true;
        }
        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    // Aggiunge macchie di sabbia sparse nel tema Desert.
    // ────────────────────────────────────────────────────────────────────
    private void AddDesertDunes(int rows, int cols, Random rand)
    {
        for (int y = 1; y < rows - 1; y++)
            for (int x = 1; x < cols - 1; x++)
                if (map[y, x] == TileType.Empty && rand.Next(100) < 40)
                    tileVariant[y, x] = "sand0";
    }

    // ────────────────────────────────────────────────────────────────────
    // Disegna tutti i tile della mappa usando l'atlas.
    // ────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch spriteBatch)
    {
        for (int y = 0; y < map.GetLength(0); y++)
            for (int x = 0; x < map.GetLength(1); x++)
            {
                Rectangle sourceRect = atlas.GetTile(tileVariant[y, x]);
                Rectangle destRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                spriteBatch.Draw(atlas.Texture, destRect, sourceRect, Color.White);
            }
    }

    /// <summary>true se il tile è dentro la mappa e di tipo Empty (percorribile).</summary>
    public bool IsWalkable(Point tile)
    {
        if (tile.X < 0 || tile.X >= map.GetLength(1) ||
            tile.Y < 0 || tile.Y >= map.GetLength(0))
            return false;
        return map[tile.Y, tile.X] == TileType.Empty;
    }

    /// <summary>true se le coordinate rientrano nei limiti della mappa.</summary>
    public bool IsInside(Point tile)
    {
        return tile.X >= 0 && tile.X < map.GetLength(1) &&
               tile.Y >= 0 && tile.Y < map.GetLength(0);
    }

    /// <summary>Restituisce il tipo del tile. Ritorna Wall se fuori dalla mappa.</summary>
    public TileType GetTile(Point tile)
    {
        if (!IsInside(tile)) return TileType.Wall;
        return map[tile.Y, tile.X];
    }

    // ────────────────────────────────────────────────────────────────────
    // Distrugge un tile Breakable, lo converte in Empty e notifica
    // i sottoscrittori tramite l'evento TileBroken (usato per i drop casse).
    // ────────────────────────────────────────────────────────────────────
    public void BreakTile(Point tile)
    {
        if (!IsInside(tile)) return;
        if (map[tile.Y, tile.X] != TileType.Breakable) return;

        map[tile.Y, tile.X] = TileType.Empty;

        var t = ThemeTiles[Theme];
        // Se adiacente all'acqua usa il primo empty del tema, altrimenti il secondo
        tileVariant[tile.Y, tile.X] = HasAdjacentWater(tile.Y, tile.X, map.GetLength(0), map.GetLength(1))
            ? t.empty[0]
            : t.empty[new Random().Next(t.empty.Length)];

        TileBroken?.Invoke(tile);
    }
}