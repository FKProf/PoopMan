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
  - `GameController.cs`, `InputBindings.cs` — input abstraction and rebindable key/button mapping
  - `Scenes/` — `TitleScene` (animated title screen), `GameScene` (gameplay loop), `GolemAttackSystem` (rocks & lasers), `LightingSystem`, `ParticleSystem`, `BiomeAtmosphere`, `VisualEffectSystem` (VFX)
  - `GameObjects/` — `Miner` (player), `Bat` (enemy; `Bat.Golem.cs` adds the golem family), `Bomb` (small & big), `MinerUpgrade` (upgrade registry & gameplay state), `MinerUpgradeDescriptions` (encyclopedia copy)
  - `UI/` — `GameHud`, `GameOverlay`, `PauseMenu`, `AudioSettingsPanel`, `ControlsSettingsPanel`, `Leaderboard` / `LeaderboardScreen` / `NameEntryScreen`, `BatEncyclopedia` (3-tab encyclopedia: Nemici · Upgrade · Biomi)
- `PoopManLibrary/` — shared game logic
  - `Core.cs` — game bootstrap (window, graphics, content, scene management)
  - `Collision.cs` — circular hitbox system
  - `Audio/` — `AudioController` (music & SFX mixer)
  - `Graphics/` — `Sprite`, `AnimatedSprite`, `TextureRegion`, `Animation`
  - `Input/` — `InputManager`, `KeyboardInfo`, `MouseInfo`
  - `Scenes/Scene.cs` — base scene class
  - `World/` — `TileMap`, `TileAtlas`, `TileType`
- `tests/` — Windows auto-play bot (`PoopMan.AutoPlay`) and smoke test (see [Testing & CI](#testing--ci))

## Features

### Map & Themes

- Procedural tile map: `23 × 39` tiles at `32 px` each
- **Six visual themes** that rotate by level: **Forest → Cave → Lava → Ice → Swamp → Ruins**
- Liquid tile animations: `water`, `swamp_water`, and `lava` alternate between two frames every 0.55 s
- Biome-specific environment generation: water blobs, lava pools, swamp patches, ruins rubble
- Tile types: `Wall` (indestructible), `Breakable` (destructible), `Empty` (walkable)
- All four corners are always cleared for safe spawning
- The exit door and the key are always placed on tiles **reachable** from the spawn (possibly by breaking blocks) — no more levels sealed off by water or lava
- Atlas-driven tile rendering via `terrain.png` + `TilesetAtlas.xml`
- Each biome has its own **door sprites** (`doors.png` + `doors.xml`, with and without key)

### Player (Miner)

- Grid-based movement with smooth pixel interpolation
- Directional idle/walk animations loaded from XML
- 3 lives; respawns in place with 2 s of blinking invincibility after each hit
- Supports up to 6 HP (increased via upgrades)

### Bombs

- **Small bomb** (`Space`): fuse 2 s, base radius 1 tile
- **Big bomb** (`X`): fuse 2 s, base radius 2 tiles
- Explosions propagate orthogonally, break `Breakable` tiles, stop at `Wall` tiles
- **Chain reactions**: an explosion that reaches another bomb detonates it instantly (Walid/Nuke blasts and chain mini-explosions trigger bombs too)
- Big bombs can be collected as chest drops
- Particle VFX on explosion (colour-coded by type)

### Enemies (Bat)

Eight **bat variants**, each with a unique ability, unlocked progressively:

| Variant | Unlocks | Ability |
|---------|---------|---------|
| Normal | Lv 0 | Standard AI; 1 HP until lv 19 |
| Dasher | Lv 5 | Rapid dash (3 s cooldown, 25 % chance) |
| Walid | Lv 8 | Explodes on death — 6×6 tile area |
| Ghost | Lv 10 | Phases through solid bombs (2 s, 8 s cooldown) |
| Splitter | Lv 15 | Splits into 2 faster mini-bats on death |
| Berserk | Lv 16 | Speed ×2.2 when within 3 tiles of the miner |
| Robusto | Lv 20 | Same as Normal but armoured: 2–6 HP depending on level |
| Nuke | Lv 20 | Explodes on death — 12×12 tile area (instant kill on all bats in range) |

- Difficulty scales with level: speed, chase chance, sight range, and HP all increase
- Bat count per wave is `1 + level`, capped at **28**; beyond that difficulty grows through HP, speed and special variants
- From level 20 onward, multiple variants can appear in the same wave
- **Robusto** HP progression: +1 HP every 5 levels past 20, cap 6 HP at lv 40+
- The **exit door** and the **key** are always immune to Nuke explosions

### Golems (new world, from level 30)

From **level 30** part of every wave (30 % at lv 30, +2 % per level, max 60 %) is replaced by **golems**: stone enemies that walk instead of flying. They reuse the bat AI, pathfinding and damage rules, and add their own abilities:

| Golem | Unlocks | Ability |
|-------|---------|---------|
| Normal | Lv 30 | No ability; slower but tougher than bats (appears twice as often as the others) |
| Booler | Lv 30 | Throws a rock when aligned with the miner (flies over water and lava, shatters on walls and blocks) |
| Brute | Lv 32 | Extra HP; 35 % chance to absorb a hit, then goes into a faster **rage** |
| Rhino | Lv 34 | Charges in a straight line: stuns the miner on hit, stuns itself against an indestructible wall, smashes breakable blocks |
| Laser | Lv 36 | Charges (red telegraph line) and fires a laser that hurts, destroys breakable blocks and detonates bombs |
| Godness | Lv 35 *(rare boss)* | All golem and bat abilities, sees through walls, can phase through them (not the border). **Touching it is an instant game over**; max 2 HP, explodes on death |

- **Godness** spawn chance: 2 % at lv 35, +0.4 % per level, capped at 8 %
- Golem kills give more points than bats (see [Scoring](#scoring))

### Chest System

- ~40 % of breakable tiles hide a chest
- Breaking a chest rolls a loot table:
  - **5 %** — drop a big bomb power-up
  - **30 %** — spawn a temporarily invincible bat
  - **65 %** — nothing

### Upgrade System

An upgrade menu appears every **3 levels**, offering 3 random choices.  
Each upgrade tracks its own level counter and is filtered from the pool when capped.
Upgrades that would have no effect (e.g. *+1 Vita* with full lives, invincibility already at the 7 s cap) are not offered.

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
| Potenza | +1 small-bomb damage per level — lv 0 = 1 dmg, lv 5 = 6 dmg (max 5 lv) |
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
| Scudo | Absorbs 1 hit; recharges every 3 levels |

**Speciali:**
| Upgrade | Effect |
|---------|--------|
| Pass-Through | Explosions ignore breakable tiles |
| Critico | 20 % chance to instantly kill a bat on contact (double points) |
| Calamita | Auto-collects items within 3 tiles |
| Detonatore | Your bombs have no fuse: they stay armed until you press `C` (gamepad `Y`) — or another explosion sets them off |
| Shockwave | Nearby bats are stunned 1.5 s after an explosion |
| Rallenta | Nearby bats slowed 40 % for 3 s after an explosion |
| Fortuna | +15 % bonus loot chance from chests (max 4 lv) |
| Bottino | +15 % chance bat drops an item on kill (max 4 lv) |

**Mythic** *(0.5 % probability per slot — extremely rare)*:
| Upgrade | Effect |
|---------|--------|
| Mythic Immortality | Complete immunity to own bomb explosions (small, big, and chain) |
| Instant Kill | Small bombs instantly kill any bat regardless of HP |

### Level Progression

- A **door** spawns on every level; step into it to advance (+500 pts)
- From level 5 onward a **key** must be collected before the door opens
- The upgrade menu appears at levels 3, 6, 9, …

### Scoring

- Kill 1 bat: **+100 pts**
- Multi-kill (N bats, same explosion): `N × 25 + (N − 1) × 50` pts
- Level clear: **+500 pts**
- Golems: Normal **+250**, Booler **+350**, Brute / Rhino **+450**, Laser **+500**, Godness **+5000**

### HUD & UI

- Top HUD bar: score, HP icons (with golden border when **Mythic Immortality** is active), big-bomb count, map theme, level, **available small-bomb slots**, key indicator
- Floating score popups (`+100`, `CRIT!`, `COMBO x3`), pickup texts (`CHIAVE!`, `+1 TNT`) and the name of the upgrade just chosen
- Small-bomb icon pulses with a special effect when **Instant Kill** is active
- Skull/explosion ability icon shown when a Mythic upgrade is held
- **Pause menu** (`Esc`): Riprendi · Enciclopedia · Audio · Comandi · Menu principale
- **Controls panel** (*Comandi*, from the title screen or the pause menu): rebind two keyboard keys and one gamepad button per action (move, small/big bomb, detonate, pause). Bindings are saved to `%APPDATA%\PoopMan\controls.cfg`
- **Leaderboard** (*Classifica*): top 20 scores, stored in `leaderboard.json` next to the executable. After a game over you enter your name (max 16 characters) before the score is saved
- **Bat Encyclopedia** — 3-tab scrollable card gallery (Nemici · Upgrade · Biomi):
  - *Nemici*: all eight bat variants plus the six golems with sprite preview, stats, and spawn level
  - *Upgrade*: all Miner upgrades including Mythic entries (highlighted with a special card style)
  - *Biomi*: six visual themes with descriptions
- **Audio Settings** — independent Music / SFX volume sliders with mute toggle, available in-game and on the title screen
- **Game Over overlay** with final score (`R` / `Enter` to restart, `Esc` to title)
- **Level flash** on completion (1.8 s, with theme name every 3 levels)
- **Title screen** with parallax scrolling clouds and a main menu: Gioca · Classifica · Istruzioni · Audio · Comandi

### Window & Rendering

- Resizable window with **letterbox scaling** (fixed map-world aspect ratio)
- `F11` toggles fullscreen
- `SamplerState.PointClamp` throughout (pixel-perfect rendering)
- **Dynamic 2D lighting** (no shaders): half-resolution light map multiplied over the world. Each biome has its own ambient light (dark caves, warm lava, bright forest); the miner carries a lantern, fuses, explosions, lava pools, keys, the door and bats emit coloured light
- **Depth**: raised blocks cast soft shadows on the floor, with a highlight on top and a darker front face; soft elliptical shadows under the miner, bats, bombs and items
- **Bomberman-style flame beams**: bright, emissive cross-shaped flames that connect the tiles of each explosion
- **Particles**: block debris and dust, explosion smoke and sparks, fuse sparks, footstep dust, pickup bursts
- Bombs "breathe" (squash & stretch) and blink faster as the fuse runs out; remote bombs pulse red. Bats hover, keys and chests float
- Modern pixel-art UI: rounded panels with drop shadow and gradient, glowing selected buttons, title screen with floating logo and rising embers, fade-in on every level
- VFX overlay: biome vignette, ambient particles, soft radial flash on every explosion, shockwave ring on big bomb / Walid / Nuke explosions
- Screen shake scaled by explosion size (map only — the HUD stays still)

## Controls

Default bindings. All of them can be changed from the **Comandi** panel.

| Key | Action |
|-----|--------|
| `W` / `A` / `S` / `D` or Arrow keys | Move |
| `Space` | Place small bomb |
| `X` | Place big bomb (if available) |
| `C` | Remote detonation (requires the *Detonatore* upgrade: with it bombs never explode on their own) |
| `Esc` | Pause / back to title |
| `Enter` | Confirm / start / restart |
| `R` | Restart (game over screen) |
| `F11` | Toggle fullscreen |

> Movement is non-diagonal; input is buffered (up to 2 queued moves).

### Gamepad

| Button | Action |
|--------|--------|
| D-pad / left stick | Move / navigate menus |
| `A` | Place small bomb / confirm |
| `B` | Place big bomb |
| `Y` | Remote detonation (*Detonatore*) |
| `Start` | Pause / confirm |
| `Back` | Pause / back |

> Mouse hover only changes the selected menu item when the mouse moves, so a resting cursor never blocks keyboard/gamepad navigation.

## Assets

| Path | Description |
|------|-------------|
| `Content/image/Tile/terrain` | Tile spritesheet (atlas-driven) |
| `Content/image/Tile/TilesetAtlas.xml` | Tile atlas metadata |
| `Content/image/character/miner_animation.xml` | Miner animation metadata |
| `Content/image/character/miner` | Miner HUD icon |
| `Content/image/enemies/bat.xml` | Bat animation metadata |
| `Content/image/enemies/golem_*.xml` | Golem animation metadata (normal, booler, brute, laser, rhino, godness) |
| `Content/image/items/doors.xml` | Per-biome door sprites (with / without key) |
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

From the command line (Windows, .NET 9 SDK):

```
dotnet build PoopMan/PoopMan.csproj -c Release   # also runs the content pipeline
dotnet run --project PoopMan/PoopMan.csproj
```

> The MonoGame Content Builder (MGCB) runs automatically as part of the build and compiles the assets before the first run.
> The game targets Windows (DirectX); it does not build or run on Linux/macOS.

### Download

Prebuilt Windows releases are published on the [Releases](https://github.com/FKProf/PoopMan/releases) page: extract the zip and run `PoopMan.exe` (requires the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)). Release notes live in `.github/release-notes/`.

## Testing & CI

There is no unit-test project; the game is verified on Windows by the **Windows auto-play** workflow (`.github/workflows/windows-autoplay.yml`):

1. Release build of the game, identical to the published one
2. Smoke test of the real `PoopMan.exe` driven with real key presses (`tests/windows-smoke.ps1`), with screenshots
3. Auto-play bot (`tests/PoopMan.AutoPlay`) that plays many levels checking for exceptions, bats stuck in walls, and reachable door/key

Screenshots and logs are attached to the workflow run as artifacts.

## Roadmap

- Deterministic map seeds for reproducible levels
- Win condition / ending screen beyond infinite level loop
- Additional bat types and advanced pathfinding

