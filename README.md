# PoopMan

**PoopMan Miner** is a 2D tile-based action game built with MonoGame on .NET 9.  
Place bombs to destroy breakable tiles, eliminate bat enemies, collect power-ups, and find the exit door to advance through procedurally-generated levels.

## Tech Stack

- .NET 9 (`net9.0-windows` for the game executable)
- C# 13
- MonoGame 3.8 (`MonoGame.Framework.WindowsDX`)

## Solution Structure

- `PoopMan/` — game executable
  - `Game1.cs` — window setup, fullscreen toggle, letterbox scaling
  - `GameController.cs` — input abstraction (keyboard bindings)
  - `Scenes/` — `TitleScene` (animated title screen), `GameScene` (gameplay loop)
  - `GameObjects/` — `Miner` (player), `Bat` (enemy), `Bomb` (small & big)
  - `UI/` — `GameHud` (score / HP / bombs / theme / level), `GameOverlay` (pause, game over, level flash)
- `PoopManLibrary/` — shared game logic
  - `Core.cs` — game bootstrap (window, graphics, content, scene management)
  - `Collision.cs` — circular hitbox system
  - `Graphics/` — `Sprite`, `AnimatedSprite`, `TextureRegion`, `Animation`
  - `Input/` — `InputManager`, `KeyboardInfo`, `MouseInfo`
  - `Scenes/Scene.cs` — base scene class
  - `World/` — `TileMap`, `TileAtlas`, `TileType`

## Current Features

### Map & Themes
- Procedural tile map: `23 × 39` tiles at `32 px` each (base resolution `736 × 1248`)
- Four visual themes that rotate every 3 levels: **Forest → Cave → Stone → Desert**
- Tile types: `Wall` (indestructible), `Breakable` (destructible), `Empty` (walkable)
- All four corners are always cleared for safe spawning

### Player (Miner)
- Grid-based movement with smooth pixel interpolation
- Directional idle/walk animations loaded from XML
- 3 lives; respawns in place with 2 s of blinking invincibility after each hit

### Bombs
- **Small bomb** (`Space`): fuse 2 s, explosion radius 1 tile per direction
- **Big bomb** (`X`): fuse 2 s, explosion radius 2 tiles per direction
- Explosions propagate orthogonally, break `Breakable` tiles, and stop at `Wall` tiles
- Big bombs can be collected as chest drops

### Enemies (Bat)
- Spawn count: `1 + current level` bats per level
- Movement AI: 55 % chance to chase the player, 45 % random
- Smooth tile-to-tile interpolation at 130 px/s
- Directional animations (`fly_front/back/left/right`, `idle`, `dead`)
- Touching a bat or standing on an active explosion tile kills the miner

### Chest System
- ~40 % of breakable tiles hide a chest
- Breaking a chest tile rolls a loot table:
  - **5 %** — drop a big bomb power-up
  - **30 %** — spawn a bat (temporarily invincible) from the tile
  - **65 %** — nothing

### Level Progression
- A **door** spawns on every level; step into it to advance
- From level 5 onward a **key** is hidden inside a breakable tile; the key must be collected before the door opens
- Completing a level awards **+500 points**

### Scoring
- Kill 1 bat with a single explosion: **+100 pts**
- Multi-kill (N bats, same explosion): `N × 25 + (N − 1) × 50` pts

### HUD & UI
- Top HUD bar: score, HP icons, big-bomb count, map theme, level, key indicator
- **Pause overlay** (`Esc`)
- **Game Over overlay** with final score (`R` / `Enter` to restart, `Esc` to title)
- **Level flash** on completion (1.8 s, with theme name on every 3rd level)
- **Title screen** with parallax scrolling clouds and key hints

### Window & Rendering
- Resizable window with **letterbox scaling** to maintain `4:3`-ish aspect ratio
- `F11` toggles fullscreen
- `SamplerState.PointClamp` throughout (no texture bleeding)

## Controls

| Key | Action |
|-----|--------|
| `W` / `A` / `S` / `D` or Arrow keys | Move |
| `Space` | Place small bomb |
| `X` | Place big bomb (if available) |
| `Esc` | Pause / back to title (game over) |
| `Enter` | Start game (title) / restart (game over) |
| `R` | Restart (game over screen) |
| `F11` | Toggle fullscreen |

> Movement is non-diagonal; input is buffered (up to 2 queued moves).

## Assets

Runtime-loaded content (must be included in the MonoGame content pipeline):

| Path | Description |
|------|-------------|
| `Content/image/Tile/terrain` | Tile spritesheet |
| `Content/image/Tile/TilesetAtlas.xml` | Tile atlas metadata |
| `Content/image/character/miner_animation.xml` | Miner animation metadata |
| `Content/image/character/miner` | Miner HUD icon |
| `Content/image/enemies/bat.xml` | Bat animation metadata |
| `Content/image/items/items.xml` | Items / UI animation metadata |
| `Content/image/backgound/1`, `2`, `3` | Title screen background layers |
| `Content/image/fxs/` | Visual effect sprites |
| `Content/font/Score` | Bitmap font used for all UI text |

## Build and Run

1. Open `PoopMan.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Set `PoopMan` as the Startup Project.
4. Build and run (`F5`).

## Roadmap

- Audio: sound effects and background music
- Visual effects polish (particles, screen shake)
- Deterministic map seeds for reproducible levels
- Additional enemy types and pathfinding improvements
- Win condition / ending screen beyond infinite level loop
