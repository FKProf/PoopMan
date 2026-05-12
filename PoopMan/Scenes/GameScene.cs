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
    private AudioSettingsPanel _audioPanel;
    private bool _showLevelFlash = false;
    private float _levelFlashTimer = 0f;
    private bool _showExtraLifeFlash = false;
    private float _extraLifeFlashTimer = 0f;
    private const float ExtraLifeFlashDuration = 1.5f;

    // ── Menu pausa ────────────────────────────────────────────────────────
    private enum PauseScreen { Menu, Audio }
    private PauseScreen _pauseScreen    = PauseScreen.Menu;
    private int         _pauseMenuItem  = 0;
    private float       _pausePulse     = 0f;
    private static readonly string[] PauseMenuItems = { "RIPRENDI", "AUDIO", "MENU PRINCIPALE" };
    private const int PauseBtnW = 280;
    private const int PauseBtnH = 34;
    private const int PauseBtnGap = 10;

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

    // ── Menu Upgrade ──────────────────────────────────────────────────────
    private bool _showUpgradeMenu = false;
    private List<UpgradeDef> _upgradeOptions = new();
    private int   _upgradeSelected = 0;
    private float _upgradePulse    = 0f;

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
        _map = new TileMap(_atlas, rows: 23, cols: 39, level: _currentLevel, playerSpawn: _spawnPoint);
        _map.TileBroken += HandleChestDrop;

        // ── Miner ─────────────────────────────────────────────────────────
        string minerXml = Path.Combine(Content.RootDirectory, "image", "character", "miner_animation.xml");
        _miner = new Miner(_spawnPoint, minerXml, Content);
        _minerHudTexture = Content.Load<Texture2D>("image/character/miner");
        _miner.NeedsRespawn += (s, e) => _miner.Respawn(_miner.TilePosition);
        _miner.DeathAnimationFinished += (s, e) => _showGameOver = true;
        _miner.ExtraLifeEarned += (s, e) => { _showExtraLifeFlash = true; _extraLifeFlashTimer = 0f; };
        _miner.BombPlaced   += (s, e)      => AudioManager.PlayBombPlaced();
        _miner.BombExploded += (s, isBig)  => AudioManager.PlayExplosion(isBig);

        // ── Pixel 1x1 per rettangoli HUD ─────────────────────────────────────
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // ── Item, bat, casse ──────────────────────────────────────────────
        LoadItemAnimations();
        _bats = new List<Bat>();

        // ── UI (dopo LoadItemAnimations che popola _itemTexture) ───────────
        _hud     = new GameHud(_scoreFont, _minerHudTexture, _itemTexture, _pixel);
        _overlay = new GameOverlay(_scoreFont, _pixel);
        _audioPanel = new AudioSettingsPanel(_scoreFont, _pixel);

        SpawnBats(_currentLevel);
        InitChests();

        // ── Audio: avvia BGM per il tema corrente ─────────────────────────
        AudioManager.Load(Content);                      // no-op se già caricato
        AudioManager.StartGameAudio((int)_map.Theme);
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        if (GameController.ToggleFullScreen()) { /* gestito in Game1 */ }

        bool pausePressed = GameController.Pause();

        if (_showGameOver)
        {
            if (GameController.Restart() || GameController.Action())
                Core.ChangeScene(new GameScene());
            else if (pausePressed)
            {
                AudioManager.StopGameAudio();
                Core.ChangeScene(new TitleScene());
            }
            return;
        }

        // ── Menu Upgrade ──────────────────────────────────────────────────
        if (_showUpgradeMenu)
        {
            _upgradePulse += (float)gameTime.ElapsedGameTime.TotalSeconds * 4f;
            var kb = Core.Input.Keyboard;

            if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Left) ||
                kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.A))
                _upgradeSelected = (_upgradeSelected - 1 + _upgradeOptions.Count) % _upgradeOptions.Count;

            if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Right) ||
                kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D))
                _upgradeSelected = (_upgradeSelected + 1) % _upgradeOptions.Count;

            if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                _miner.ApplyUpgrade(_upgradeOptions[_upgradeSelected].Type);
                _showUpgradeMenu = false;
            }
            return;
        }

        if (pausePressed)
        {
            if (_isPaused && _pauseScreen == PauseScreen.Audio)
            {
                // ESC dal pannello audio → torna al menu pausa
                _pauseScreen = PauseScreen.Menu;
            }
            else
            {
                _isPaused = !_isPaused;
                _pauseScreen   = PauseScreen.Menu;
                _pauseMenuItem = 0;
            }
        }

        if (_isPaused)
        {
            _pausePulse += (float)gameTime.ElapsedGameTime.TotalSeconds * 3.5f;

            if (_pauseScreen == PauseScreen.Audio)
            {
                _audioPanel.Update(gameTime);
            }
            else
            {
                var kb = Core.Input.Keyboard;
                if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up))
                    _pauseMenuItem = (_pauseMenuItem - 1 + PauseMenuItems.Length) % PauseMenuItems.Length;
                if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down))
                    _pauseMenuItem = (_pauseMenuItem + 1) % PauseMenuItems.Length;
                if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    switch (_pauseMenuItem)
                    {
                        case 0: // RIPRENDI
                            _isPaused = false;
                            break;
                        case 1: // AUDIO
                            _pauseScreen = PauseScreen.Audio;
                            break;
                        case 2: // MENU PRINCIPALE
                            AudioManager.StopGameAudio();
                            Core.ChangeScene(new TitleScene());
                            break;
                    }
                }
            }
            return;
        }

        // ── Collisione miner ↔ bat ────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            var rand = new Random();
            foreach (var b in _bats)
            {
                if (b.IsDead) continue;
                var bc = b.GetBounds();
                var mc = _miner.GetBounds();
                if (bc != Collision.Empty && mc != Collision.Empty && bc.Intersects(mc))
                {
                    // Critico: 20% probabilità di uccidere il bat invece del miner
                    if (_miner.UpgradeCritical && rand.Next(100) < 20)
                    {
                        _score += b.KillPoints * 2; // doppio punteggio sul critico
                        b.Kill();
                    }
                    else if (_miner.TryAbsorbWithShield())
                    {
                        // scudo assorbe il colpo
                    }
                    else
                    {
                        _miner.TriggerDashAfterHit();
                        _miner.Kill();
                    }
                    break;
                }
            }
        }

        // ── Collisione esplosione ↔ miner ─────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            foreach (var tile in _miner.ActiveExplosionTiles)
            {
                if (_miner.TilePosition == tile)
                {
                    if (!_miner.TryAbsorbWithShield())
                    {
                        _miner.TriggerDashAfterHit();
                        _miner.Kill();
                    }
                    break;
                }
            }
        }

        // ── Miner su item droppato ────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
        {
            foreach (var it in _droppedItems.Values)
                it.JustSpawned = false;

            // Calamita: raccoglie automaticamente item entro 3 tile
            if (_miner.UpgradeMagnet)
            {
                var toCollect = _droppedItems
                    .Where(kv => !kv.Value.IsOpen && kv.Value.Type != "door"
                        && Math.Abs(kv.Key.X - _miner.VisualTilePosition.X) <= 3
                        && Math.Abs(kv.Key.Y - _miner.VisualTilePosition.Y) <= 3)
                    .Select(kv => kv.Key).ToList();
                foreach (var t in toCollect)
                {
                    var it = _droppedItems[t];
                    if (it.Type == "chest_tnt") { _miner.AddBigBomb(); _droppedItems.Remove(t); }
                    else if (it.Type == "key")  { _hasKey = true;      _droppedItems.Remove(t); }
                }
            }

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

            // Tile adiacenti alle esplosioni (per shockwave stun/slow)
            var explosionSet = _miner.ActiveExplosionTiles.ToHashSet();
            var adjacentTiles = explosionSet
                .SelectMany(t => new[]
                {
                    new Point(t.X + 1, t.Y), new Point(t.X - 1, t.Y),
                    new Point(t.X, t.Y + 1), new Point(t.X, t.Y - 1)
                })
                .Where(t => !explosionSet.Contains(t))
                .ToHashSet();

            foreach (var tile in explosionSet)
            {
                foreach (var b in _bats)
                {
                    if (!b.IsDead && !b.IsInvincible && b.VisualTilePosition == tile)
                    {
                        bool killed = b.TakeDamage();
                        if (killed)
                        {
                            _score += b.KillPoints;
                            _killStreak++;

                            if (_miner.ChainExplosionChance > 0f &&
                                new Random().NextDouble() < _miner.ChainExplosionChance)
                                TriggerChainAt(tile);
                        }
                    }
                }
            }

            // Shockwave: stordimento/rallentamento sui bat adiacenti che sopravvivono
            if (_miner.UpgradeStunOnHit || _miner.UpgradeSlowOnHit)
            {
                foreach (var b in _bats)
                {
                    if (b.IsDead) continue;
                    if (!adjacentTiles.Contains(b.VisualTilePosition)) continue;
                    if (_miner.UpgradeStunOnHit)  b.ApplyStun(1.5f);
                    if (_miner.UpgradeSlowOnHit)  b.ApplySlow(0.4f, 3.0f);
                }
            }

            // Bonus streak (2+ bat uccisi nella stessa esplosione)
            if (_killStreak >= 2) _score += (_killStreak - 1) * 75;
            _miner.CheckExtraLife(_score);
        }

        if (_showExtraLifeFlash)
        {
            _extraLifeFlashTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_extraLifeFlashTimer >= ExtraLifeFlashDuration) _showExtraLifeFlash = false;
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
            {
                // Durante la safe zone del miner i bat non aggiornano il target
                // (vagano per la mappa invece di convergere sullo spawn)
                if (!_miner.IsInvincible)
                    _bats[i].SetPlayerTarget(_miner.VisualTilePosition);
            }
            if (_bats[i].IsDeathAnimationFinished)
            {
                // DoubleDrop: probabilità di spawnare un item alla morte del bat
                if (_miner.DoubleDropChance > 0f &&
                    new Random().NextDouble() < _miner.DoubleDropChance)
                    TryDropItem(_bats[i].TilePosition, forceChest: true);
                _bats.RemoveAt(i);
            }
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

        // ── Overlay (pausa / game over / flash livello / upgrade) ────────
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if      (_showGameOver)       _overlay.DrawGameOver(_spriteBatch, _score);
        else if (_showUpgradeMenu)    _overlay.DrawUpgradeMenu(_spriteBatch, _upgradeOptions, _upgradeSelected, (float)Math.Sin(_upgradePulse));
        else if (_isPaused)           DrawPauseWithAudio();
        else if (_showExtraLifeFlash) _overlay.DrawExtraLife(_spriteBatch, _extraLifeFlashTimer, ExtraLifeFlashDuration);
        else if (_showLevelFlash)     _overlay.DrawLevelFlash(_spriteBatch, _currentLevel, _levelFlashTimer, _map.Theme);
        _spriteBatch.End();
    }

    private void DrawPauseWithAudio()
    {
        int vw = _spriteBatch.GraphicsDevice.Viewport.Width;
        int vh = _spriteBatch.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;

        // Sfondo scuro
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), Color.Black * 0.60f);

        if (_pauseScreen == PauseScreen.Audio)
        {
            DrawPauseAudioPanel(cx, vh / 2);
            return;
        }

        // ── Menu pausa ────────────────────────────────────────────────────
        int totalMenuH = PauseMenuItems.Length * (PauseBtnH + PauseBtnGap) - PauseBtnGap;
        int boxW  = PauseBtnW + 60;
        int boxH  = 52 + totalMenuH + 28;
        int boxX  = cx - boxW / 2;
        int boxY  = vh / 2 - boxH / 2;

        // Riquadro
        DrawRect(new Rectangle(boxX, boxY, boxW, boxH), new Color(15, 15, 35, 240));
        DrawBorderRect(boxX, boxY, boxW, boxH, Color.Yellow);

        DrawTextCentered("PAUSA", cx, boxY + 22, Color.Yellow, 1.8f);

        // Linea separatrice
        DrawRect(new Rectangle(boxX + 16, boxY + 44, boxW - 32, 2), new Color(80, 60, 160));

        int menuStartY = boxY + 54;
        for (int i = 0; i < PauseMenuItems.Length; i++)
        {
            int btnY = menuStartY + i * (PauseBtnH + PauseBtnGap);
            bool sel = i == _pauseMenuItem;

            Color bg     = sel ? new Color(60, 40, 140, 230) : new Color(25, 25, 55, 180);
            Color border = sel ? Color.Yellow : new Color(70, 70, 110);
            float pulse  = sel ? (0.85f + 0.15f * (float)Math.Sin(_pausePulse)) : 1f;

            DrawRect(new Rectangle(cx - PauseBtnW / 2 - 1, btnY - 1, PauseBtnW + 2, PauseBtnH + 2), border);
            DrawRect(new Rectangle(cx - PauseBtnW / 2,     btnY,     PauseBtnW,     PauseBtnH),     bg);

            // Icona colorata per Home
            Color textColor = i == 2
                ? (sel ? new Color(255, 120, 120) * pulse : new Color(200, 100, 100))
                : (sel ? Color.Yellow * pulse : Color.LightGray);

            DrawTextCentered(PauseMenuItems[i], cx, btnY + PauseBtnH / 2, textColor, sel ? 1.05f : 1.0f);

            if (sel)
            {
                string arrow = ">";
                Vector2 arSz = _scoreFont.MeasureString(arrow);
                float arX = cx - PauseBtnW / 2 - arSz.X - 8;
                _spriteBatch.DrawString(_scoreFont, arrow,
                    new Vector2(arX, btnY + PauseBtnH / 2 - arSz.Y / 2),
                    Color.Yellow * pulse);
            }
        }

        DrawTextCentered("^v: seleziona   ENTER: conferma   ESC: riprendi",
            cx, boxY + boxH - 14, Color.DarkGray, 0.72f);
    }

    private void DrawPauseAudioPanel(int cx, int midY)
    {
        int boxW = 460;
        int boxH = 190;
        int boxX = cx - boxW / 2;
        int boxY = midY - boxH / 2;

        DrawRect(new Rectangle(boxX, boxY, boxW, boxH), new Color(15, 15, 35, 245));
        DrawBorderRect(boxX, boxY, boxW, boxH, Color.CornflowerBlue);

        DrawTextCentered("IMPOSTAZIONI AUDIO", cx, boxY + 22, Color.CornflowerBlue, 1.0f);
        DrawRect(new Rectangle(boxX + 16, boxY + 40, boxW - 32, 2), new Color(40, 80, 160));

        _audioPanel.Draw(_spriteBatch, cx, boxY + 85, showHint: false);

        DrawTextCentered("< > volume    ^ v seleziona", cx, boxY + 138, new Color(100, 100, 130), 0.78f);
        DrawTextCentered("ESC: indietro", cx, boxY + boxH - 16, Color.DarkGray, 0.75f);
    }

    private void DrawBorderRect(int x, int y, int w, int h, Color c)
    {
        DrawRect(new Rectangle(x,         y,         w, 2), c);
        DrawRect(new Rectangle(x,         y + h - 2, w, 2), c);
        DrawRect(new Rectangle(x,         y,         2, h), c);
        DrawRect(new Rectangle(x + w - 2, y,         2, h), c);
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

    /// <summary>
    /// Gestisce l'esplosione di un bat esplosivo alla sua morte.
    /// Danneggia tutti i bat e il miner nei tile circostanti (raggio 1 o 2),
    /// rompe i breakable e riproduce il suono di esplosione.
    /// </summary>
    private void TriggerBatExplosion(Point origin, bool big)
    {
        int range = big ? 2 : 1;

        // Calcola tutti i tile colpiti (identico alla logica di Bomb.Explode)
        var hitTiles = new HashSet<Point>();
        if (_map.GetTile(origin) != PoopManLibrary.World.TileType.Wall)
            hitTiles.Add(origin);

        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        for (int dir = 0; dir < 4; dir++)
        {
            for (int step = 1; step <= range; step++)
            {
                Point t = new(origin.X + dx[dir] * step, origin.Y + dy[dir] * step);
                if (!_map.IsInside(t)) break;
                var tileType = _map.GetTile(t);
                if (tileType == PoopManLibrary.World.TileType.Wall) break;
                if (tileType == PoopManLibrary.World.TileType.Breakable)
                {
                    _map.BreakTile(t);
                    break;
                }
                hitTiles.Add(t);
            }
        }

        // Danno ai bat nei tile colpiti (le esplosioni dei bat ignorano la resistenza)
        foreach (var b in _bats)
        {
            if (!b.IsDead && !b.IsInvincible && hitTiles.Contains(b.VisualTilePosition))
            {
                if (b.TakeDamage()) _score += b.KillPoints;
            }
}

        // Danno al miner
        if (!_miner.IsDead && !_miner.IsInvincible && hitTiles.Contains(_miner.VisualTilePosition))
            _miner.Kill();

        // Audio esplosione
        AudioManager.PlayExplosion(big);
    }

    /// <summary>
    /// Prova a spawnare un item sul tile specificato se è libero.
    /// Se <paramref name="forceChest"/> è true, spawna sempre una cassa TNT.
    /// Altrimenti usa la probabilità base + BonusLootChance del miner.
    /// </summary>
    private void TryDropItem(Point tile, bool forceChest = false)
    {
        if (_droppedItems.ContainsKey(tile)) return;
        if (!_map.IsWalkable(tile)) return;

        var rng = new Random();
        float roll = (float)rng.NextDouble();
        float threshold = forceChest ? 0f : (0.20f + _miner.BonusLootChance);  // 20% base + bonus

        if (forceChest || roll < threshold)
        {
            _droppedItems[tile] = new DroppedItem { Type = "chest_tnt", JustSpawned = true };
        }
    }

    /// <summary>
    /// Mini-esplosione a catena (raggio 1) sul tile dove un bat è stato ucciso.
    /// Non può scatenare ulteriori catene (no ricorsione).
    /// </summary>
    private void TriggerChainAt(Point origin)
    {
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        var hitTiles = new HashSet<Point> { origin };
        for (int dir = 0; dir < 4; dir++)
        {
            Point t = new(origin.X + dx[dir], origin.Y + dy[dir]);
            if (!_map.IsInside(t)) continue;
            var tt = _map.GetTile(t);
            if (tt == PoopManLibrary.World.TileType.Wall) continue;
            if (tt == PoopManLibrary.World.TileType.Breakable) { _map.BreakTile(t); continue; }
            hitTiles.Add(t);
        }
        foreach (var b in _bats)
        {
            if (!b.IsDead && !b.IsInvincible && hitTiles.Contains(b.VisualTilePosition))
            {
                if (b.TakeDamage()) _score += b.KillPoints;
            }
        }
        if (!_miner.IsDead && !_miner.IsInvincible && hitTiles.Contains(_miner.VisualTilePosition))
        {
            if (!_miner.TryAbsorbWithShield()) _miner.Kill();
        }
    }

    private void SpawnMiniBats(Point origin)
    {
        string batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
        Point[] dirs  = { new(1,0), new(-1,0), new(0,1), new(0,-1) };
        int spawned   = 0;

        foreach (var d in dirs.OrderBy(_ => new Random().Next()))
        {
            if (spawned >= 2) break;
            Point tile = new(origin.X + d.X, origin.Y + d.Y);
            if (!_map.IsWalkable(tile)) continue;
            try
            {
                var mini = new Bat(tile, batXml, Content, _map);
                mini.SetAggressionLevel(_currentLevel);
                mini.SetMini();          // mini-bat non può splittarsi ulteriormente
                mini.SetInvincible(1f);
                mini.OnSplit         += SpawnMiniBats; // non farà nulla (SetMini blocca CanSplit)
                mini.OnDeathExplosion += TriggerBatExplosion;
                _bats.Add(mini);
                spawned++;
            }
            catch { }
        }
    }

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

            if (Math.Abs(tx - _miner.TilePosition.X) < 6 &&
                Math.Abs(ty - _miner.TilePosition.Y) < 6) continue;

            if (!_map.IsWalkable(tile)) continue;

            var bat = new Bat(tile, batXml, Content, _map);
            bat.SetAggressionLevel(level);
            bat.OnSplit         += SpawnMiniBats;
            bat.OnDeathExplosion += TriggerBatExplosion;
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
                        nb.OnSplit += SpawnMiniBats;
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

        _map = new TileMap(_atlas, 23, 39, level: _currentLevel, playerSpawn: _spawnPoint);
        _map.TileBroken += HandleChestDrop;

        _miner.ResetForNewLevel(_spawnPoint);
        _miner.NotifyLevelUp(); // SlowRegen

        SpawnBats(_currentLevel);
        InitChests();

        // ── Aggiorna BGM al nuovo tema ────────────────────────────────────
        AudioManager.OnLevelChanged((int)_map.Theme);

        // ── Menu upgrade ogni EveryNLevels livelli (primo al livello 3) ───
        if (_currentLevel >= UpgradeRegistry.EveryNLevels
            && _currentLevel % UpgradeRegistry.EveryNLevels == 0)
        {
            _upgradeOptions  = UpgradeRegistry.PickRandom(3);
            _upgradeSelected = 0;
            _upgradePulse    = 0f;
            _showUpgradeMenu = true;
        }
    }
}
