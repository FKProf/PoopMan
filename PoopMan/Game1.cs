using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.GameObjects;
using PoopManLibrary;
using PoopManLibrary.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PoopMan
{
    public class Game1 : Core
    {
        // ── Rendering ────────────────────────────────────────────────────────
        private SpriteBatch _spriteBatch;
        private SpriteFont scoreFont;           // Font per l'HUD
        private const int HudHeight = 32;       // Altezza in pixel della barra HUD superiore
        private Texture2D minerHudTexture;
        // ── Mappa e personaggi ───────────────────────────────────────────────
        private TileAtlas atlas;
        private TileMap map;
        private Miner miner;
        private List<Bat> bats;
        private Point currentSpawnPoint;//pos spawn
        // ── Stato di gioco ───────────────────────────────────────────────────
        private bool showGameOver = false;      // true quando il miner muore senza vite

        // ── Punteggio ────────────────────────────────────────────────────────
        private int score = 0;
        private int killStreak = 0;             // Pipistrelli uccisi nella stessa esplosione

        // ── Sistema casse e item droppati ────────────────────────────────────
        private Texture2D itemTexture;
        private Dictionary<string, List<Rectangle>> itemAnimations = new();
        private HashSet<Point> chestTiles = new();          // Tile breakable che nascondono una cassa
        private Dictionary<Point, DroppedItem> droppedItems = new(); // Item visibili dopo l'esplosione

        /// <summary>
        /// Rappresenta un item droppato a terra (porta o cassa con TNT).
        /// </summary>
        private class DroppedItem
        {
            public string Type;             // "door" o "chest_tnt"
            public bool IsOpen;             // true quando il miner ci è passato sopra
            public bool JustSpawned = true; // Protegge dal trigger immediato al frame di spawn
            public bool IsOpening = false;  // true mentre l'animazione di apertura è in corso
            public float OpeningTimer = 0f; // Accumulatore tempo per l'animazione
            public int OpeningFrame = 0;    // Frame corrente dell'animazione di apertura
        }

        // ── Porta di uscita livello ──────────────────────────────────────────
        private bool doorSpawned = false;       // true se la porta è già apparsa
        private Point doorPosition;             // Tile dove si trova la porta

        // ── Animazione item a terra ──────────────────────────────────────────
        private float itemAnimTimer = 0f;
        private float itemAnimSpeed = 0.15f;    // Secondi per frame
        private int itemAnimFrame = 0;          // Frame corrente animazione casse

        // ── Progressione livelli ─────────────────────────────────────────────
        private int currentLevel = 0;
        private bool levelComplete = false;     // Flag sicuro per cambiare livello fuori dal foreach

        // ── Sistema chiave (livello 5+) ──────────────────────────────────────
        private HashSet<Point> keyTiles = new(); // Tile che droppano la chiave
        private bool hasKey = false;

        // ────────────────────────────────────────────────────────────────────
        // Restituisce uno dei 4 angoli della mappa come punto di spawn casuale.
        // Gli angoli sono sempre liberi da ostacoli grazie a ClearSpawnArea in TileMap.
        // ────────────────────────────────────────────────────────────────────
        private Point GetRandomCornerSpawn()
        {
            Point[] corners = {
                new Point(1, 1),    // angolo top-left
                new Point(37, 1),   // angolo top-right
                new Point(1, 21),   // angolo bottom-left
                new Point(37, 21)   // angolo bottom-right
            };
            return corners[new Random().Next(corners.Length)];
        }

        public Game1() : base("PoopMan", 1248, 767, false) { }

        protected override void Initialize()
        {
            base.Initialize();
        }

        // ────────────────────────────────────────────────────────────────────
        // Carica tutte le risorse grafiche e inizializza gli oggetti di gioco.
        // ────────────────────────────────────────────────────────────────────
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            scoreFont = Content.Load<SpriteFont>("font/Score");

            // Carica il tileset e costruisce l'atlas dei tile
            Texture2D tilesetTexture = Content.Load<Texture2D>("image/Tile/terrain");
            atlas = new TileAtlas(tilesetTexture);

            string xmlPath = Path.Combine(Content.RootDirectory, "image", "Tile", "TilesetAtlas.xml");
            XDocument doc = XDocument.Load(xmlPath);
            foreach (var sprite in doc.Descendants("Region"))
            {
                string name = sprite.Attribute("Name").Value;
                int x = (int)sprite.Attribute("X");
                int y = (int)sprite.Attribute("Y");
                int w = (int)sprite.Attribute("Width");
                int h = (int)sprite.Attribute("Height");
                atlas.AddTile(name, x, y, w, h);
            }

            // Crea mappa e collegamento evento rottura tile → drop casse
            currentSpawnPoint = GetRandomCornerSpawn();
            map = new TileMap(atlas, 23, 39, level: currentLevel);
            map.TileBroken += HandleChestDrop;

            // Crea il miner in un angolo casuale
            string minerXml = Path.Combine(Content.RootDirectory, "image", "character", "miner_animation.xml");
            miner = new Miner(currentSpawnPoint, minerXml, Content);
            minerHudTexture = Content.Load<Texture2D>("image/character/miner");

            // Quando il miner perde una vita, respawna dove è morto
            miner.NeedsRespawn += (s, e) => miner.Respawn(miner.TilePosition);

            LoadItemAnimations();
            SpawnBats(currentLevel);
            InitChests();
        }

        // ────────────────────────────────────────────────────────────────────
        // Loop principale di gioco: gestisce collisioni, item, esplosioni,
        // aggiornamento entità e progressione livello.
        // ────────────────────────────────────────────────────────────────────
        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Quando il game over è attivo blocca tutto
            if (showGameOver)
                return;

            // ── Collisione miner ↔ bat ──────────────────────────────────────
            // Collisione miner ↔ bat — ignorata durante invincibilità
            if (!miner.IsDead && !miner.IsInvincible && bats != null)
            {
                foreach (var b in bats)
                {
                    var batCollision = b.GetBounds();
                    var minerCollision = miner.GetBounds();
                    if (batCollision != Collision.Empty &&
                        minerCollision != Collision.Empty &&
                        batCollision.Intersects(minerCollision))
                    {
                        miner.Kill();
                        break;
                    }
                }
            }
            // ── Collisione esplosione ↔ miner ───────────────────────────────────
            if (!miner.IsDead && !miner.IsInvincible)
            {
                foreach (var explosionTile in miner.ActiveExplosionTiles)
                {
                    if (miner.TilePosition == explosionTile)
                    {
                        miner.Kill();
                        break;
                    }
                }
            }
            // ── Miner su item droppato ──────────────────────────────────────
            if (!miner.IsDead && !miner.IsInvincible)
            {
                // Resetta il flag JustSpawned dopo il primo frame
                foreach (var it in droppedItems.Values)
                    it.JustSpawned = false;

                if (droppedItems.TryGetValue(miner.TilePosition, out var item)
                    && !item.IsOpen && !item.JustSpawned)
                {
                    if (item.Type == "door")
                    {
                        // Dal livello 5 serve la chiave
                        if (currentLevel >= 5 && !hasKey)
                        {
                            // Porta bloccata, non aprire
                        }
                        else if (!item.IsOpening)
                        {
                            item.IsOpening = true;
                        }
                    }
                    else if (item.Type == "chest_tnt")
                    {
                        item.IsOpen = true;
                        miner.AddBigBomb();
                        droppedItems.Remove(miner.TilePosition);
                    }
                    else if (item.Type == "key")
                    {
                        item.IsOpen = true;
                        hasKey = true;
                        droppedItems.Remove(miner.TilePosition);
                    }
                }
            }

            // ── Animazione apertura porta ───────────────────────────────────
            foreach (var item in droppedItems.Values.Where(i => i.IsOpening))
            {
                item.OpeningTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (item.OpeningTimer >= itemAnimSpeed)
                {
                    item.OpeningTimer = 0f;
                    item.OpeningFrame++;

                    // Animazione completata (3 frame: 0, 1, 2) → cambia livello
                    if (item.OpeningFrame >= 3)
                    {
                        item.IsOpen = true;
                        item.IsOpening = false;
                        levelComplete = true; // Usa flag per evitare modifica durante foreach
                    }
                }
            }

            // Cambia livello fuori dal foreach per evitare InvalidOperationException
            if (levelComplete)
            {
                levelComplete = false;
                GoToNextLevel();
            }

            // ── Collisione esplosione ↔ bat ─────────────────────────────────
            if (!miner.IsDead && bats != null)
            {
                killStreak = 0;

                foreach (var explosionTile in miner.ActiveExplosionTiles)
                {
                    foreach (var b in bats)
                        if (b.TilePosition == explosionTile && !b.IsDead && !b.IsInvincible)
                        {
                            b.Kill();
                            killStreak++;
                        }
                }

                // Calcolo punteggio con bonus streak
                if (killStreak == 1)
                    score += 50;
                else if (killStreak >= 2)
                    score += killStreak * 25 + (killStreak - 1) * 50;
            }

            // ── Aggiornamento miner (sempre, anche da morto per l'animazione) ─
            miner.Update(map, gameTime);

            if (miner.IsDead)
            {
                if (miner.IsDeathAnimationFinished)
                    showGameOver = true;
                return; // I bat non si aggiornano mentre il miner è morto
            }

            // ── Aggiornamento e pulizia bat ─────────────────────────────────
            if (bats != null)
            {
                foreach (var b in bats)
                    b.Update(map, gameTime);

                // Rimuovi i bat la cui animazione di morte è terminata
                bats.RemoveAll(b => b.IsDeathAnimationFinished);
            }

            // ── Timer animazione casse a terra ──────────────────────────────
            itemAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (itemAnimTimer >= itemAnimSpeed)
            {
                itemAnimTimer = 0f;
                itemAnimFrame++;
            }

            base.Update(gameTime);
        }

        // ────────────────────────────────────────────────────────────────────
        // Disegna HUD, mappa, item droppati, miner e bat.
        // Usa una matrice di traslazione per spostare il gioco sotto l'HUD.
        // ────────────────────────────────────────────────────────────────────
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // ── HUD (senza offset) ──────────────────────────────────────────────
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // SCORE
            _spriteBatch.DrawString(scoreFont, $"SCORE: {score}", new Vector2(10, 8), Color.Yellow);

            // VITE: icona miner ripetuta per ogni vita
            Rectangle minerFrame = new Rectangle(128, 32, 32, 32); // idle_front0
            for (int i = 0; i < miner.Lives; i++)
                _spriteBatch.Draw(minerHudTexture, new Vector2(200 + i * 28, 0), minerFrame, Color.White, 0f,
                    Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

            // BOMBE GRANDI: icona + quantità
            Rectangle bigTntFrame = new Rectangle(96, 0, 32, 32); // big_tnt0
            _spriteBatch.Draw(itemTexture, new Vector2(400, 0), bigTntFrame, Color.White, 0f,
                Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(scoreFont, $"X {miner.BigBombCount}", new Vector2(430, 8), Color.Orange);

            // LIVELLO
            _spriteBatch.DrawString(scoreFont, $"LIVELLO: {currentLevel}", new Vector2(600, 8), Color.Cyan);

            // CHIAVE: solo dal livello 5
            if (currentLevel >= 5)
            {
                Rectangle keyFrame = new Rectangle(96, 96, 32, 32); // key0
                if (hasKey)
                    _spriteBatch.Draw(itemTexture, new Vector2(850, 0), keyFrame, Color.White, 0f,
                        Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
                else
                    _spriteBatch.Draw(itemTexture, new Vector2(850, 0), keyFrame, Color.Gray * 0.4f, 0f,
                        Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            }

            _spriteBatch.End();
            // ── Gioco (offset verso il basso di HudHeight pixel) ────────────
            var transform = Matrix.CreateTranslation(0, HudHeight, 0);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);

            map.Draw(_spriteBatch);

            // ── Item droppati animati ───────────────────────────────────────
            foreach (var item in droppedItems)
            {
                string animKey;
                int frame = 0;

                if (item.Value.Type == "door")
                {
                    if (item.Value.IsOpening)
                    {
                        animKey = "door_key";       // animazione apertura con chiave
                        frame = item.Value.OpeningFrame;
                    }
                    else
                    {
                        animKey = (currentLevel >= 5) ? "door_key_closed" : "door_closed";
                        frame = 0;
                    }
                }
                else if (item.Value.Type == "key")
                {
                    animKey = "key";
                    frame = itemAnimFrame % (itemAnimations.ContainsKey("key")
                             ? itemAnimations["key"].Count : 1);
                }
                else
                {
                    animKey = "chest";
                    frame = itemAnimFrame % (itemAnimations.ContainsKey("chest")
                             ? itemAnimations["chest"].Count : 1);
                }

                if (!itemAnimations.TryGetValue(animKey, out var frames) || frames.Count == 0)
                    continue;

                frame = Math.Min(frame, frames.Count - 1);
                Vector2 pos = new Vector2(item.Key.X * TileMap.TileSize,
                                           item.Key.Y * TileMap.TileSize);
                _spriteBatch.Draw(itemTexture, pos, frames[frame], Color.White);
            }

            miner.Draw(_spriteBatch);

            if (bats != null)
                foreach (var b in bats)
                    b.Draw(_spriteBatch);

            // Schermo nero in caso di game over
            if (showGameOver)
                GraphicsDevice.Clear(Color.Black);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        // ────────────────────────────────────────────────────────────────────
        // Spawna un numero di bat proporzionale al livello corrente,
        // evitando di posizionarli vicino al miner.
        // ────────────────────────────────────────────────────────────────────
        private void SpawnBats(int level)
        {
            bats = new List<Bat>();
            int count = 1 + level; // livello 1 → 2 bat, livello 2 → 3, ecc.
            string batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
            Random rand = new Random();
            int attempts = 0;

            while (bats.Count < count && attempts < 1000)
            {
                attempts++;
                int tx = rand.Next(1, 38);
                int ty = rand.Next(1, 22);
                Point tile = new Point(tx, ty);

                // Distanza minima di 4 tile dal miner
                if (Math.Abs(tx - miner.TilePosition.X) < 4 &&
                    Math.Abs(ty - miner.TilePosition.Y) < 4)
                    continue;

                if (!map.IsWalkable(tile))
                    continue;

                bats.Add(new Bat(tile, batXml, Content, map));
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Chiamato da TileMap.TileBroken quando un tile breakable viene distrutto.
        // Se il tile nascondeva una cassa, genera il drop appropriato, e chiave.
        // ────────────────────────────────────────────────────────────────────
        private void HandleChestDrop(Point tile)
        {
            // Drop chiave (livello 5+)
            if (currentLevel >= 5 && keyTiles.Contains(tile))
            {
                keyTiles.Remove(tile);
                droppedItems[tile] = new DroppedItem { Type = "key", IsOpen = false, JustSpawned = true };
                return;
            }

            if (!chestTiles.Contains(tile)) return;
            chestTiles.Remove(tile);

            Random rand = new Random();
            int roll = rand.Next(100);

            if (roll < 5)
            {
                droppedItems[tile] = new DroppedItem { Type = "chest_tnt", IsOpen = false };
            }
            else if (roll < 35)
            {
                string batXml = Path.Combine(Content.RootDirectory, "image", "enemies", "bat.xml");
                Point[] neighbors = {
                new Point(tile.X + 1, tile.Y),
                new Point(tile.X - 1, tile.Y),
                new Point(tile.X, tile.Y + 1),
                new Point(tile.X, tile.Y - 1)
                };
                foreach (var n in neighbors)
                {
                    if (map.IsWalkable(n))
                    {
                        try
                        {
                            var newBat = new Bat(n, batXml, Content, map);
                            newBat.SetInvincible(1.6f);
                            bats.Add(newBat);
                        }
                        catch { }
                        break;
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Segna casualmente il 5% dei tile breakable come "casse nascoste" e chiave dal livello 5 in poi.
        // ────────────────────────────────────────────────────────────────────
        private void InitChests()
        {
            Random rand = new Random();
            var breakableTiles = new List<Point>();

            for (int y = 0; y < 23; y++)
                for (int x = 0; x < 39; x++)
                {
                    Point t = new Point(x, y);
                    if (map.GetTile(t) == TileType.Breakable)
                        breakableTiles.Add(t);
                }

            breakableTiles = breakableTiles.OrderBy(_ => rand.Next()).ToList();

            foreach (var tile in breakableTiles)
                if (rand.Next(100) < 40)
                    chestTiles.Add(tile);

            // Dal livello 5: sceglie UN breakable casuale che droppa la chiave (100%)
            if (currentLevel >= 5)
            {
                hasKey = false;
                var nonChestBreakables = breakableTiles.Where(t => !chestTiles.Contains(t)).ToList();
                if (nonChestBreakables.Count > 0)
                {
                    Point keyTile = nonChestBreakables[rand.Next(nonChestBreakables.Count)];
                    keyTiles.Add(keyTile);
                }
            }

            SpawnDoor();
        }

        /// <summary>
        /// Trova un tile walkable a distanza >= 10 dal miner e ci piazza la porta visibile.
        /// </summary>
        private void SpawnDoor()
        {
            var candidates = new List<Point>();

            for (int y = 1; y < 22; y++)
                for (int x = 1; x < 38; x++)
                {
                    Point t = new Point(x, y);
                    if (!map.IsWalkable(t)) continue;

                    float dist = Vector2.Distance(
                        new Vector2(t.X, t.Y),
                        new Vector2(miner.TilePosition.X, miner.TilePosition.Y));

                    if (dist >= 10f)
                        candidates.Add(t);
                }

            if (candidates.Count == 0) return; // Fallback: nessun tile valido

            Random rand = new Random();
            Point doorTile = candidates[rand.Next(candidates.Count)];

            // Aggiunge direttamente ai droppedItems come porta chiusa
            droppedItems[doorTile] = new DroppedItem { Type = "door", IsOpen = false, JustSpawned = false };
            doorSpawned = true;
            doorPosition = doorTile;
        }

        // ────────────────────────────────────────────────────────────────────
        // Carica le animazioni degli item (casse, porte, TNT) dall'XML.
        // I nomi con numeri finali (chest0..8) vengono raggruppati per animazione.
        // ────────────────────────────────────────────────────────────────────
        private void LoadItemAnimations()
        {
            string xmlPath = Path.Combine(Content.RootDirectory, "image", "items", "items.xml");
            XDocument doc = XDocument.Load(xmlPath);

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value
                              ?? "image/items/items";
            itemTexture = Content.Load<Texture2D>(texturePath);

            foreach (var region in doc.Descendants("Region"))
            {
                string fullName = region.Attribute("Name")?.Value ?? "";
                int x = int.Parse(region.Attribute("X")?.Value ?? "0");
                int y = int.Parse(region.Attribute("Y")?.Value ?? "0");
                int width = int.Parse(region.Attribute("Width")?.Value ?? "32");
                int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

                // Rimuove i numeri finali per ottenere il nome base dell'animazione
                // es. "chest3" → "chest", "door_opening1" → "door_opening"
                int i = fullName.Length;
                while (i > 0 && char.IsDigit(fullName[i - 1]))
                    i--;
                string animName = fullName.Substring(0, i).TrimEnd('_', '-', ' ');
                if (string.IsNullOrEmpty(animName))
                    animName = fullName;

                if (!itemAnimations.ContainsKey(animName))
                    itemAnimations[animName] = new List<Rectangle>();

                var rect = new Rectangle(x, y, width, height);
                if (!itemAnimations[animName].Contains(rect))
                    itemAnimations[animName].Add(rect);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Avanza al livello successivo: reimposta mappa, miner, bat e casse.
        // ────────────────────────────────────────────────────────────────────
        private void GoToNextLevel()
        {
            currentLevel++;
            doorSpawned = false;
            droppedItems.Clear();
            chestTiles.Clear();
            keyTiles.Clear(); 
            hasKey = false;   
            score += 500;

            map = new TileMap(atlas, 23, 39, level: currentLevel);
            map.TileBroken += HandleChestDrop;

            miner.ResetForNewLevel(currentSpawnPoint);

            SpawnBats(currentLevel);
            InitChests();
        }
    }
}
