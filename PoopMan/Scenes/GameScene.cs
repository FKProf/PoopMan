    using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopMan;
using PoopMan.GameObjects;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.Scenes;
using PoopManLibrary.World;

namespace PoopMan.Scenes;

public class GameScene : Scene
{
    // ── Rendering ────────────────────────────────────────────────────────
    private SpriteBatch _spriteBatch;
    private SpriteFont  _scoreFont;
    private Texture2D   _minerHudTexture;
    private Texture2D   _pixel;
    private GameHud     _hud;
    private GameOverlay _overlay;

    // ── Mappa e personaggi ───────────────────────────────────────────────
    private TileAtlas _atlas;
    private TileMap _map;
    private Miner _miner;
    private List<Bat> _bats;
    private Point _spawnPoint;

    // ── Stato di gioco ───────────────────────────────────────────────────
    private bool _showGameOver  = false;
    private int  _score         = 0;
    private int  _killStreak    = 0;
    private bool _isPaused      = false;
    private bool _showLevelFlash = false;
    private float _levelFlashTimer = 0f;

    // ── Sistema casse e item droppati ────────────────────────────────────
    private Texture2D _itemTexture;
    private Dictionary<string, List<Rectangle>> _itemAnimations = new();
    private HashSet<Point> _chestTiles = new();
    private Dictionary<Point, DroppedItem> _droppedItems = new();

    private class DroppedItem
    {
        public string Type;
        public bool IsOpen;
        public bool JustSpawned = true;
        public bool IsOpening = false;
        public float OpeningTimer = 0f;
        public int OpeningFrame = 0;
    }

    // ── Porta ────────────────────────────────────────────────────────────
    private bool _doorSpawned = false;
    private Point _doorPosition;

    // ── Animazione item ───────────────────────────────────────────────────
    private float _itemAnimTimer = 0f;
    private float _itemAnimSpeed = 0.15f;
    private int _itemAnimFrame = 0;

    // ── Progressione livelli ──────────────────────────────────────────────
    private int _currentLevel = 0;
    private bool _levelComplete = false;

    // ── Chiave (livello 5+) ───────────────────────────────────────────────
    private HashSet<Point> _keyTiles = new();
    private bool _hasKey = false;

    // ── Costanti angoli spawn ─────────────────────────────────────────────
    private static readonly Point[] Corners =
    {
        new(1,  1),
        new(37, 1),
        new(1,  21),
        new(37, 21)
    };

    // ─────────────────────────────────────────────────────────────────────
    public override void Initialize()
    {
        base.Initialize(); // chiama LoadContent()
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(Core.GraphicsDevice);
        _scoreFont   = Content.Load<SpriteFont>("font/Score");

        // ── TileAtlas ─────────────────────────────────────────────────────
        Texture2D tilesetTexture = Content.Load<Texture2D>("image/Tile/terrain");
        _atlas = new TileAtlas(tilesetTexture);

        string atlasXml = Path.Combine(Content.RootDirectory, "image", "Tile", "TilesetAtlas.xml");
        XDocument atlasDoc = XDocument.Load(atlasXml);
        foreach (var sprite in atlasDoc.Descendants("Region"))
        {
            string name = sprite.Attribute("Name")!.Value;
            int x = (int)sprite.Attribute("X")!;
            int y = (int)sprite.Attribute("Y")!;
            int w = (int)sprite.Attribute("Width")!;
            int h = (int)sprite.Attribute("Height")!;
            _atlas.AddTile(name, x, y, w, h);
        }

        // ── TileMap ───────────────────────────────────────────────────────
        _spawnPoint = GetRandomCornerSpawn();
        _map = new TileMap(_atlas, rows: 23, cols: 39, level: _currentLevel);
        _map.TileBroken += HandleChestDrop;

        // ── Miner ─────────────────────────────────────────────────────────
        string minerXml = Path.Combine(Content.RootDirectory, "image", "character", "miner_animation.xml");
        _miner = new Miner(_spawnPoint, minerXml, Content);
        _minerHudTexture = Content.Load<Texture2D>("image/character/miner");
        _miner.NeedsRespawn += (s, e) => _miner.Respawn(_miner.TilePosition);
        _miner.DeathAnimationFinished += (s, e) => _showGameOver = true;

        // ── Pixel 1x1 per rettangoli HUD ─────────────────────────────────────
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // ── Item, bat, casse ──────────────────────────────────────────────
        LoadItemAnimations();
        _bats = new List<Bat>();

        // ── UI (dopo LoadItemAnimations che popola _itemTexture) ───────────
        _hud     = new GameHud(_scoreFont, _minerHudTexture, _itemTexture, _pixel);
        _overlay = new GameOverlay(_scoreFont, _pixel);

        SpawnBats(_currentLevel);
        InitChests();
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        if (GameController.ToggleFullScreen()) { /* gestito in Game1 */ }

        if (GameController.Pause())
        {
            _isPaused = !_isPaused;
        }

        if (_showGameOver)
        {
            if (GameController.Restart() || GameController.Action())
                Core.ChangeScene(new GameScene());
            else if (GameController.Pause())
                Core.ChangeScene(new TitleScene());
            return;
        }

        if (_isPaused) return;

        // ── Collisione miner ↔ bat ────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            foreach (var b in _bats)
            {
                if (b.IsDead) continue;
                var bc = b.GetBounds();
                var mc = _miner.GetBounds();
                if (bc != Collision.Empty && mc != Collision.Empty && bc.Intersects(mc))
                {
                    _miner.Kill();
                    break;
                }
            }
        }

        // ── Collisione esplosione ↔ miner ─────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            foreach (var tile in _miner.ActiveExplosionTiles)
            {
                if (_miner.TilePosition == tile) { _miner.Kill(); break; }
            }
        }

        // ── Miner su item droppato ────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            foreach (var it in _droppedItems.Values)
                it.JustSpawned = false;

            if (_droppedItems.TryGetValue(_miner.TilePosition, out var item)
                && !item.IsOpen && !item.JustSpawned)
            {
                if (item.Type == "door")
                {
                    if (!(_currentLevel >= 5 && !_hasKey) && !item.IsOpening)
                        item.IsOpening = true;
                }
                else if (item.Type == "chest_tnt")
                {
                    item.IsOpen = true;
                    _miner.AddBigBomb();
                    _droppedItems.Remove(_miner.TilePosition);
                }
                else if (item.Type == "key")
                {
                    item.IsOpen = true;
                    _hasKey = true;
                    _droppedItems.Remove(_miner.TilePosition);
                }
            }
        }

        // ── Animazione apertura porta ─────────────────────────────────────
        foreach (var item in _droppedItems.Values.Where(i => i.IsOpening))
        {
            item.OpeningTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (item.OpeningTimer >= _itemAnimSpeed)
            {
                item.OpeningTimer = 0f;
                item.OpeningFrame++;
                if (item.OpeningFrame >= 3)
                {
                    item.IsOpen = true;
                    item.IsOpening = false;
                    _levelComplete = true;
                }
            }
        }

        if (_levelComplete)
        {
            _levelComplete = false;
            _showLevelFlash = true;
            _levelFlashTimer = 0f;
            GoToNextLevel();
        }

        if (_showLevelFlash)
        {
            _levelFlashTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_levelFlashTimer >= GameOverlay.LevelFlashDuration) _showLevelFlash = false;
        }

        // ── Collisione esplosione ↔ bat ───────────────────────────────────
        if (!_miner.IsDead)
        {
            _killStreak = 0;
            foreach (var tile in _miner.ActiveExplosionTiles)
            {
                foreach (var b in _bats)
                {
                    if (!b.IsDead && !b.IsInvincible && b.VisualTilePosition == tile)
                    {
                        b.Kill();
                        _killStreak++;
                    }
                }
            }
            if (_killStreak == 1) _score += 100;
            else if (_killStreak >= 2) _score += _killStreak * 25 + (_killStreak - 1) * 50;
        }

        // ── Aggiorna entità ───────────────────────────────────────────────
        _miner.Update(_map, gameTime);

        if (_miner.IsDead) return;

        // Raccoglie tile pericolose e bombe solide per i bat
        var bombTiles      = _miner.ActiveBombTiles.ToList();
        var explosionTiles = _miner.ActiveExplosionTiles.ToList();
        var solidBombs     = _miner.SolidBombTiles.ToList();

        for (int i = _bats.Count - 1; i >= 0; i--)
        {
            // Grace period: la bomba non blocca il bat già sopra di essa
            var solidForThisBat = solidBombs.Where(t => t != _bats[i].TilePosition);
            _bats[i].SetDangerTiles(bombTiles, explosionTiles);
            _bats[i].SetSolidBombTiles(solidForThisBat);
            _bats[i].Update(_map, gameTime);
            if (!_bats[i].IsDead)
                _bats[i].SetPlayerTarget(_miner.VisualTilePosition);
            if (_bats[i].IsDeathAnimationFinished)
                _bats.RemoveAt(i);
        }

        // ── Timer animazione item ─────────────────────────────────────────
        _itemAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_itemAnimTimer >= _itemAnimSpeed) { _itemAnimTimer = 0f; _itemAnimFrame++; }
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        // ── Sfondo schermo (colore dipende dal tema) ─────────────────────
        Core.GraphicsDevice.Clear(_map.BackgroundColor);

        // ── HUD (sopra la mappa, scalato alla larghezza viewport) ───────────
        var hudMatrix = GameHud.GetHudMatrix(Core.GraphicsDevice);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: hudMatrix);
        _hud.Draw(_spriteBatch, _score, _miner.Lives, _miner.BigBombCount,
                  _currentLevel, _hasKey, _currentLevel >= 5, _map.Theme);
        _spriteBatch.End();

        // ── Mappa + entità (scalata per adattarsi allo schermo) ──────────
        int hudScreenH = GameHud.ScreenHeight(Core.GraphicsDevice);
        var transform  = Game1.GetMapScaleMatrix(hudScreenH);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);

        _map.Draw(_spriteBatch);

        // Item droppati
        foreach (var item in _droppedItems)
        {
            string animKey;
            int frame = 0;

            if (item.Value.Type == "door")
            {
                bool needsKey = _currentLevel >= 5;
                if (item.Value.IsOpening)
                    animKey = needsKey ? "door_key" : "door_opening";
                else
                    animKey = needsKey ? "door_key_closed" : "door_closed";
                frame = item.Value.IsOpening ? item.Value.OpeningFrame : 0;
            }
            else if (item.Value.Type == "key")
            {
                animKey = "key";
                frame = _itemAnimFrame % (_itemAnimations.ContainsKey("key") ? _itemAnimations["key"].Count : 1);
            }
            else
            {
                animKey = "chest";
                frame = _itemAnimFrame % (_itemAnimations.ContainsKey("chest") ? _itemAnimations["chest"].Count : 1);
            }

            if (!_itemAnimations.TryGetValue(animKey, out var frames) || frames.Count == 0) continue;
            frame = Math.Min(frame, frames.Count - 1);
            Vector2 pos = new Vector2(item.Key.X * PoopManLibrary.World.TileMap.TileSize,
                                      item.Key.Y * PoopManLibrary.World.TileMap.TileSize);
            _spriteBatch.Draw(_itemTexture, pos, frames[frame], Color.White);
        }

        _miner.Draw(_spriteBatch);
        foreach (var b in _bats) b.Draw(_spriteBatch);

        _spriteBatch.End();

        // ── Overlay (pausa / game over / flash livello) ───────────────────
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if      (_showGameOver)   _overlay.DrawGameOver(_spriteBatch, _score);
        else if (_isPaused)       _overlay.DrawPause(_spriteBatch);
        else if (_showLevelFlash) _overlay.DrawLevelFlash(_spriteBatch, _currentLevel, _levelFlashTimer, _map.Theme);
        _spriteBatch.End();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Draw helpers (mantenuti per compatibilità interna)
    // ─────────────────────────────────────────────────────────────────────
    private void DrawRect(Rectangle r, Color c)
        => _spriteBatch.Draw(_pixel, r, c);

    private void DrawTextCentered(string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _scoreFont.MeasureString(text) * 0.5f;
        Vector2 pos    = new Vector2(cx, cy);
        _spriteBatch.DrawString(_scoreFont, text, pos + new Vector2(2, 2) * scale, Color.Black * 0.6f, 0f, origin, scale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_scoreFont, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────
    private Point GetRandomCornerSpawn()
        => Corners[new Random().Next(Corners.Length)];

    private void SpawnBats(int level)
    {
        _bats = new List<Bat>();
        int count = 1 + level;
        string batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
        var rand = new Random();
        int attempts = 0;

        while (_bats.Count < count && attempts < 1000)
        {
            attempts++;
            int tx = rand.Next(1, 38);
            int ty = rand.Next(1, 22);
            Point tile = new Point(tx, ty);

            if (Math.Abs(tx - _miner.TilePosition.X) < 4 &&
                Math.Abs(ty - _miner.TilePosition.Y) < 4) continue;

            if (!_map.IsWalkable(tile)) continue;

            var bat = new Bat(tile, batXml, Content, _map);
            bat.SetAggressionLevel(level);
            _bats.Add(bat);
        }
    }

    private void HandleChestDrop(Point tile)
    {
        if (_currentLevel >= 5 && _keyTiles.Contains(tile))
        {
            _keyTiles.Remove(tile);
            _droppedItems[tile] = new DroppedItem { Type = "key", IsOpen = false, JustSpawned = true };
            return;
        }

        if (!_chestTiles.Contains(tile)) return;
        _chestTiles.Remove(tile);

        var rand = new Random();
        int roll = rand.Next(100);

        if (roll < 5)
        {
            _droppedItems[tile] = new DroppedItem { Type = "chest_tnt", IsOpen = false };
        }
        else if (roll < 35)
        {
            string batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
            Point[] neighbors = {
                new(tile.X + 1, tile.Y), new(tile.X - 1, tile.Y),
                new(tile.X, tile.Y + 1), new(tile.X, tile.Y - 1)
            };
            foreach (var n in neighbors)
            {
                if (_map.IsWalkable(n))
                {
                    try
                    {
                        var nb = new Bat(n, batXml, Content, _map);
                        nb.SetInvincible(1.6f);
                        nb.SetAggressionLevel(_currentLevel);
                        _bats.Add(nb);
                    }
                    catch { }
                    break;
                }
            }
        }
    }

    private void InitChests()
    {
        var rand = new Random();
        var breakableTiles = new List<Point>();

        for (int y = 0; y < 23; y++)
            for (int x = 0; x < 39; x++)
            {
                Point t = new Point(x, y);
                if (_map.GetTile(t) == TileType.Breakable)
                    breakableTiles.Add(t);
            }

        breakableTiles = breakableTiles.OrderBy(_ => rand.Next()).ToList();

        foreach (var tile in breakableTiles)
            if (rand.Next(100) < 40)
                _chestTiles.Add(tile);

        if (_currentLevel >= 5)
        {
            _hasKey = false;
            var nonChest = breakableTiles.Where(t => !_chestTiles.Contains(t)).ToList();
            if (nonChest.Count > 0)
                _keyTiles.Add(nonChest[rand.Next(nonChest.Count)]);
        }

        SpawnDoor();
    }

    private void SpawnDoor()
    {
        var candidates = new List<Point>();
        for (int y = 1; y < 22; y++)
            for (int x = 1; x < 38; x++)
            {
                Point t = new Point(x, y);
                if (!_map.IsWalkable(t)) continue;
                if (Vector2.Distance(new Vector2(t.X, t.Y),
                    new Vector2(_miner.TilePosition.X, _miner.TilePosition.Y)) >= 10f)
                    candidates.Add(t);
            }

        if (candidates.Count == 0) return;

        Point doorTile = candidates[new Random().Next(candidates.Count)];
        _droppedItems[doorTile] = new DroppedItem { Type = "door", IsOpen = false, JustSpawned = false };
        _doorSpawned = true;
        _doorPosition = doorTile;
    }

    private void LoadItemAnimations()
    {
        string xmlPath = Path.Combine(Content.RootDirectory, "image", "items", "items.xml");
        XDocument doc = XDocument.Load(xmlPath);

        var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/items/items";
        _itemTexture = Content.Load<Texture2D>(texturePath);

        foreach (var region in doc.Descendants("Region"))
        {
            string fullName = region.Attribute("Name")?.Value ?? "";
            int x = int.Parse(region.Attribute("X")?.Value ?? "0");
            int y = int.Parse(region.Attribute("Y")?.Value ?? "0");
            int width  = int.Parse(region.Attribute("Width")?.Value  ?? "32");
            int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

            int i = fullName.Length;
            while (i > 0 && char.IsDigit(fullName[i - 1])) i--;
            string animName = fullName[..i].TrimEnd('_', '-', ' ');
            if (string.IsNullOrEmpty(animName)) animName = fullName;

            if (!_itemAnimations.ContainsKey(animName))
                _itemAnimations[animName] = new List<Rectangle>();

            var rect = new Rectangle(x, y, width, height);
            if (!_itemAnimations[animName].Contains(rect))
                _itemAnimations[animName].Add(rect);
        }
    }

    private void GoToNextLevel()
    {
        _currentLevel++;
        _doorSpawned = false;
        _droppedItems.Clear();
        _chestTiles.Clear();
        _keyTiles.Clear();
        _hasKey = false;
        _score += 500;

        _map = new TileMap(_atlas, 23, 39, level: _currentLevel);
        _map.TileBroken += HandleChestDrop;

        _miner.ResetForNewLevel(_spawnPoint);

        SpawnBats(_currentLevel);
        InitChests();
    }
}
