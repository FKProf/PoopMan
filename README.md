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
  - `Scenes/` — `TitleScene` (animated title screen), `GameScene` (gameplay loop), `VisualEffectSystem` (VFX)
  - `GameObjects/` — `Miner` (player), `Bat` (enemy), `Bomb` (small & big)
  - `UI/` — `GameHud`, `GameOverlay`, `PauseMenu`, `AudioSettingsPanel`, `BatEncyclopedia`
- `PoopManLibrary/` — shared game logic
  - `Core.cs` — game bootstrap (window, graphics, content, scene management)
  - `Collision.cs` — circular hitbox system
  - `Audio/` — `AudioController` (music & SFX mixer)
  - `Graphics/` — `Sprite`, `AnimatedSprite`, `TextureRegion`, `Animation`
  - `Input/` — `InputManager`, `KeyboardInfo`, `MouseInfo`
  - `Scenes/Scene.cs` — base scene class
  - `World/` — `TileMap`, `TileAtlas`, `TileType`

## Features

### Map & Themes

- Procedural tile map: `23 × 39` tiles at `32 px` each
- **Six visual themes** that rotate by level: **Forest → Cave → Lava → Ice → Swamp → Ruins**
- Liquid tile animations: `water`, `swamp_water`, and `lava` alternate between two frames every 0.55 s
- Biome-specific environment generation: water blobs, lava pools, swamp patches, ruins rubble
- Tile types: `Wall` (indestructible), `Breakable` (destructible), `Empty` (walkable)
- All four corners are always cleared for safe spawning
- Atlas-driven tile rendering via `terrain.png` + `TilesetAtlas.xml`

### Player (Miner)

- Grid-based movement with smooth pixel interpolation
- Directional idle/walk animations loaded from XML
- 3 lives; respawns in place with 2 s of blinking invincibility after each hit
- Supports up to 6 HP (increased via upgrades)

### Bombs

- **Small bomb** (`Space`): fuse 2 s, base radius 1 tile
- **Big bomb** (`X`): fuse 2 s, base radius 2 tiles
- Explosions propagate orthogonally, break `Breakable` tiles, stop at `Wall` tiles
- Big bombs can be collected as chest drops
- Particle VFX on explosion (colour-coded by type)

### Enemies (Bat)

Seven **bat variants**, each with a unique ability, unlocked progressively:

| Variant | Unlocks | Ability |
|---------|---------|---------|
| Normal | Lv 0 | Standard AI |
| Dasher | Lv 5 | Rapid dash (3 s cooldown, 25 % chance) |
| Walid | Lv 8 | Explodes on death — 6×6 tile area |
| Ghost | Lv 10 | Phases through solid bombs (2 s, 8 s cooldown) |
| Splitter | Lv 15 | Splits into 2 faster mini-bats on death |
| Berserk | Lv 16 | Speed ×2.2 when within 3 tiles of the miner |
| Nuke | Lv 20 | Explodes on death — 12×12 tile area (instant kill) |

- Difficulty scales with level: speed, chase chance, sight range, and HP all increase
- From level 20 onward, multiple variants can appear in the same wave
- HP cap per bat: 1 HP (lv 0–19), scaling up to 6 HP at lv 40+

### Chest System

- ~40 % of breakable tiles hide a chest
- Breaking a chest rolls a loot table:
  - **5 %** — drop a big bomb power-up
  - **30 %** — spawn a temporarily invincible bat
  - **65 %** — nothing

### Upgrade System

An upgrade menu appears every **3 levels**, offering 3 random choices.  
Each upgrade tracks its own level counter and is filtered from the pool when capped.

**Vita:**
| Upgrade | Effect |
|---------|--------|
| +1 Vita | Instant extra life |
| Vita Max+ | Raises the life cap and grants 1 life (max 3 lv) |
| Rigenerazione | Recover 1 life every 5 levels |

**Offensivi:**
| Upgrade | Effect |
|---------|--------|
| Danno+ | Explosion radius +1 tile (max 4 lv) |
| Potenza | +1 bomb damage every 2 levels (max 6 lv) |
| Miccia Corta | Fuse timer −0.4 s (max 4 lv) |
| Bomba+ | +1 simultaneous bomb (max 3 lv) |
| Catena | +15 % chance enemy death triggers a mini-explosion (max 4 lv) |

**Movimento:**
| Upgrade | Effect |
|---------|--------|
| Velocità | +20 px/s move speed (max 6 lv) |
| Adrenalina | +40 % speed for 3 s after taking damage |

**Difensivi:**
| Upgrade | Effect |
|---------|--------|
| Resistenza | +1 s invincibility after respawn (cumulative, max 7 s) |
| Armatura | +0.5 s invincibility on hit (cumulative, max 7 s) |
| Scudo | Absorbs 1 hit; recharges every 5 levels |

**Speciali:**
| Upgrade | Effect |
|---------|--------|
| Pass-Through | Explosions ignore breakable tiles |
| Critico | 20 % chance to instantly kill a bat on contact (double points) |
| Calamita | Auto-collects items within 3 tiles |
| Shockwave | Nearby bats are stunned 1.5 s after an explosion |
| Rallenta | Nearby bats slowed 40 % for 3 s after an explosion |
| Fortuna | +15 % bonus loot chance from chests (max 4 lv) |
| Bottino | +15 % chance bat drops an item on kill (max 4 lv) |

### Level Progression

- A **door** spawns on every level; step into it to advance (+500 pts)
- From level 5 onward a **key** must be collected before the door opens
- The upgrade menu appears at levels 3, 6, 9, …

### Scoring

- Kill 1 bat: **+100 pts**
- Multi-kill (N bats, same explosion): `N × 25 + (N − 1) × 50` pts
- Level clear: **+500 pts**

### HUD & UI

- Top HUD bar: score, HP icons, big-bomb count, map theme, level, key indicator
- **Pause menu** (`Esc`) with Audio and Bat Encyclopedia sub-pages
- **Bat Encyclopedia** — scrollable card gallery of all seven bat variants with mouse & keyboard navigation
- **Audio Settings** — independent Music / SFX volume sliders with mute toggle, available in-game and on the title screen
- **Game Over overlay** with final score (`R` / `Enter` to restart, `Esc` to title)
- **Level flash** on completion (1.8 s, with theme name every 3 levels)
- **Title screen** with parallax scrolling clouds, main menu, instructions, and audio overlay

### Window & Rendering

- Resizable window with **letterbox scaling** (fixed map-world aspect ratio)
- `F11` toggles fullscreen
- `SamplerState.PointClamp` throughout (pixel-perfect rendering)
- Post-process VFX layer: heat distortion on Lava theme, shockwave ring on Nuke explosions

## Controls

| Key | Action |
|-----|--------|
| `W` / `A` / `S` / `D` or Arrow keys | Move |
| `Space` | Place small bomb |
| `X` | Place big bomb (if available) |
| `Esc` | Pause / back to title |
| `Enter` | Confirm / start / restart |
| `R` | Restart (game over screen) |
| `F11` | Toggle fullscreen |

> Movement is non-diagonal; input is buffered (up to 2 queued moves).

## Assets

| Path | Description |
|------|-------------|
| `Content/image/Tile/terrain` | Tile spritesheet (atlas-driven) |
| `Content/image/Tile/TilesetAtlas.xml` | Tile atlas metadata |
| `Content/image/character/miner_animation.xml` | Miner animation metadata |
| `Content/image/character/miner` | Miner HUD icon |
| `Content/image/enemies/bat.xml` | Bat animation metadata |
| `Content/image/items/items.xml` | Items / UI animation metadata |
| `Content/image/backgound/1`, `2`, `3` | Title screen background layers |
| `Content/image/fxs/` | Visual effect sprites |
| `Content/font/Score` | Bitmap font used for all UI text |
| `Content/fx/heatdistort.fx` | Lava heat-distortion shader |
| `Content/fx/shockwave.fx` | Nuke shockwave shader |

## Build and Run

1. Open `PoopMan.sln` in Visual Studio 2022 or later.
2. Restore NuGet packages.
3. Set `PoopMan` as the Startup Project.
4. Build and run (`F5`).

> Requires the **MonoGame Content Builder** (MGCB) to compile assets before the first run.

## Roadmap

- Deterministic map seeds for reproducible levels
- Win condition / ending screen beyond infinite level loop
- Additional bat types and advanced pathfinding
- Gamepad / controller support

