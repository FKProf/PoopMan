using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.Linq;
using GameTime = Microsoft.Xna.Framework.GameTime;

namespace PoopManLibrary.World;

public class TileMap
{
    private TileAtlas atlas;
    private TileType[,] map;
    private string[,] tileVariant;

    // ── Animazione liquidi ────────────────────────────────────────────────
    private float _liquidAnimTimer = 0f;
    private const float LiquidFrameInterval = 0.55f; // secondi per frame
    private int _liquidFrame = 0;

    public const int TileSize = 32;
    public const int Rows = 23;
    public const int Cols = 39;
    private int currentLevel;
    private Point _playerSpawn;

    public enum MapTheme { Forest, Cave, Lava, Ice, Swamp, Ruins }
    public MapTheme Theme { get; private set; }

    private static readonly Dictionary<MapTheme, (string wall, string[] breakable, string[] empty, string border)> ThemeTiles = new()
    {
        [MapTheme.Forest] = ("brick_wall0", new[] { "log0", "log1", "plank0", "plank1" }, new[] { "glass0", "glass1", "glass2" }, "brick_wall1"),
        [MapTheme.Cave] = ("stone_brick_wall", new[] { "cobblestone", "mossy_cobblestone", "dirt_stone" }, new[] { "ground2" }, "stone_brick_wall"),
        [MapTheme.Lava] = ("magma_brick_wall", new[] { "cobblestone", "magma_stone1" }, new[] { "magma_stone0", "magma_stone2" }, "stone_brick_wall"),
        [MapTheme.Ice] = ("ice_brick_wall", new[] { "log0", "log1", "plank0", "plank1" }, new[] { "ice_glass0", "ice_glass1", "ice_glass2" }, "ice_brick_wall"),
        [MapTheme.Swamp] = ("mossy_brick_wall", new[] { "log0", "log1", "plank0", "plank1" }, new[] { "ground2", "ground0" }, "mossy_brick_wall"),
        [MapTheme.Ruins] = ("mossy_brick_wall", new[] { "cobblestone", "mossy_cobblestone", "dirt_stone", "plank0" }, new[] { "ground0", "ground1", "sand0" }, "stone_brick_wall"),
    };

    private static readonly Dictionary<MapTheme, Color> ThemeBackground = new()
    {
        [MapTheme.Forest] = new Color(8, 22, 8),
        [MapTheme.Cave] = new Color(8, 6, 14),
        [MapTheme.Lava] = new Color(28, 6, 4),
        [MapTheme.Ice] = new Color(4, 16, 38),
        [MapTheme.Swamp] = new Color(4, 12, 6),
        [MapTheme.Ruins] = new Color(18, 14, 10),
    };

    public Color BackgroundColor => ThemeBackground[Theme];

    public event Action<Point>? TileBroken;

    private static readonly Point[] SpawnCorners = {
        new(1, 1), new(37, 1), new(1, 21), new(37, 21)
    };

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
        this.currentLevel = level;
        map = new TileType[rows, cols];
        tileVariant = new string[rows, cols];

        Theme = ((level / 4) % 6) switch
        {
            0 => MapTheme.Forest,
            1 => MapTheme.Cave,
            2 => MapTheme.Lava,
            3 => MapTheme.Ice,
            4 => MapTheme.Swamp,
            _ => MapTheme.Ruins,
        };

        var rand = new Random();
        var t = ThemeTiles[Theme];

        // Densita breakable: 35% base, cresce con il livello fino a 75
        int breakableChance = Math.Clamp(35 + level * 2, 35, 75);

        // 1) Riempimento completo uniforme con pavimento decorato
        // Genera una "mappa di zona" 2D con Perlin-like noise per variare i floor tile
        float[,] noiseMap = GenerateNoiseMap(rows, cols, rand);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
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
                    float n = noiseMap[y, x];
                    int breakIdx = n < 0.4f ? 0 : n < 0.75f ? (t.breakable.Length > 1 ? 1 : 0) : (t.breakable.Length > 2 ? 2 : 0);
                    tileVariant[y, x] = t.breakable[breakIdx % t.breakable.Length];
                }
                else
                {
                    map[y, x] = TileType.Empty;
                    // Scegli il floor tile in base alla zona noise per varietà visiva
                    float n = noiseMap[y, x];
                    int emptyIdx = n < 0.35f ? 0 : n < 0.70f ? (t.empty.Length > 1 ? 1 : 0) : (t.empty.Length > 2 ? 2 : 0);
                    tileVariant[y, x] = t.empty[emptyIdx % t.empty.Length];
                }
            }
        }

        Point chosenSpawn = playerSpawn ?? SpawnCorners[rand.Next(SpawnCorners.Length)];
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

    // -------------------------------------------------------------------------
    // Svuota una zona 5x5 (raggio 2) attorno allo spawn del miner.
    // -------------------------------------------------------------------------
    private void ClearSpawnZone(Point spawn, int rows, int cols, Random rand)
    {
        var theme = ThemeTiles[Theme];
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                int cy = spawn.Y + dy;
                int cx = spawn.X + dx;
                if (cy <= 0 || cy >= rows - 1 || cx <= 0 || cx >= cols - 1) continue;
                if (cy % 2 == 0 && cx % 2 == 0) continue; // pilastri fissi: indistruttibili
                if (map[cy, cx] != TileType.Empty)
                {
                    map[cy, cx] = TileType.Empty;
                    tileVariant[cy, cx] = theme.empty[rand.Next(theme.empty.Length)];
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Rimuove pericoli (acqua/lava) in un raggio 5x5 attorno allo spawn attivo.
    // -------------------------------------------------------------------------
    private void ProtectSpawnFromHazards(int rows, int cols, Point spawn)
    {
        var t = ThemeTiles[Theme];
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                int y = spawn.Y + dy;
                int x = spawn.X + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Wall && IsHazardVariant(tileVariant[y, x]))
                {
                    map[y, x] = TileType.Empty;
                    tileVariant[y, x] = t.empty[0];
                }
            }
        }
    }

    private static bool IsHazardVariant(string variant) =>
        variant.StartsWith("water") || variant.StartsWith("lava") || variant.StartsWith("swamp_water");

    // -------------------------------------------------------------------------
    // Aggiunge elementi ambientali specifici per ogni bioma.
    // -------------------------------------------------------------------------
    private void AddBiomeEnvironment(int rows, int cols, Random rand, int level)
    {
        int intensity = Math.Clamp(level / 5, 0, 4);

        switch (Theme)
        {
            case MapTheme.Forest:
                for (int i = 0; i < rand.Next(2, 4 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(2, rows - 2), rand.Next(2, cols - 2),
                        rand.Next(6, 12 + intensity * 2), "water", rows, cols, rand);
                AddSandNearLiquid(rows, cols, rand);
                for (int i = 0; i < rand.Next(2, 4 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 4), rows, cols, rand);
                break;

            case MapTheme.Cave:
                for (int i = 0; i < rand.Next(3, 5 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(3, 6 + intensity), rows, cols, rand);
                for (int i = 0; i < rand.Next(3, 6 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);
                break;

            case MapTheme.Lava:
                for (int i = 0; i < rand.Next(2, 4 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(2, rows - 2), rand.Next(2, cols - 2),
                        rand.Next(6, 12 + intensity * 2), "lava", rows, cols, rand);
                for (int i = 0; i < rand.Next(2, 4 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 5), rows, cols, rand);
                break;

            case MapTheme.Ice:
                // 1) Lastre di ghiaccio aperte (aree sgombrate per permettere movimento)
                for (int i = 0; i < rand.Next(5, 8 + intensity); i++)
                    CarveOpenArea(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(3, 6 + intensity), rows, cols, rand);

                // 2) Stagni di ghiaccio (water tinted azzurro in Draw)
                for (int i = 0; i < rand.Next(4, 7 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(6, 12 + intensity), "water", rows, cols, rand);

                // 3) Stalattiti/cristalli di ghiaccio (molti cluster colonnari)
                for (int i = 0; i < rand.Next(10, 16 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);

                // 4) Rocce ghiacciate sparse (stone cluster → aspetto grigio-blu grazie al tint)
                for (int i = 0; i < rand.Next(4, 7 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(1, 3), rows, cols, rand);

                // 5) Pavimento ghiacciato: quasi tutto ice_glass (superficie scivolosa)
                for (int y = 1; y < rows - 1; y++)
                    for (int x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Empty)
                            tileVariant[y, x] = "ice_glass" + rand.Next(3);

                // 6) Brina sui muri fissi: sostituisce ice_brick_wall con ice_glass per varietà gelida
                for (int y = 1; y < rows - 1; y++)
                    for (int x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Wall
                            && y % 2 == 0 && x % 2 == 0
                            && tileVariant[y, x] == "ice_brick_wall"
                            && rand.Next(100) < 40)
                            tileVariant[y, x] = "ice_glass" + rand.Next(3);
                break;

            case MapTheme.Swamp:
                // Grandi pozze di acqua melmosa
                for (int i = 0; i < rand.Next(4, 7 + intensity); i++)
                    GenerateLiquidBlob(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(10, 20 + intensity * 2), "swamp_water", rows, cols, rand);
                // Transizione fango attorno all'acqua
                AddSandNearLiquid(rows, cols, rand);
                // Alberi morti e vegetazione palustre (rock cluster = tronchi/radici)
                for (int i = 0; i < rand.Next(4, 6 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(2, 4 + intensity), rows, cols, rand);
                // Piante palustri verticali (cluster colonnari)
                for (int i = 0; i < rand.Next(3, 6 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);
                // Pavimento fangoso: colora i tile empty verso ground2/ground0 (più scuri)
                for (int y = 1; y < rows - 1; y++)
                    for (int x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Empty && rand.Next(100) < 45)
                            tileVariant[y, x] = rand.Next(2) == 0 ? "ground2" : "ground0";
                break;

            case MapTheme.Ruins:
                for (int y = 1; y < rows - 1; y++)
                    for (int x = 1; x < cols - 1; x++)
                        if (map[y, x] == TileType.Empty && rand.Next(100) < 25 + intensity * 5)
                            tileVariant[y, x] = "sand0";
                for (int i = 0; i < rand.Next(3, 5 + intensity); i++)
                    AddRockCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1),
                        rand.Next(3, 6 + intensity), rows, cols, rand);
                for (int i = 0; i < rand.Next(3, 6 + intensity); i++)
                    AddColumnCluster(rand.Next(1, rows - 1), rand.Next(1, cols - 1), rows, cols, rand);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Genera una noise map smooth (bilinear interpolation su griglia grossolana)
    // per variare i tile di floor e breakable in zone coerenti.
    // -------------------------------------------------------------------------
    private static float[,] GenerateNoiseMap(int rows, int cols, Random rand)
    {
        // Griglia di controllo grossolana (ogni ~6 tile)
        int gCols = cols / 6 + 2;
        int gRows = rows / 6 + 2;
        float[,] coarse = new float[gRows, gCols];
        for (int y = 0; y < gRows; y++)
            for (int x = 0; x < gCols; x++)
                coarse[y, x] = (float)rand.NextDouble();

        float[,] result = new float[rows, cols];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float gx = (float)x / 6f;
                float gy = (float)y / 6f;
                int x0 = (int)gx; int y0 = (int)gy;
                int x1 = Math.Min(x0 + 1, gCols - 1);
                int y1 = Math.Min(y0 + 1, gRows - 1);
                float tx = gx - x0; float ty = gy - y0;
                float v = coarse[y0, x0] * (1 - tx) * (1 - ty)
                        + coarse[y0, x1] * tx * (1 - ty)
                        + coarse[y1, x0] * (1 - tx) * ty
                        + coarse[y1, x1] * tx * ty;
                result[y, x] = v;
            }
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
            for (int i = 3; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (dy[i], dy[j]) = (dy[j], dy[i]);
                (dx[i], dx[j]) = (dx[j], dx[i]);
            }
            for (int i = 0; i < 4; i++)
            {
                int ny = y + dy[i];
                int nx = x + dx[i];
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
        int count = rand.Next(1, 3);
        for (int i = 0; i < count; i++)
        {
            int y = cy + oys[i];
            int x = cx;
            if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
            if (y % 2 == 0 && x % 2 == 0) continue;
            if (map[y, x] == TileType.Empty)
            {
                map[y, x] = TileType.Breakable;
                tileVariant[y, x] = t.breakable[rand.Next(t.breakable.Length)];
            }
        }
    }

    private void AddRockCluster(int cy, int cx, int radius, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dy * dy + dx * dx > radius * radius) continue;
                int y = cy + dy;
                int x = cx + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Empty && rand.Next(100) < 60)
                {
                    map[y, x] = TileType.Breakable;
                    tileVariant[y, x] = t.breakable[rand.Next(t.breakable.Length)];
                }
            }
        }
    }

    private void CarveOpenArea(int cy, int cx, int radius, int rows, int cols, Random rand)
    {
        var t = ThemeTiles[Theme];
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dy * dy + dx * dx > radius * radius) continue;
                int y = cy + dy;
                int x = cx + dx;
                if (y <= 0 || y >= rows - 1 || x <= 0 || x >= cols - 1) continue;
                if (y % 2 == 0 && x % 2 == 0) continue;
                if (map[y, x] == TileType.Breakable && rand.Next(100) < 70)
                {
                    map[y, x] = TileType.Empty;
                    tileVariant[y, x] = t.empty[rand.Next(t.empty.Length)];
                }
            }
        }
    }

    private void AddSandNearLiquid(int rows, int cols, Random rand)
    {
        for (int y = 1; y < rows - 1; y++)
            for (int x = 1; x < cols - 1; x++)
                if (map[y, x] == TileType.Empty && HasAdjacentLiquid(y, x, rows, cols))
                    if (rand.Next(2) == 0)
                        tileVariant[y, x] = "sand0";
    }

    private bool HasAdjacentLiquid(int y, int x, int rows, int cols)
    {
        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int ny = y + dy[i];
            int nx = x + dx[i];
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
            for (int i = 0; i < 4; i++)
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
    // Restituisce un tile Empty casuale lontano dagli spawn.
    // -------------------------------------------------------------------------
    public Point? GetRandomWalkableTile(Random rand, int minDistFromSpawn = 4)
    {
        var candidates = new List<Point>();
        for (int y = 1; y < map.GetLength(0) - 1; y++)
        {
            for (int x = 1; x < map.GetLength(1) - 1; x++)
            {
                if (map[y, x] != TileType.Empty) continue;
                var p = new Point(x, y);
                bool tooClose = SpawnCorners.Any(c =>
                    Math.Abs(c.X - x) + Math.Abs(c.Y - y) < minDistFromSpawn);
                if (!tooClose) candidates.Add(p);
            }
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
        for (int y = 0; y < map.GetLength(0); y++)
            for (int x = 0; x < map.GetLength(1); x++)
            {
                string variant = AnimatedVariant(tileVariant[y, x]);
                Rectangle sourceRect = atlas.GetTile(variant);
                Rectangle destRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
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
        tileVariant[tile.Y, tile.X] = HasAdjacentLiquid(tile.Y, tile.X, map.GetLength(0), map.GetLength(1))
            ? t.empty[0]
            : t.empty[new Random().Next(t.empty.Length)];

        TileBroken?.Invoke(tile);
    }
}
