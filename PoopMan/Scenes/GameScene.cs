using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.GameObjects;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.Input;
using PoopManLibrary.Scenes;
using PoopManLibrary.World;

namespace PoopMan.Scenes;

public class GameScene : Scene
{
    private const float ExtraLifeFlashDuration = 1.5f;

    // ── Costanti angoli spawn ─────────────────────────────────────────────
    private static readonly Point[] Corners =
    {
        new(1, 1),
        new(37, 1),
        new(1, 21),
        new(37, 21)
    };

    private readonly List<BatExplosionParticle> _batExplosionParticles = new();
    private readonly HashSet<Point> _chestTiles = new();
    private readonly Dictionary<Point, DroppedItem> _droppedItems = new();
    private readonly Dictionary<string, List<Rectangle>> _itemAnimations = new();
    private readonly float _itemAnimSpeed = 0.15f;

    // ── Mappa e personaggi ───────────────────────────────────────────────
    private TileAtlas _atlas;
    private List<Bat> _bats;

    // ── Progressione livelli ──────────────────────────────────────────────
    private int _currentLevel;
    private Point _doorPosition;

    // ── Porta ────────────────────────────────────────────────────────────
    private bool _doorSpawned;
    private float _extraLifeFlashTimer;

    // ── Chiave (livello 5+) ───────────────────────────────────────────────
    private bool _hasKey;
    private GameHud _hud;
    private bool _isPaused;
    private int _itemAnimFrame;

    // ── Animazione item ───────────────────────────────────────────────────
    private float _itemAnimTimer;

    // ── Sistema casse e item droppati ────────────────────────────────────
    private Texture2D _itemTexture;
    private int _killStreak;
    private bool _levelComplete;
    private float _levelFlashTimer;
    private TileMap _map;
    private Miner _miner;
    private Texture2D _minerHudTexture;
    private GameOverlay _overlay;

    // ── Menu pausa ────────────────────────────────────────────────────────
    private PauseMenu _pauseMenu;
    private Texture2D _pixel;
    private int _score;
    private SpriteFont _scoreFont;
    private bool _showExtraLifeFlash;

    // ── Stato di gioco ───────────────────────────────────────────────────
    private bool _showGameOver;
    private bool _showLevelFlash;

    // ── Menu Upgrade ──────────────────────────────────────────────────────
    private bool _showUpgradeMenu;

    private Point _spawnPoint;

    // ── Rendering ────────────────────────────────────────────────────────
    private SpriteBatch _spriteBatch;
    private List<UpgradeDef> _upgradeOptions = new();
    private float _upgradePulse;
    private int _upgradeSelected;
    private VisualEffectSystem _vfx;

    // ─────────────────────────────────────────────────────────────────────
    public override void Initialize()
    {
        base.Initialize(); // chiama LoadContent()
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(Core.GraphicsDevice);
        _scoreFont = Content.Load<SpriteFont>("font/Score");

        // ── TileAtlas ─────────────────────────────────────────────────────
        var tilesetTexture = Content.Load<Texture2D>("image/Tile/terrain");
        _atlas = new TileAtlas(tilesetTexture);

        var atlasXml = Path.Combine(Content.RootDirectory, "image", "Tile", "TilesetAtlas.xml");
        var atlasDoc = XDocument.Load(atlasXml);
        foreach (var sprite in atlasDoc.Descendants("Region"))
        {
            var name = sprite.Attribute("Name")!.Value;
            var x = (int)sprite.Attribute("X")!;
            var y = (int)sprite.Attribute("Y")!;
            var w = (int)sprite.Attribute("Width")!;
            var h = (int)sprite.Attribute("Height")!;
            _atlas.AddTile(name, x, y, w, h);
        }

        // ── TileMap ───────────────────────────────────────────────────────
        _spawnPoint = GetRandomCornerSpawn();
        _map = new TileMap(_atlas, 23, 39, _currentLevel, _spawnPoint);
        _map.TileBroken += HandleChestDrop;

        // ── Miner ─────────────────────────────────────────────────────────
        var minerXml = Path.Combine(Content.RootDirectory, "image", "character", "miner_animation.xml");
        _miner = new Miner(_spawnPoint, minerXml, Content);
        _minerHudTexture = Content.Load<Texture2D>("image/character/miner");
        _miner.NeedsRespawn += (s, e) => _miner.Respawn(_miner.TilePosition);
        _miner.DeathAnimationFinished += (s, e) => _showGameOver = true;
        _miner.ExtraLifeEarned += (s, e) =>
        {
            _showExtraLifeFlash = true;
            _extraLifeFlashTimer = 0f;
        };
        _miner.BombPlaced += (s, e) => AudioManager.PlayBombPlaced();
        _miner.BombExploded += (s, isBig) => AudioManager.PlayExplosion(isBig);

        // ── Pixel 1x1 per rettangoli HUD ─────────────────────────────────────
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // ── Item, bat, casse ──────────────────────────────────────────────
        LoadItemAnimations();
        _bats = new List<Bat>();

        // ── UI (dopo LoadItemAnimations che popola _itemTexture) ───────────
        _hud = new GameHud(_scoreFont, _minerHudTexture, _itemTexture, _pixel);
        _overlay = new GameOverlay(_scoreFont, _pixel);
        _pauseMenu = new PauseMenu(_scoreFont, _pixel, Content);

        SpawnBats(_currentLevel);
        InitChests();

        // ── VFX system ────────────────────────────────────────────────────
        _vfx = new VisualEffectSystem(Core.GraphicsDevice);
        _vfx.LoadContent(Content,
            TileMap.Cols * TileMap.TileSize,
            TileMap.Rows * TileMap.TileSize);

        // ── Audio: avvia BGM per il tema corrente ─────────────────────────
        AudioManager.Load(Content); // no-op se già caricato
        AudioManager.StartGameAudio((int)_map.Theme);
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        if (GameController.ToggleFullScreen())
        {
            /* gestito in Game1 */
        }

        var pausePressed = GameController.Pause();

        if (_showGameOver)
        {
            var mouseClick = Core.Input.Mouse.WasButtonJustPressed(MouseButton.Left);
            if (GameController.Restart() || GameController.Action() || mouseClick)
            {
                Core.ChangeScene(new NameEntryScreen(_score, _currentLevel));
            }
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
            var mouse = Core.Input.Mouse;

            if (kb.WasKeyJustPressed(Keys.Left) ||
                kb.WasKeyJustPressed(Keys.A))
                _upgradeSelected = (_upgradeSelected - 1 + _upgradeOptions.Count) % _upgradeOptions.Count;

            if (kb.WasKeyJustPressed(Keys.Right) ||
                kb.WasKeyJustPressed(Keys.D))
                _upgradeSelected = (_upgradeSelected + 1) % _upgradeOptions.Count;

            // Mouse: hover selects card, click confirms
            {
                var vw = Core.GraphicsDevice.Viewport.Width;
                var vh = Core.GraphicsDevice.Viewport.Height;
                var cardW = Math.Min(320, vw / _upgradeOptions.Count - 20);
                var cardH = 220;
                var gap = 18;
                var totalW = _upgradeOptions.Count * cardW + (_upgradeOptions.Count - 1) * gap;
                var startX = vw / 2 - totalW / 2;
                var cardY = vh / 2 - cardH / 2 + 20;
                var mp = mouse.Position;
                for (var i = 0; i < _upgradeOptions.Count; i++)
                {
                    var cardRect = new Rectangle(startX + i * (cardW + gap), cardY, cardW, cardH);
                    if (cardRect.Contains(mp))
                    {
                        _upgradeSelected = i;
                        if (mouse.WasButtonJustPressed(MouseButton.Left))
                        {
                            _miner.ApplyUpgrade(_upgradeOptions[_upgradeSelected].Type);
                            _showUpgradeMenu = false;
                        }
                    }
                }
            }

            if (kb.WasKeyJustPressed(Keys.Enter))
            {
                _miner.ApplyUpgrade(_upgradeOptions[_upgradeSelected].Type);
                _showUpgradeMenu = false;
            }

            return;
        }

        if (pausePressed)
        {
            if (!_isPaused)
            {
                _isPaused = true;
                _pauseMenu.Open();
            }
            else
            {
                // ESC mentre pausa è aperta: PauseMenu gestisce il back interno,
                // qui lo trattiamo come "riprendi" se è già nel menu principale
                _isPaused = false;
            }
        }

        if (_isPaused)
        {
            var action = _pauseMenu.Update(gameTime,
                Core.Input.Keyboard, Core.Input.Mouse,
                Core.GraphicsDevice, pausePressed && _isPaused);

            switch (action)
            {
                case PauseAction.Resume:
                    _isPaused = false;
                    break;
                case PauseAction.GoToTitle:
                    AudioManager.StopGameAudio();
                    Core.ChangeScene(new TitleScene());
                    break;
            }

            return;
        }

        // ── Collisione miner ↔ bat ────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
            foreach (var bat in _bats)
            {
                if (bat.IsDead) continue;
                if (bat.VisualTilePosition == _miner.TilePosition)
                {
                    if (!_miner.TryAbsorbWithShield())
                    {
                        _miner.TriggerDashAfterHit();
                        _miner.Kill();
                    }

                    break;
                }
            }

        // ── Collisione esplosione ↔ miner ─────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible)
            foreach (var tile in _miner.ActiveExplosionTiles)
                if (_miner.TilePosition == tile)
                {
                    if (!_miner.TryAbsorbWithShield())
                    {
                        _miner.TriggerDashAfterHit();
                        _miner.Kill();
                    }

                    break;
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
                    if (it.Type == "chest_tnt")
                    {
                        _miner.AddBigBomb();
                        _droppedItems.Remove(t);
                    }
                    else if (it.Type == "key")
                    {
                        _hasKey = true;
                        _droppedItems.Remove(t);
                    }
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
            var rngExplosion = new Random();

            foreach (var bomb in _miner.FreshExplosions.ToList())
            {
                bomb.DamageApplied = true; // marca subito: il danno viene inflitto una sola volta

                // ── Particelle esplosione bomba (visivamente distinte per big bomb) ───
                SpawnBombParticles(bomb);

                var explosionSet = bomb.ExplosionTiles.ToHashSet();

                // Centro dell'esplosione in pixel (tile centrale della bomba)
                var explosionCenter = new Vector2(
                    bomb.Position.X + TileMap.TileSize * 0.5f,
                    bomb.Position.Y + TileMap.TileSize * 0.5f);

                // Raggio di hitbox in pixel per tipo bomba:
                //   normale → 0.6 tile,  grande → 0.9 tile (cattura i bordi meglio)
                var hitPixelRadius = bomb.BigBomb
                    ? TileMap.TileSize * 0.9f
                    : TileMap.TileSize * 0.6f;

                // Tile adiacenti all'esplosione (per shockwave)
                var adjacentTiles = explosionSet
                    .SelectMany(t => new[]
                    {
                        new Point(t.X + 1, t.Y), new Point(t.X - 1, t.Y),
                        new Point(t.X, t.Y + 1), new Point(t.X, t.Y - 1)
                    })
                    .Where(t => !explosionSet.Contains(t))
                    .ToHashSet();

                // Costruisce l'elenco dei bat colpiti (una sola volta per bat)
                var hitBats = new HashSet<Bat>();
                foreach (var b in _bats)
                {
                    if (b.IsDead || b.IsInvincible) continue;

                    // 1) Controllo tile esatto (principale)
                    if (explosionSet.Contains(b.VisualTilePosition))
                    {
                        hitBats.Add(b);
                        continue;
                    }

                    // 2) Controllo pixel per bat sui bordi dell'esplosione
                    //    (controlla se la posizione pixel è vicina ad almeno una tile dell'esplosione)
                    var batCenter = b.Position + new Vector2(TileMap.TileSize * 0.5f);
                    foreach (var tile in explosionSet)
                    {
                        var tileCenter = new Vector2(
                            tile.X * TileMap.TileSize + TileMap.TileSize * 0.5f,
                            tile.Y * TileMap.TileSize + TileMap.TileSize * 0.5f);
                        if (Vector2.DistanceSquared(batCenter, tileCenter) <= hitPixelRadius * hitPixelRadius)
                        {
                            hitBats.Add(b);
                            break;
                        }
                    }
                }

                foreach (var b in hitBats)
                {
                    // Knockback: spingi il bat lontano dal centro dell'esplosione
                    var batCenter = b.Position + new Vector2(TileMap.TileSize * 0.5f);
                    var knockDir = batCenter - explosionCenter;
                    if (knockDir != Vector2.Zero) knockDir = Vector2.Normalize(knockDir);
                    else knockDir = new Vector2(1, 0);
                    var knockSpeed = bomb.BigBomb ? 260f : 160f;
                    b.ApplyKnockback(knockDir, knockSpeed);

                    bool killed;
                    if (bomb.BigBomb)
                    {
                        killed = true;
                        _score += b.KillPoints;
                        b.Kill();
                    }
                    else
                    {
                        killed = b.TakeDamage();
                        if (killed) _score += b.KillPoints;
                    }

                    if (killed)
                    {
                        _killStreak++;
                        if (_miner.ChainExplosionChance > 0f &&
                            rngExplosion.NextDouble() < _miner.ChainExplosionChance)
                            TriggerChainAt(b.VisualTilePosition);
                    }
                }

                // Shockwave: stordimento/rallentamento sui bat adiacenti che sopravvivono
                if (_miner.UpgradeStunOnHit || _miner.UpgradeSlowOnHit)
                    foreach (var b in _bats)
                    {
                        if (b.IsDead) continue;
                        if (!adjacentTiles.Contains(b.VisualTilePosition)) continue;
                        if (_miner.UpgradeStunOnHit) b.ApplyStun(1.5f);
                        if (_miner.UpgradeSlowOnHit) b.ApplySlow(0.4f, 3.0f);
                    }
            } // fine foreach bomb

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
        var bombTiles = _miner.ActiveBombTiles.ToList();
        var explosionTiles = _miner.ActiveExplosionTiles.ToList();
        var solidBombs = _miner.SolidBombTiles.ToList();

        for (var i = _bats.Count - 1; i >= 0; i--)
        {
            // Grace period: la bomba non blocca il bat già sopra di essa
            var solidForThisBat = solidBombs.Where(t => t != _bats[i].TilePosition);
            _bats[i].SetDangerTiles(bombTiles, explosionTiles);
            _bats[i].SetSolidBombTiles(solidForThisBat);
            _bats[i].Update(_map, gameTime);
            if (!_bats[i].IsDead)
                // Durante la safe zone del miner i bat non aggiornano il target
                // (vagano per la mappa invece di convergere sullo spawn)
                if (!_miner.IsInvincible)
                    _bats[i].SetPlayerTarget(_miner.VisualTilePosition);
            if (_bats[i].IsDeathAnimationFinished)
            {
                // DoubleDrop: probabilità di spawnare un item alla morte del bat
                if (_miner.DoubleDropChance > 0f &&
                    new Random().NextDouble() < _miner.DoubleDropChance)
                    TryDropItem(_bats[i].TilePosition, true);
                _bats.RemoveAt(i);
            }
        }

        // ── Danno continuo esplosione ↔ bat (bat che entrano nel fuoco attivo) ──
        {
            var activeTiles = _miner.ActiveExplosionTiles.ToHashSet();
            if (activeTiles.Count > 0)
                foreach (var bat in _bats.ToList())
                {
                    if (bat.IsDead || bat.IsInvincible) continue;
                    if (!activeTiles.Contains(bat.VisualTilePosition)) continue;
                    var killed = bat.TakeDamage();
                    if (killed) _score += bat.KillPoints;
                }
        }

        // ── Timer animazione item ─────────────────────────────────────────
        _itemAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_itemAnimTimer >= _itemAnimSpeed)
        {
            _itemAnimTimer = 0f;
            _itemAnimFrame++;
        }

        // ── Aggiorna particelle esplosione bat ────────────────────────────
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (var i = _batExplosionParticles.Count - 1; i >= 0; i--)
        {
            var p = _batExplosionParticles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _batExplosionParticles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity *= 0.88f; // attrito
            _batExplosionParticles[i] = p;
        }

        // ── TileMap (animazione liquidi) ──────────────────────────────────
        _map.Update(gameTime);

        // ── VFX update ───────────────────────────────────────────────────
        var mapTransform = Game1.GetMapScaleMatrix(GameHud.ScreenHeight(Core.GraphicsDevice));
        _vfx?.Update(gameTime, _map.Theme,
            TileMap.Cols * TileMap.TileSize,
            TileMap.Rows * TileMap.TileSize,
            () => Enumerable.Empty<(Vector2, Color, float)>());
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        // ── Mappa + entità → cattura nel render target VFX ───────────────
        var hudScreenH = GameHud.ScreenHeight(Core.GraphicsDevice);
        var transform = Game1.GetMapScaleMatrix(hudScreenH);

        if (_vfx != null)
            _vfx.BeginWorldCapture(_map.BackgroundColor);
        else
            Core.GraphicsDevice.Clear(_map.BackgroundColor);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);

        _map.Draw(_spriteBatch);

        // Item droppati
        foreach (var item in _droppedItems)
        {
            string animKey;
            var frame = 0;

            if (item.Value.Type == "door")
            {
                var needsKey = _currentLevel >= 5;
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
            var pos = new Vector2(item.Key.X * TileMap.TileSize,
                item.Key.Y * TileMap.TileSize);
            _spriteBatch.Draw(_itemTexture, pos, frames[frame], Color.White);
        }

        _miner.Draw(_spriteBatch);


        foreach (var b in _bats) b.Draw(_spriteBatch);

        // ── Particelle esplosione bat ─────────────────────────────────────
        foreach (var p in _batExplosionParticles)
        {
            var alpha = p.Life / p.MaxLife; // fade out
            var c = p.Color * alpha;
            var s = Math.Max(1, (int)(p.Size * alpha + 0.5f));
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)(p.Position.X - s / 2f),
                    (int)(p.Position.Y - s / 2f), s, s), c);
        }

        _spriteBatch.End();

        // ── Post-processing (compositing al back buffer con effetti) ─────
        if (_vfx != null)
        {
            _vfx.ApplyPostProcess(transform);
        }
        else
        {
            Core.GraphicsDevice.Clear(_map.BackgroundColor);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
            _spriteBatch.End();
        }

        // ── Overlay (pausa / game over / flash livello / upgrade) ────────
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if (_showGameOver) _overlay.DrawGameOver(_spriteBatch, _score);
        else if (_showUpgradeMenu)
            _overlay.DrawUpgradeMenu(_spriteBatch, _upgradeOptions, _upgradeSelected, (float)Math.Sin(_upgradePulse),
                t => (_miner.GetUpgradeLevel(t), UpgradeRegistry.MaxLevel(t)));
        else if (_isPaused) _pauseMenu.Draw(_spriteBatch);
        else if (_showExtraLifeFlash)
            _overlay.DrawExtraLife(_spriteBatch, _extraLifeFlashTimer, ExtraLifeFlashDuration);
        else if (_showLevelFlash) _overlay.DrawLevelFlash(_spriteBatch, _currentLevel, _levelFlashTimer, _map.Theme);
        _spriteBatch.End();

        // ── HUD ridisegnato sopra gli overlay così è sempre visibile ─────
        var hudMatrix2 = GameHud.GetHudMatrix(Core.GraphicsDevice);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: hudMatrix2);
        _hud.Draw(_spriteBatch, _score, _miner.Lives, _miner.MaxLives, _miner.BigBombCount,
            _currentLevel, _hasKey, _currentLevel >= 5, _map.Theme);
        _spriteBatch.End();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────
    private Point GetRandomCornerSpawn()
    {
        return Corners[new Random().Next(Corners.Length)];
    }

    /// <summary>
    ///     Spawna particelle colorate per l'esplosione di una bomba del Miner.
    ///     Big bomb: molte particelle bianche/arancioni, più grandi e veloci.
    ///     Bomba normale: poche particelle gialle/arancioni.
    /// </summary>
    private void SpawnBombParticles(Bomb bomb)
    {
        var big = bomb.BigBomb;
        var particleCount = big ? 30 : 10;
        var maxSpeed = big ? 160f : 100f;
        var lifetime = big ? 0.65f : 0.45f;
        var size = big ? 6f : 3f;
        var palette = big
            ? new[] { Color.White, Color.Yellow, Color.OrangeRed, new Color(255, 140, 0) }
            : new[] { Color.Yellow, Color.Orange, Color.Gold };

        var rng = new Random();
        foreach (var tile in bomb.ExplosionTiles)
        {
            // Spawn a few particles per tile for big bombs, 1 per tile for small
            var perTile = big ? 2 : 1;
            var tileCenter = new Vector2(
                tile.X * TileMap.TileSize + TileMap.TileSize / 2f,
                tile.Y * TileMap.TileSize + TileMap.TileSize / 2f);
            for (var i = 0;
                 i < perTile && _batExplosionParticles.Count < particleCount + _batExplosionParticles.Count;
                 i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var speed = (float)(rng.NextDouble() * 0.5 + 0.5) * maxSpeed;
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = tileCenter,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed,
                        (float)Math.Sin(angle) * speed),
                    Life = lifetime * (float)(rng.NextDouble() * 0.4 + 0.6),
                    MaxLife = lifetime,
                    Color = palette[rng.Next(palette.Length)],
                    Size = size * (float)(rng.NextDouble() * 0.5 + 0.75)
                });
            }
        }
    }

    /// <summary>
    ///     Gestisce l'esplosione di un bat esplosivo alla sua morte.
    ///     Walid: area 6×6 tile (raggio 3).
    ///     Nuke:  area 12×12 tile (raggio 6), distrugge tutto tranne i muri indistruttibili.
    /// </summary>
    private void TriggerBatExplosion(Point origin, bool big)
    {
        // ── Area quadrata ─────────────────────────────────────────────────
        // Walid: raggio 3 → 7×7 ≈ 6 tile di diametro
        // Nuke:  raggio 6 → 13×13 ≈ 12 tile di diametro
        var radius = big ? 6 : 3;
        var hitTiles = new HashSet<Point>();

        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            Point t = new(origin.X + dx, origin.Y + dy);
            if (!_map.IsInside(t)) continue;
            var tt = _map.GetTile(t);
            if (tt == TileType.Wall) continue; // muri indistruttibili: immuni

            if (tt == TileType.Breakable)
                _map.BreakTile(t); // rompe il breakable

            hitTiles.Add(t);
        }

        // ── Nuke: rimuove item droppati nell'area ─────────────────────────
        if (big)
            foreach (var t in hitTiles)
                _droppedItems.Remove(t);

        // ── Danno ai bat (Nuke: instant kill; Walid: TakeDamage normale) ──
        var explosionPx = new Vector2(
            origin.X * TileMap.TileSize + TileMap.TileSize * 0.5f,
            origin.Y * TileMap.TileSize + TileMap.TileSize * 0.5f);
        var batKnockSpeed = big ? 300f : 180f;

        foreach (var b in _bats.ToList())
        {
            if (b.IsDead || b.IsInvincible) continue;
            if (!hitTiles.Contains(b.VisualTilePosition)) continue;

            // Knockback dalla posizione di esplosione
            var batPx = b.Position + new Vector2(TileMap.TileSize * 0.5f);
            var kDir = batPx - explosionPx;
            if (kDir != Vector2.Zero) kDir = Vector2.Normalize(kDir);
            else kDir = new Vector2(1, 0);
            b.ApplyKnockback(kDir, batKnockSpeed);

            if (big)
            {
                _score += b.KillPoints;
                b.Kill(); // Nuke: kill istantaneo
            }
            else
            {
                if (b.TakeDamage()) _score += b.KillPoints;
            }
        }

        // ── Danno al miner ────────────────────────────────────────────────
        if (!_miner.IsDead && !_miner.IsInvincible && hitTiles.Contains(_miner.VisualTilePosition)) _miner.Kill();

        // ── Audio ─────────────────────────────────────────────────────────
        AudioManager.PlayExplosion(big);

        // ── Effetto visivo particelle ─────────────────────────────────────
        var rng = new Random();
        var worldCenter = new Vector2(
            origin.X * TileMap.TileSize + TileMap.TileSize / 2f,
            origin.Y * TileMap.TileSize + TileMap.TileSize / 2f);

        if (big)
        {
            // ── NUKE: fungo atomico ───────────────────────────────────────
            float nukeR = radius * TileMap.TileSize;

            // Layer 1: flash bianco iniziale (burst radiale densissimo)
            var flashCount = 80;
            for (var i = 0; i < flashCount; i++)
            {
                var angle = Math.PI * 2 / flashCount * i;
                var speed = (float)(rng.NextDouble() * 0.5 + 0.8) * 320f;
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.15 + 0.15),
                    MaxLife = 0.3f,
                    Color = Color.White,
                    Size = (float)(rng.NextDouble() * 10 + 8)
                });
            }

            // Layer 2: shockwave ring (anello espanso a media altezza)
            var ringCount = 64;
            for (var i = 0; i < ringCount; i++)
            {
                var angle = Math.PI * 2 / ringCount * i + rng.NextDouble() * 0.05;
                var speed = (float)(rng.NextDouble() * 0.2 + 0.9) * 300f;
                var ringCol = i % 4 == 0 ? Color.White
                    : i % 4 == 1 ? new Color(255, 220, 80)
                    : i % 4 == 2 ? new Color(255, 100, 20)
                    : new Color(255, 60, 0);
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle))
                        * (float)(rng.NextDouble() * 0.3) * nukeR,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.3 + 0.5),
                    MaxLife = 0.8f,
                    Color = ringCol,
                    Size = (float)(rng.NextDouble() * 7 + 5)
                });
            }

            // Layer 3: colonna di fuoco verso l'alto (fungo vero)
            for (var i = 0; i < 60; i++)
            {
                var xOff = (float)(rng.NextDouble() * 2 - 1) * nukeR * 0.35f;
                var yOff = (float)(rng.NextDouble() * 2 - 1) * nukeR * 0.2f;
                var riseSpeed = (float)(rng.NextDouble() * 0.6 + 0.5) * 260f;
                var lateralSpeed = (float)(rng.NextDouble() * 2 - 1) * 60f;
                Color[] stemPalette =
                {
                    Color.White, new(255, 220, 60),
                    new(255, 130, 0), new(255, 60, 0)
                };
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter + new Vector2(xOff, yOff),
                    Velocity = new Vector2(lateralSpeed, -riseSpeed),
                    Life = (float)(rng.NextDouble() * 0.5 + 0.7),
                    MaxLife = 1.2f,
                    Color = stemPalette[rng.Next(stemPalette.Length)],
                    Size = (float)(rng.NextDouble() * 9 + 5)
                });
            }

            // Layer 4: cappello del fungo (particelle che si espandono in arco verso l'alto)
            for (var i = 0; i < 50; i++)
            {
                var angle = rng.NextDouble() * Math.PI - Math.PI; // -PI..0 (arco superiore)
                var speed = (float)(rng.NextDouble() * 0.5 + 0.6) * 200f;
                var startR = (float)(rng.NextDouble() * 0.5 + 0.3) * nukeR;
                Color[] capPalette =
                {
                    new(255, 80, 0), new(200, 0, 0),
                    new(255, 160, 0), new(120, 0, 0)
                };
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter + new Vector2((float)Math.Cos(angle) * startR,
                        (float)Math.Sin(angle) * startR * 0.4f - nukeR * 0.3f),
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed * 0.3f - 40f),
                    Life = (float)(rng.NextDouble() * 0.4 + 0.6),
                    MaxLife = 1.0f,
                    Color = capPalette[rng.Next(capPalette.Length)],
                    Size = (float)(rng.NextDouble() * 10 + 6)
                });
            }

            // Layer 5: detriti radioattivi (lenti, verdi, lunga durata)
            for (var i = 0; i < 35; i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var speed = (float)(rng.NextDouble() * 0.3 + 0.05) * 90f;
                var spawnOff = new Vector2(
                    (float)(rng.NextDouble() * 2 - 1) * nukeR,
                    (float)(rng.NextDouble() * 2 - 1) * nukeR);
                var radColor = rng.Next(3) == 0
                    ? new Color(100, 255, 60)
                    : rng.Next(2) == 0
                        ? new Color(180, 255, 80)
                        : new Color(60, 200, 30);
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter + spawnOff,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.8 + 1.0),
                    MaxLife = 1.8f,
                    Color = radColor,
                    Size = (float)(rng.NextDouble() * 5 + 2)
                });
            }
        }
        else
        {
            // ── WALID: fire burst caldo e compatto ────────────────────────
            // Layer 1: fiammate principali (esplosione rapida)
            Color[] walidCore =
            {
                new(255, 220, 60), new(255, 140, 0),
                new(255, 60, 0), Color.White
            };
            for (var i = 0; i < 36; i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var speed = (float)(rng.NextDouble() * 0.6 + 0.4) * 160f;
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.25 + 0.35),
                    MaxLife = 0.6f,
                    Color = walidCore[rng.Next(walidCore.Length)],
                    Size = (float)(rng.NextDouble() * 7 + 4)
                });
            }

            // Layer 2: scintille veloci che si allontanano
            for (var i = 0; i < 20; i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var speed = (float)(rng.NextDouble() * 0.5 + 0.5) * 220f;
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.2 + 0.2),
                    MaxLife = 0.4f,
                    Color = rng.Next(2) == 0 ? Color.Yellow : Color.White,
                    Size = (float)(rng.NextDouble() * 3 + 1)
                });
            }

            // Layer 3: brace lenta ad alta durata
            for (var i = 0; i < 10; i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var speed = (float)(rng.NextDouble() * 0.3 + 0.05) * 60f;
                var spawnOff = new Vector2(
                    (float)(rng.NextDouble() * 2 - 1) * TileMap.TileSize * 0.5f,
                    (float)(rng.NextDouble() * 2 - 1) * TileMap.TileSize * 0.5f);
                _batExplosionParticles.Add(new BatExplosionParticle
                {
                    Position = worldCenter + spawnOff,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Life = (float)(rng.NextDouble() * 0.4 + 0.5),
                    MaxLife = 0.9f,
                    Color = new Color(255, (int)(rng.NextDouble() * 60 + 40), 0),
                    Size = (float)(rng.NextDouble() * 3 + 2)
                });
            }
        }
    }

    /// <summary>
    ///     Prova a spawnare un item sul tile specificato se è libero.
    ///     Se <paramref name="forceChest" /> è true, spawna sempre una cassa TNT.
    ///     Altrimenti usa la probabilità base + BonusLootChance del miner.
    /// </summary>
    private void TryDropItem(Point tile, bool forceChest = false)
    {
        if (_droppedItems.ContainsKey(tile)) return;
        if (!_map.IsWalkable(tile)) return;

        var rng = new Random();
        var roll = (float)rng.NextDouble();
        var threshold = forceChest ? 0f : 0.20f + _miner.BonusLootChance; // 20% base + bonus

        if (forceChest || roll < threshold)
            _droppedItems[tile] = new DroppedItem { Type = "chest_tnt", JustSpawned = true };
    }

    /// <summary>
    ///     Mini-esplosione a catena (raggio 1) sul tile dove un bat è stato ucciso.
    ///     Non può scatenare ulteriori catene (no ricorsione).
    /// </summary>
    private void TriggerChainAt(Point origin)
    {
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        var hitTiles = new HashSet<Point> { origin };
        for (var dir = 0; dir < 4; dir++)
        {
            Point t = new(origin.X + dx[dir], origin.Y + dy[dir]);
            if (!_map.IsInside(t)) continue;
            var tt = _map.GetTile(t);
            if (tt == TileType.Wall) continue;
            if (tt == TileType.Breakable)
            {
                _map.BreakTile(t);
                continue;
            }

            hitTiles.Add(t);
        }

        foreach (var b in _bats)
            if (!b.IsDead && !b.IsInvincible && hitTiles.Contains(b.VisualTilePosition))
                if (b.TakeDamage())
                    _score += b.KillPoints;

        if (!_miner.IsDead && !_miner.IsInvincible && hitTiles.Contains(_miner.VisualTilePosition))
            if (!_miner.TryAbsorbWithShield())
                _miner.Kill();
    }

    private void SpawnMiniBats(Point origin)
    {
        var batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
        Point[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        var spawned = 0;

        foreach (var d in dirs.OrderBy(_ => new Random().Next()))
        {
            if (spawned >= 2) break;
            Point tile = new(origin.X + d.X, origin.Y + d.Y);
            if (!_map.IsWalkable(tile)) continue;
            try
            {
                var mini = new Bat(tile, batXml, Content, _map);
                mini.SetAggressionLevel(_currentLevel);
                mini.SetMini(); // mini-bat non può splittarsi ulteriormente
                mini.SetInvincible(1f);
                mini.OnSplit += SpawnMiniBats; // non farà nulla (SetMini blocca CanSplit)
                mini.OnDeathExplosion += TriggerBatExplosion;
                _bats.Add(mini);
                spawned++;
            }
            catch
            {
            }
        }
    }

    private void SpawnBats(int level)
    {
        _bats = new List<Bat>();
        var count = 1 + level;
        var batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
        var rand = new Random();
        var attempts = 0;

        // Probabilità che un bat sia speciale (non Normal): cresce gradualmente, cap 40%
        var specialChance = Math.Clamp(0.02f + level * 0.03f, 0f, 0.40f);

        // Tutte le varianti sbloccate fino al livello corrente sono disponibili.
        // I pipistrelli normali restano la maggioranza: solo specialChance% sarà speciale.
        var unlocked = Bat.UnlockedVariants(level);
        var specialVariants = unlocked.Where(v => v != Bat.BatVariant.Normal).ToList();

        // Garantisce almeno 1 bat speciale per ondata se ci sono 2+ varianti sbloccate
        var guaranteedSpecialPlaced = specialVariants.Count < 2;

        while (_bats.Count < count && attempts < 1000)
        {
            attempts++;
            var tx = rand.Next(1, 38);
            var ty = rand.Next(1, 22);
            var tile = new Point(tx, ty);

            if (Math.Abs(tx - _miner.TilePosition.X) < 6 &&
                Math.Abs(ty - _miner.TilePosition.Y) < 6) continue;

            if (!_map.IsWalkable(tile)) continue;

            var bat = new Bat(tile, batXml, Content, _map);
            bat.SetAggressionLevel(level);

            // Sceglie la variante: il primo bat garantisce almeno un tipo speciale,
            // poi la probabilità specialChance decide i successivi.
            var isSpecial = !guaranteedSpecialPlaced || (float)rand.NextDouble() < specialChance;
            if (isSpecial && specialVariants.Count > 0)
            {
                // Tutte le varianti sbloccate hanno pari probabilità
                bat.ApplyVariant(specialVariants[rand.Next(specialVariants.Count)]);
                guaranteedSpecialPlaced = true;
            }
            else
            {
                bat.ApplyVariant(Bat.BatVariant.Normal);
            }

            bat.OnSplit += SpawnMiniBats;
            bat.OnDeathExplosion += TriggerBatExplosion;
            _bats.Add(bat);
        }
    }

    private void HandleChestDrop(Point tile)
    {
        if (!_chestTiles.Contains(tile)) return;
        _chestTiles.Remove(tile);

        var rand = new Random();
        var roll = rand.Next(100);

        if (roll < 5)
        {
            _droppedItems[tile] = new DroppedItem { Type = "chest_tnt", IsOpen = false };
        }
        else if (roll < 35)
        {
            var batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
            Point[] neighbors =
            {
                new(tile.X + 1, tile.Y), new(tile.X - 1, tile.Y),
                new(tile.X, tile.Y + 1), new(tile.X, tile.Y - 1)
            };
            foreach (var n in neighbors)
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
                    catch
                    {
                    }

                    break;
                }
        }
    }

    private void InitChests()
    {
        var rand = new Random();
        var breakableTiles = new List<Point>();

        for (var y = 0; y < 23; y++)
        for (var x = 0; x < 39; x++)
        {
            var t = new Point(x, y);
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
            // Spawn chiave direttamente visibile su un tile calpestabile (lontano dal miner)
            var candidates = new List<Point>();
            for (var y = 1; y < 22; y++)
            for (var x = 1; x < 38; x++)
            {
                var t = new Point(x, y);
                if (!_map.IsWalkable(t)) continue;
                if (_droppedItems.ContainsKey(t)) continue;
                if (Vector2.Distance(new Vector2(x, y),
                        new Vector2(_miner.TilePosition.X, _miner.TilePosition.Y)) < 12f) continue;
                candidates.Add(t);
            }

            if (candidates.Count > 0)
            {
                var keyTile = candidates[rand.Next(candidates.Count)];
                _droppedItems[keyTile] = new DroppedItem { Type = "key", IsOpen = false, JustSpawned = false };
            }
        }

        SpawnDoor();
    }

    private void SpawnDoor()
    {
        var candidates = new List<Point>();
        for (var y = 1; y < 22; y++)
        for (var x = 1; x < 38; x++)
        {
            var t = new Point(x, y);
            if (!_map.IsWalkable(t)) continue;
            if (Vector2.Distance(new Vector2(t.X, t.Y),
                    new Vector2(_miner.TilePosition.X, _miner.TilePosition.Y)) >= 12f)
                candidates.Add(t);
        }

        if (candidates.Count == 0) return;

        var doorTile = candidates[new Random().Next(candidates.Count)];
        _droppedItems[doorTile] = new DroppedItem { Type = "door", IsOpen = false, JustSpawned = false };
        _doorSpawned = true;
        _doorPosition = doorTile;
    }

    private void LoadItemAnimations()
    {
        var xmlPath = Path.Combine(Content.RootDirectory, "image", "items", "items.xml");
        var doc = XDocument.Load(xmlPath);

        var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/items/items";
        _itemTexture = Content.Load<Texture2D>(texturePath);

        foreach (var region in doc.Descendants("Region"))
        {
            var fullName = region.Attribute("Name")?.Value ?? "";
            var x = int.Parse(region.Attribute("X")?.Value ?? "0");
            var y = int.Parse(region.Attribute("Y")?.Value ?? "0");
            var width = int.Parse(region.Attribute("Width")?.Value ?? "32");
            var height = int.Parse(region.Attribute("Height")?.Value ?? "32");

            var i = fullName.Length;
            while (i > 0 && char.IsDigit(fullName[i - 1])) i--;
            var animName = fullName[..i].TrimEnd('_', '-', ' ');
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
        _hasKey = false;
        _score += 500;

        _map = new TileMap(_atlas, 23, 39, _currentLevel, _spawnPoint);
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
            // Costruisce il dizionario livelli correnti interrogando il miner
            var currentLevels = Enum.GetValues<UpgradeType>()
                .ToDictionary(t => t, t => _miner.GetUpgradeLevel(t));
            _upgradeOptions = UpgradeRegistry.PickRandom(3, currentLevels);
            _upgradeSelected = 0;
            _upgradePulse = 0f;
            _showUpgradeMenu = true;
        }
    }

    private class DroppedItem
    {
        public bool IsOpen;
        public bool IsOpening;
        public bool JustSpawned = true;
        public int OpeningFrame;
        public float OpeningTimer;
        public string Type;
    }

    // ── Effetti esplosione bat ────────────────────────────────────────────
    private struct BatExplosionParticle
    {
        public Vector2 Position; // pixel nel world-space
        public Vector2 Velocity; // pixel/s
        public float Life; // secondi rimasti
        public float MaxLife; // secondi totali
        public Color Color;
        public float Size; // lato quadrato in pixel world
    }
}