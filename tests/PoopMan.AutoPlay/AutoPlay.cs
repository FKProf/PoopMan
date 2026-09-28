using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan;
using PoopMan.GameObjects;
using PoopMan.Scenes;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.Input;

/// <summary>
///     Auto-play di PoopMan: avvia il gioco vero (stesso codice, stesse risorse) e lo fa
///     giocare da un bot a velocità accelerata, controllando invarianti (porta/chiave
///     raggiungibili, nessun pipistrello dentro i muri, nessuna eccezione) e salvando
///     screenshot. Usato dal workflow "Windows auto-play".
///     Uso: PoopMan.AutoPlay &lt;secondi&gt; &lt;velocità&gt; &lt;cartellaScreenshot&gt; &lt;salto livello ogni N s&gt; [modalità]
///     Modalità: bot (default) | deep | deepall | chaintest | det | knock | roll | biomes | ui
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var seconds = args.Length > 0 ? double.Parse(args[0], inv) : 120;
        var speed = args.Length > 1 ? int.Parse(args[1], inv) : 8;
        var shotDir = args.Length > 2 ? args[2] : "shots";
        var levelEvery = args.Length > 3 ? double.Parse(args[3], inv) : 20;
        var mode = args.Length > 4 ? args[4] : "bot";
        BotGame.Deep = mode.StartsWith("deep");
        BotGame.AllUpgrades = mode == "deepall";
        BotGame.ChainTest = mode == "chaintest";
        BotGame.Manual = mode == "manual";
        var scenarioFlags = new Dictionary<string, string>
        {
            ["det"] = "DETTEST", ["knock"] = "KNOCKTEST", ["roll"] = "ROLLTEST",
            ["biomes"] = "BIOMES", ["ui"] = "UISHOTS", ["upgshot"] = "UPGSHOT", ["scenario"] = null
        };
        if (scenarioFlags.TryGetValue(mode, out var flag))
        {
            BotGame.Scenario = true;
            if (flag != null) Environment.SetEnvironmentVariable(flag, "1");
        }

        Directory.CreateDirectory(shotDir);
        Console.WriteLine($"AUTOPLAY mode={mode} seconds={seconds} speed={speed} os={Environment.OSVersion}");
        int exitCode;
        using (var game = new BotGame(seconds, speed, shotDir, levelEvery))
        {
            game.Run();
            exitCode = game.ExitCode;
        }

        Environment.Exit(exitCode);
    }
}

class BotGame : Game1
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
    readonly double _limit; readonly int _speed; readonly string _shotDir; readonly double _levelEvery;
    public int ExitCode; public static bool Deep, AllUpgrades, ChainTest, Manual, Scenario; int _scStep; double _scT; bool _snapNext; string _snapName; int _chainTests; object _upgradedScene; int _checkedLevel = -1;
    double _t; double _lastShot = -100; double _lastLevelSkip; int _shotN;
    readonly HashSet<Keys> _held = new();
    readonly HashSet<Keys> _tap = new();
    Keys _dir = Keys.None; double _dirUntil;
    readonly Random _r = new(1234);
    double _pauseUntil = -1; int _pausePhase;
    int _gameOvers, _levelsSeen, _upgradesPicked, _scenes;
    object _lastScene;
    public BotGame(double s, int sp, string d, double le) { _limit = s; _speed = sp; _shotDir = d; _levelEvery = le;
        if (!Manual) KeyboardInfo.Provider = () => new KeyboardState(_held.Concat(_tap).ToArray());
        IsFixedTimeStep = false; Graphics.SynchronizeWithVerticalRetrace = false; }

    static object Get(object o, string name) => o.GetType().GetField(name, F)?.GetValue(o) ?? o.GetType().GetProperty(name, F)?.GetValue(o);
    static void Set(object o, string name, object v) => o.GetType().GetField(name, F).SetValue(o, v);
    static object Call(object o, string name, params object[] a) => o.GetType().GetMethod(name, F).Invoke(o, a);
    static object Scene() => typeof(Core).GetField("p_activeScene", F).GetValue(null);

    protected override void Update(GameTime gameTime)
    {
        for (int i = 0; i < _speed; i++)
        {
            var dt = 1.0 / 60.0;
            _t += dt;
            try { if (Scenario) RunScenario(); else if (!Manual) Bot(); }
            catch (Exception e) { Console.WriteLine("BOT ERROR " + e); }
            var gt = new GameTime(TimeSpan.FromSeconds(_t), TimeSpan.FromSeconds(dt));
            try { base.Update(gt); }
            catch (Exception e)
            {
                Console.WriteLine($"*** GAME EXCEPTION at t={_t:F1} scene={Scene()?.GetType().Name}: {e}");
                DumpState();
                ExitCode = 2; Exit(); return;
            }
            _tap.Clear();
        }
        if (_t >= _limit)
        {
            Console.WriteLine($"DONE t={_t:F0}s gameOvers={_gameOvers} levels={_levelsSeen} upgrades={_upgradesPicked} sceneChanges={_scenes} violations={_violations}");
            if (_violations > 0 && ExitCode == 0) ExitCode = 1;
            Exit();
        }
    }

    void DumpState()
    {
        var s = Scene();
        if (s is GameScene gs)
            Console.WriteLine($"level={Get(gs, "_currentLevel")} bats={((IList)Get(gs, "_bats")).Count}");
    }

    protected override void Draw(GameTime gameTime)
    {
        try { base.Draw(gameTime); }
        catch (Exception e)
        {
            Console.WriteLine($"*** DRAW EXCEPTION at t={_t:F1} scene={Scene()?.GetType().Name}: {e}");
            ExitCode = 3; Exit(); return;
        }
        if (File.Exists(Path.Combine(_shotDir, "snap")))
        {
            var nm = File.ReadAllText(Path.Combine(_shotDir, "snap")).Trim();
            File.Delete(Path.Combine(_shotDir, "snap"));
            Shot(nm);
        }
        if (_snapNext) { _snapNext = false; Shot(_snapName); }
        if (!Manual && !Scenario && _t - _lastShot >= 15)
        {
            _lastShot = _t;
            Shot($"auto_{_shotN++:D3}_{Scene()?.GetType().Name}");
        }
    }

    public void Shot(string name)
    {
        var gd = GraphicsDevice;
        int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        gd.GetBackBufferData(data);
        using var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        using var fs = File.Create(Path.Combine(_shotDir, name + ".png"));
        tex.SaveAsPng(fs, w, h);
    }

    int _violations;
    void CheckInvariants(GameScene gs, int level)
    {
        
        var map = (PoopManLibrary.World.TileMap)Get(gs, "_map");
        foreach (Bat b in (IList)Get(gs, "_bats"))
        {
            if (b.IsDead) continue;
            var vt = b.VisualTilePosition;
            if (!map.IsWalkable(b.TilePosition) || !map.IsWalkable(vt))
            {
                _violations++;
                if (_violations <= 30) Console.WriteLine($"[INV] t={_t:F1} lv={level} bat tile={b.TilePosition} visual={vt} pos={b.Position} walkT={map.IsWalkable(b.TilePosition)} walkV={map.IsWalkable(vt)} tileT={map.GetTile(b.TilePosition)} tileV={map.GetTile(vt)} knock={Get(b, "_knockbackTimer")} moving={Get(b, "isMoving")}");
                if (_violations <= 30 && _violations % 10 == 1) Shot($"inv_{_violations:D3}");
            }
        }
        var miner = (Miner)Get(gs, "_miner");
        if (!miner.IsDead && !map.IsWalkable(miner.TilePosition))
        { _violations++; Console.WriteLine($"[INV] miner on unwalkable {miner.TilePosition}"); }
    }

    void Tap(Keys k) => _tap.Add(k);

    void RunScenario()
    {
        var s = Scene();
        if (Environment.GetEnvironmentVariable("UISHOTS") == "1") { UiShots(s); return; }
        if (s is TitleScene) { if (_scStep == 0) { Tap(Keys.Enter); _scStep = 1; } return; }
        if (s is not GameScene gs) return;
        var miner = (Miner)Get(gs, "_miner");
        var map = (PoopManLibrary.World.TileMap)Get(gs, "_map");
        var bombs = (IList)Get(miner, "_bombs");
        if (Environment.GetEnvironmentVariable("KNOCKTEST") == "1")
        {
            Set(gs, "_showUpgradeMenu", false);
            typeof(Miner).GetProperty("Lives").SetValue(miner, 3);
            if (!miner.IsInvincible) Set(miner, "<IsInvincible>k__BackingField", true);
            var lvlK = (int)Get(gs, "_currentLevel");
            CheckInvariants(gs, lvlK);
            foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList())
            {
                if (b.IsDead || _r.NextDouble() > 0.03) continue;
                var a = _r.NextDouble() * Math.PI * 2;
                b.ApplyKnockback(new Vector2((float)Math.Cos(a), (float)Math.Sin(a)), 160f + (float)_r.NextDouble() * 100f);
                _rollPlaced++;
            }
            if (_t - _scT2 > 15) { _scT2 = _t; Call(gs, "GoToNextLevel"); }
            if ((int)(_t * 60) % 1800 == 0) Console.WriteLine($"[KNOCK] t={_t:F0} knocks={_rollPlaced} violations={_violations}");
            return;
        }
        if (Environment.GetEnvironmentVariable("ROLLTEST") == "1")
        {
            Set(gs, "_showUpgradeMenu", false);
            if (_scStep == 1)
            {
                miner.ApplyUpgrade(UpgradeType.RemoteDetonator);
                for (int k = 0; k < 6; k++) miner.ApplyUpgrade(UpgradeType.ExtraBomb);
                _scStep = 2; _scT = _t;
            }
            typeof(Miner).GetProperty("Lives").SetValue(miner, 3);
            var lvl0 = (int)Get(gs, "_currentLevel");
            CheckInvariants(gs, lvl0);
            foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList())
            {
                if (b.IsDead || !(bool)Get(b, "isMoving")) continue;
                var tgt = (Vector2)Get(b, "targetPosition");
                if (Vector2.Distance(tgt, b.Position) < 10f) continue;
                if (_r.NextDouble() < 0.08) { if ((bool)Call(miner, "TryPlaceBomb", b.TilePosition, false)) _rollPlaced++; }
            }
            if (_t - _scT > 1.5) { Tap(Keys.C); _scT = _t; }
            if (_t - _scT2 > 15) { _scT2 = _t; Call(gs, "GoToNextLevel"); }
            if ((int)(_t * 60) % 1800 == 0) Console.WriteLine($"[ROLL] t={_t:F0} placed={_rollPlaced} violations={_violations}");
            return;
        }
        if (Environment.GetEnvironmentVariable("BIOMES") == "1")
        {
            Set(gs, "_showUpgradeMenu", false);
            var lvl = (int)Get(gs, "_currentLevel");
            var target = (_scStep - 1) * 4;
            if (_scStep >= 1 && _scStep <= 6)
            {
                if (lvl < target) { Call(gs, "GoToNextLevel"); _scT = _t; return; }
                if (_t < _scT + 1.0) return;
                if (_sub == 0)
                {
                    foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList()) { if (_r.NextDouble() < 0.5) b.Kill(); }
                    miner.ApplyUpgrade(UpgradeType.ExtraBomb);
                    var p0 = miner.TilePosition;
                    Call(miner, "TryPlaceBomb", p0, false);
                    foreach (var d in new[] { new Point(2, 0), new Point(0, 2), new Point(-2, 0), new Point(0, -2), new Point(1,0), new Point(0,1) })
                    { var q = new Point(p0.X + d.X, p0.Y + d.Y); if (map.IsWalkable(q)) { Call(miner, "TryPlaceBomb", q, true); break; } }
                    _sub = 1; _scT2 = _t;
                }
                else if (_sub == 1 && _t > _scT2 + 1.2) { _snapNext = true; _snapName = $"biome{_scStep}_bombs"; _sub = 2; }
                else if (_sub == 2 && _t > _scT2 + 2.1) { _snapNext = true; _snapName = $"biome{_scStep}_boom"; _sub = 3; }
                else if (_sub == 3 && _t > _scT2 + 2.6) { _snapNext = true; _snapName = $"biome{_scStep}_after"; _sub = 0; _scStep++; _scT = _t; miner.Respawn(miner.TilePosition); }
            }
            else if (_scStep == 7) { Exit(); _scStep = 8; }
            return;
        }
        if (Environment.GetEnvironmentVariable("DETTEST") == "1")
        {
            bool Exploding(object b) => (bool)b.GetType().GetProperty("IsExploding").GetValue(b);
            if (_scStep == 1 && _t > 1.5)
            {
                foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList()) b.Kill();
                miner.ApplyUpgrade(UpgradeType.RemoteDetonator);
                miner.ApplyUpgrade(UpgradeType.ExtraBomb);
                var p0 = miner.TilePosition;
                Call(miner, "TryPlaceBomb", p0, false);
                _scT = _t; _scStep = 2;
                Console.WriteLine($"[DET] bomb placed at {p0}, count={bombs.Count}");
            }
            else if (_scStep == 2 && _t > _scT + 6)
            {
                var anyExp = bombs.Cast<object>().Any(Exploding);
                Console.WriteLine($"[DET] after 6s: bombs={bombs.Count} exploded={anyExp} -> {(bombs.Count == 1 && !anyExp ? "PASS" : "FAIL")}");
                _snapNext = true; _snapName = "det_waiting";
                Tap(Keys.C); _scStep = 3; _scT = _t;
            }
            else if (_scStep == 3)
            {
                var anyExp = bombs.Cast<object>().Any(Exploding);
                if (anyExp) { Console.WriteLine($"[DET] detonated after C at +{_t - _scT:F2}s PASS"); _snapNext = true; _snapName = "det_boom"; _scStep = 4; _scT = _t; }
                else if (_t > _scT + 1) { Console.WriteLine("[DET] C did not detonate FAIL"); _scStep = 4; }
            }
            else if (_scStep == 4 && _t > _scT + 1.5)
            {
                Console.WriteLine($"[DET] end bombs left={bombs.Count} minerDead={miner.IsDead} lives={miner.Lives}"); Exit(); _scStep = 5;
            }
            return;
        }
        if (Environment.GetEnvironmentVariable("UPGSHOT") == "1")
        {
            if (_scStep == 1 && _t > 1.5)
            {
                var m = (Miner)Get(gs, "_miner");
                var lv = Enum.GetValues<UpgradeType>().ToDictionary(t => t, t => 0);
                var opts = UpgradeRegistry.PickRandom(3, lv);
                opts[0] = UpgradeRegistry.PickRandom(30, lv).First(o => o.Type == UpgradeType.ExplosionDamage);
                opts[1] = UpgradeRegistry.PickRandom(30, lv).First(o => o.Type == UpgradeType.RemoteDetonator);
                m.ApplyUpgrade(UpgradeType.ExplosionDamage); m.ApplyUpgrade(UpgradeType.ExplosionDamage);
                Set(gs, "_upgradeOptions", opts); Set(gs, "_showUpgradeMenu", true); Set(gs, "_upgradeSelected", 1);
                _scStep = 9; _scT = _t;
            }
            else if (_scStep == 9 && _t > _scT + 0.5) { Shot("upgrade_menu"); Exit(); _scStep = 10; }
            return;
        }
        if (_scStep == 1 && _t > 1.5)
        {
            foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList()) b.Kill();
            var p0 = miner.TilePosition;
            Point? p1 = null;
            foreach (var d in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            { var q = new Point(p0.X + d.X, p0.Y + d.Y); if (map.IsWalkable(q)) { p1 = q; break; } }
            Call(miner, "TryPlaceBomb", p0, false);
            _scT = _t;
            Console.WriteLine($"[SC] bomb A at {p0} t={_t:F2}");
            _scStep = 2; _p1 = p1.Value;
        }
        else if (_scStep == 2 && _t > _scT + 1.0)
        {
            Call(miner, "TryPlaceBomb", _p1, false);
            Console.WriteLine($"[SC] bomb B at {_p1} t={_t:F2} (own fuse would end ~{_t + 2:F2})");
            miner.Respawn(miner.TilePosition);
            _scStep = 3;
        }
        else if (_scStep == 3)
        {
            int i = 0;
            foreach (var b in bombs)
            {
                var exp = (bool)b.GetType().GetProperty("IsExploding").GetValue(b);
                var key = b.GetHashCode();
                if (exp && !_exploded.Contains(key)) { _exploded.Add(key); Console.WriteLine($"[SC] bomb #{i} exploded at t={_t:F2}"); _snapNext = true; _snapName = "chain_" + _exploded.Count; }
                i++;
            }
            if (_t > _scT + 1.95 && _t < _scT + 2.3 && !_snapNext && (int)(_t*60)%4==0) { _snapNext = true; _snapName = $"boom_{_t:F2}"; }
            if (_t > _scT + 4) { Console.WriteLine("[SC] end"); _scStep = 4; Exit(); }
        }
    }
    int _ui; double _uiT;
    void UiShots(object s)
    {
        void Snap(string n) { _snapNext = true; _snapName = n; }
        bool After(double d) => _t > _uiT + d;
        void Next() { _ui++; _uiT = _t; }
        switch (_ui)
        {
            case 0: if (After(1.5)) { Snap("ui_title"); Next(); } break;
            case 1: if (After(0.3)) { Tap(Keys.Down); Next(); } break;
            case 2: if (After(0.3)) { Tap(Keys.Down); Next(); _ui = 102; } break;
            case 102: if (After(0.3)) { Tap(Keys.Enter); _ui = 3; _uiT = _t; } break;
            case 3: if (After(0.6)) { Snap("ui_istruzioni"); Next(); } break;
            case 4: if (After(0.3)) { Tap(Keys.Escape); Next(); } break;
            case 5: if (After(0.3)) { Tap(Keys.Up); Next(); _ui = 105; } break;
            case 105: if (After(0.3)) { Tap(Keys.Up); _ui = 6; _uiT = _t; } break;
            case 6: if (After(0.3)) { Tap(Keys.Enter); Next(); } break;
            case 7: if (After(1.2)) { Snap("ui_level_start"); Next(); } break;
            case 8: if (After(1.5)) { Tap(Keys.Escape); Next(); } break;
            case 9: if (After(0.5)) { Snap("ui_pause"); Next(); } break;
            case 10: if (After(0.2)) { Tap(Keys.Down); _ui = 110; _uiT = _t; } break;
            case 110: if (After(0.3)) { Tap(Keys.Enter); _ui = 111; _uiT = _t; } break;
            case 111: if (After(0.8)) { Snap("ui_encyclopedia"); _ui = 112; _uiT = _t; } break;
            case 112: if (After(0.3)) { Tap(Keys.Escape); _ui = 113; _uiT = _t; } break;
            case 113: if (After(0.3)) { Tap(Keys.Escape); _ui = 11; _uiT = _t; } break;
            case 11:
                if (After(0.5) && s is GameScene gs)
                {
                    var lv = Enum.GetValues<UpgradeType>().ToDictionary(t => t, t => 0);
                    Set(gs, "_upgradeOptions", UpgradeRegistry.PickRandom(3, lv));
                    Set(gs, "_showUpgradeMenu", true); Set(gs, "_upgradeSelected", 1); Set(gs, "_upgradeMenuTimer", 0f);
                    Next();
                }
                break;
            case 12: if (After(0.8)) { Snap("ui_upgrade"); Next(); } break;
            case 13: if (After(0.2)) { Tap(Keys.Enter); Next(); } break;
            case 14: if (After(0.4)) { Snap("ui_upgrade_picked"); Next(); } break;
            case 15:
                if (After(0.5) && s is GameScene gs2)
                {
                    var m = (Miner)Get(gs2, "_miner");
                    typeof(Miner).GetProperty("Lives").SetValue(m, 1);
                    m.Kill(); Next();
                }
                break;
            case 16: if (After(3.5)) { Snap("ui_gameover"); Next(); } break;
            case 17: if (After(0.2)) { Tap(Keys.Enter); Next(); } break;
            case 18: if (After(0.8)) { Snap("ui_name"); Next(); } break;
            case 19: if (After(0.2) && s is NameEntryScreen ne) { Set(ne, "_name", "TESTER"); Call(ne, "Confirm"); Next(); } break;
            case 20: if (After(1.0)) { Snap("ui_leaderboard"); Next(); } break;
            case 21: if (After(0.5)) { Exit(); Next(); } break;
        }
    }
    int _rollPlaced; Point _p1; int _sub; double _scT2; readonly HashSet<int> _exploded = new();

    void Bot()
    {
        var s = Scene();
        if (!ReferenceEquals(s, _lastScene)) { _scenes++; _lastScene = s; _held.Clear(); _dir = Keys.None; }
        switch (s)
        {
            case TitleScene:
                if (_r.NextDouble() < 0.05) Tap(Keys.Enter);
                break;
            case NameEntryScreen ne:
                if (_r.NextDouble() < 0.05) { Set(ne, "_name", "BOT" + _gameOvers); Call(ne, "Confirm"); }
                break;
            case LeaderboardScreen lb:
                if (_r.NextDouble() < 0.05) Call(lb, "HandleButton", Enum.Parse(lb.GetType().GetNestedType("BtnId", F), "Restart"));
                break;
            case GameScene gs:
                PlayGame(gs);
                break;
        }
    }

    void PlayGame(GameScene gs)
    {
        if ((bool)Get(gs, "_showGameOver"))
        {
            _held.Clear();
            if (_r.NextDouble() < 0.03) { _gameOvers++; Tap(Keys.Enter); }
            return;
        }
        if ((bool)Get(gs, "_showUpgradeMenu"))
        {
            _held.Clear();
            var roll = _r.NextDouble();
            if (roll < 0.05) Tap(Keys.Right);
            else if (roll < 0.08) { Tap(Keys.Enter); _upgradesPicked++; }
            return;
        }
        var level = (int)Get(gs, "_currentLevel");
        if ((int)(_t * 60) % 6000 == 0) Console.WriteLine($"[DBG] t={_t:F0} lv={level} paused={Get(gs, "_isPaused")} phase={_pausePhase} pmScreen={Get(Get(gs, "_pauseMenu"), "_screen")} upg={Get(gs, "_showUpgradeMenu")} dead={((Miner)Get(gs, "_miner")).IsDead}");
        CheckInvariants(gs, level);
        if (ChainTest && _t > 3 && _chainTests < 50)
        {
            foreach (Bat b in ((IList)Get(gs, "_bats")).Cast<Bat>().ToList())
            {
                if (b.IsDead) continue;
                b.ApplyVariant(Bat.BatVariant.Splitter);
                Set(b, "<IsInvincible>k__BackingField", false);
                _chainTests++;
                var before = ((IList)Get(gs, "_bats")).Count;
                Call(gs, "TriggerChainAt", b.VisualTilePosition);
                Console.WriteLine($"[CHAIN] test {_chainTests}: bats {before} -> {((IList)Get(gs, "_bats")).Count}, splitterDead={b.IsDead}");
                break;
            }
            if (_chainTests >= 50) { Console.WriteLine("[CHAIN] completed"); }
        }
        if (AllUpgrades && !ReferenceEquals(_upgradedScene, gs))
        {
            _upgradedScene = gs;
            var m0 = (Miner)Get(gs, "_miner");
            foreach (var ut in Enum.GetValues<UpgradeType>())
                if (!UpgradeRegistry.IsMythic(ut))
                    for (int k = 0; k < 4; k++) m0.ApplyUpgrade(ut);
            Console.WriteLine("[BOT] applied all non-mythic upgrades");
        }
        if (level != _checkedLevel)
        {
            _checkedLevel = level;
            var map0 = (PoopManLibrary.World.TileMap)Get(gs, "_map");
            var miner0 = (Miner)Get(gs, "_miner");
            var reach = map0.GetReachableTiles(miner0.TilePosition);
            var door = (Point)Get(gs, "_doorPosition");
            var batsN = ((IList)Get(gs, "_bats")).Count;
            if (!(bool)Get(gs, "_doorSpawned") || !reach.Contains(door))
                { _violations++; Console.WriteLine($"[INV] lv={level} door unreachable or missing! spawned={Get(gs, "_doorSpawned")} door={door}"); }
            if (batsN > 40) { _violations++; Console.WriteLine($"[INV] lv={level} too many bats {batsN}"); }
            var items = (IDictionary)Get(gs, "_droppedItems");
            foreach (DictionaryEntry kv in items)
                if ((string)Get(kv.Value, "Type") == "key" && !reach.Contains((Point)kv.Key))
                { _violations++; Console.WriteLine($"[INV] lv={level} key unreachable at {kv.Key}"); }
        }
        if (Deep)
        {
            var m = (Miner)Get(gs, "_miner");
            if (m.Lives < 2) typeof(Miner).GetProperty("Lives").SetValue(m, 3);
        }
        if (level + 1 > _levelsSeen) _levelsSeen = level + 1;

        // pause menu excursions
        if ((bool)Get(gs, "_isPaused"))
        {
            _held.Clear();
            if (_t < _pauseUntil) {
                // inside encyclopedia: browse
                if (_r.NextDouble() < 0.1) Tap(new[] { Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Tab }[_r.Next(5)]);
                return;
            }
            if (_pausePhase == 1) { Tap(Keys.Down); _pausePhase = 2; }
            else if (_pausePhase == 2) { Tap(Keys.Enter); _pausePhase = 3; _pauseUntil = _t + 4; }
            else if (_pausePhase == 3) { Tap(Keys.Escape); _pausePhase = 4; }
            else if (_pausePhase == 4) { Tap(Keys.Up); _pausePhase = 5; }
            else if (_pausePhase == 5) { Tap(Keys.Enter); _pausePhase = 0; }
            else if (_r.NextDouble() < 0.05) Tap(Keys.Escape); // uscita di sicurezza (Esc: indietro / riprendi)
            return;
        }
        if (_r.NextDouble() < 0.0015) { _held.Clear(); _dirUntil = 0; Tap(Keys.Escape); _pausePhase = 1; return; }

        // cheat: skip level periodically to exercise higher content
        if (_t - _lastLevelSkip > _levelEvery)
        {
            _lastLevelSkip = _t;
            Call(gs, "GoToNextLevel");
            var miner = (Miner)Get(gs, "_miner");
            var types = Enum.GetValues<UpgradeType>();
            miner.ApplyUpgrade(types[_r.Next(types.Length)]);
            return;
        }

        // movement
        if (_t >= _dirUntil)
        {
            _held.Clear();
            var opts = new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.W, Keys.A, Keys.S, Keys.D, Keys.None };
            _dir = opts[_r.Next(opts.Length)];
            if (_dir != Keys.None) _held.Add(_dir);
            _dirUntil = _t + 0.2 + _r.NextDouble() * 1.2;
        }
        if (_r.NextDouble() < 0.02) Tap(Keys.Space);
        if (_r.NextDouble() < 0.004) Tap(Keys.X);
        if (_r.NextDouble() < 0.01) Tap(Keys.C);
    }
}
