# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**PoopMan Miner**: 2D tile-based action game (bombs, bat enemies, power-ups, procedurally generated levels) built with MonoGame 3.8 on .NET 9 / C# 13. See `README.md` for the full feature list, controls and roadmap.

## Solution layout

- `PoopMan/` : game executable (`net9.0-windows`, MonoGame `WindowsDX`, WinForms)
  - `Program.cs`, `Game1.cs` : entry point, window setup, fullscreen toggle, letterbox scaling
  - `GameController.cs`, `InputBindings.cs` : input abstraction and key bindings
  - `Scenes/` : `TitleScene`, `GameScene` (gameplay loop), plus rendering systems (`LightingSystem`, `ParticleSystem`, `VisualEffectSystem`, `BiomeAtmosphere`, `GolemAttackSystem`, `FxTextures`)
  - `GameObjects/` : `Miner`, `Bat` (+ `Bat.Golem.cs`), `Bomb`, `MinerUpgrade`, `MinerUpgradeDescriptions`
  - `UI/` : HUD, overlays, pause menu, settings panels, encyclopedia, leaderboard, name entry
  - `Content/` : MGCB content pipeline (`Content.mgcb`), shaders in `fx/`, atlas/animation XML next to the images
- `PoopManLibrary/` : shared engine/game logic (`net9.0`): `Core`, `Collision`, `Audio/`, `Graphics/`, `Input/`, `Scenes/Scene.cs`, `World/` (`TileMap`, `TileAtlas`, `TileType`)
- `tests/PoopMan.AutoPlay/` : bot that compiles the game sources (without `Program.cs` and the real `KeyboardInfo`) with the `AUTOPLAY` define and plays automatically
- `tests/windows-smoke.ps1` : smoke test of the real `PoopMan.exe` driven with real key presses

## Build and run

The game targets Windows (DirectX). There is no unit-test project; verification is done by the auto-play bot and the smoke test.

```
dotnet build PoopMan/PoopMan.csproj -c Release        # also runs the MGCB content pipeline
dotnet run --project PoopMan/PoopMan.csproj           # run the game
dotnet build tests/PoopMan.AutoPlay/PoopMan.AutoPlay.csproj -c Release
```

CI (`.github/workflows/windows-autoplay.yml`, `windows-latest`) builds Release, runs the smoke test, then the auto-play bot, and uploads screenshots/logs as artifacts. Releases are built by `.github/workflows/release.yml`.

The content pipeline and WindowsDX do not run on Linux, so in a Linux/cloud session you generally cannot build or run the game: say so instead of claiming it was tested.

## Conventions

- Match the surrounding code: naming, comment density and idioms. Existing comments are a mix of English and Italian; keep the language of the file you are editing.
- `Region` is defined in `PoopManLibrary/Graphics/Sprite.cs`; `AnimatedSprite` must reuse it, not declare a duplicate member.
- `tests/PoopMan.AutoPlay` compiles `PoopMan/**/*.cs` and `PoopManLibrary/**/*.cs` directly. New files are picked up automatically, but anything that depends on real input/window must keep working under the `AUTOPLAY` define.
- New textures/atlases/animations go through `Content/Content.mgcb` and the XML files next to the assets; do not edit generated output.
- Keep changes focused; update `README.md` when gameplay behaviour or controls change.

## Git

- Never commit build output or generated files: `bin/`, `obj/`, `Content/bin`, `Content/obj`, `*.xnb`, `*.mgcontent` are in `.gitignore`.
- Runtime files (e.g. `leaderboard.json`, saves, local ini/config, `.vscode/`) are local only and ignored.
- Develop on the branch you were assigned; open a pull request only when asked. PRs follow `.github/PULL_REQUEST_TEMPLATE.md`.

## Security and secrets

- Never write secrets in this file or in any committed file: API keys, tokens, passwords, connection strings, certificates (`*.pfx`, `*.pem`, `*.key`), `.env` files, local database files (`*.db`, `*.sqlite`).
- If a credential is needed, read it from an environment variable or a gitignored local file, and refer to it by name only, never by value.
- Report vulnerabilities as described in `SECURITY.md`.
