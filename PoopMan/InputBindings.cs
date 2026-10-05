using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PoopMan;

/// <summary>Azioni di gioco rimappabili dal giocatore.</summary>
public enum GameAction
{
    Up,
    Down,
    Left,
    Right,
    MiniBomb,
    BigBomb,
    Detonate,
    Pause
}

/// <summary>
///     Assegnazione tasti personalizzabile: per ogni azione due tasti della tastiera
///     (principale + alternativo) e un pulsante del gamepad.
///     Salvata in %APPDATA%\PoopMan\controls.cfg (una riga "Azione=Tasto1,Tasto2,Pulsante").
/// </summary>
public static class InputBindings
{
    /// <summary>Numero di colonne tastiera per azione.</summary>
    public const int KeySlots = 2;

    private static readonly string _path =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoopMan", "controls.cfg");

    public static readonly GameAction[] Actions = Enum.GetValues<GameAction>();

    /// <summary>Pulsanti del gamepad assegnabili (in ordine di rilevamento).</summary>
    public static readonly Buttons[] AssignableButtons =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.LeftStick, Buttons.RightStick, Buttons.Start, Buttons.Back,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight
    };

    private static readonly Dictionary<GameAction, Keys[]> _keys = new();
    private static readonly Dictionary<GameAction, Buttons> _buttons = new();

    static InputBindings()
    {
        ResetToDefaults();
        Load();
    }

    public static Keys GetKey(GameAction action, int slot)
    {
        return _keys[action][slot];
    }

    public static Buttons GetButton(GameAction action)
    {
        return _buttons[action];
    }

    public static bool IsKeyBound(GameAction action, Keys key)
    {
        return key != Keys.None && Array.IndexOf(_keys[action], key) >= 0;
    }

    /// <summary>
    ///     Assegna un tasto. Se era già usato da un'altra azione i due tasti
    ///     vengono scambiati, così nessuna azione resta senza comando per errore.
    /// </summary>
    public static void SetKey(GameAction action, int slot, Keys key)
    {
        var previous = _keys[action][slot];
        if (key != Keys.None)
            foreach (var other in Actions)
            {
                var keys = _keys[other];
                for (var s = 0; s < KeySlots; s++)
                    if (keys[s] == key && (other != action || s != slot))
                        keys[s] = previous;
            }

        _keys[action][slot] = key;
        Save();
    }

    /// <summary>Assegna un pulsante del gamepad, scambiandolo con l'azione che lo usava.</summary>
    public static void SetButton(GameAction action, Buttons button)
    {
        var previous = _buttons[action];
        foreach (var other in Actions)
            if (other != action && _buttons[other] == button)
                _buttons[other] = previous;

        _buttons[action] = button;
        Save();
    }

    public static void ResetToDefaults()
    {
        _keys[GameAction.Up] = new[] { Keys.Up, Keys.W };
        _keys[GameAction.Down] = new[] { Keys.Down, Keys.S };
        _keys[GameAction.Left] = new[] { Keys.Left, Keys.A };
        _keys[GameAction.Right] = new[] { Keys.Right, Keys.D };
        _keys[GameAction.MiniBomb] = new[] { Keys.Space, Keys.None };
        _keys[GameAction.BigBomb] = new[] { Keys.X, Keys.None };
        _keys[GameAction.Detonate] = new[] { Keys.C, Keys.None };
        _keys[GameAction.Pause] = new[] { Keys.Escape, Keys.None };

        _buttons[GameAction.Up] = Buttons.DPadUp;
        _buttons[GameAction.Down] = Buttons.DPadDown;
        _buttons[GameAction.Left] = Buttons.DPadLeft;
        _buttons[GameAction.Right] = Buttons.DPadRight;
        _buttons[GameAction.MiniBomb] = Buttons.A;
        _buttons[GameAction.BigBomb] = Buttons.B;
        _buttons[GameAction.Detonate] = Buttons.Y;
        _buttons[GameAction.Pause] = Buttons.Start;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllLines(_path, Actions.Select(a =>
                $"{a}={string.Join(",", _keys[a])},{_buttons[a]}"));
        }
        catch
        {
            /* non critico */
        }
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            foreach (var line in File.ReadAllLines(_path))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2 || !Enum.TryParse<GameAction>(parts[0], out var action)) continue;
                var values = parts[1].Split(',');
                if (values.Length != KeySlots + 1) continue;

                var keys = new Keys[KeySlots];
                var ok = true;
                for (var s = 0; s < KeySlots; s++)
                    ok &= Enum.TryParse(values[s], out keys[s]);
                if (!ok || !Enum.TryParse<Buttons>(values[KeySlots], out var button)) continue;

                _keys[action] = keys;
                _buttons[action] = button;
            }
        }
        catch
        {
            /* file corrotto: restano i predefiniti */
        }
    }

    // ── Nomi leggibili per l'interfaccia ──────────────────────────────────
    public static string ActionName(GameAction action)
    {
        return action switch
        {
            GameAction.Up => "SU",
            GameAction.Down => "GIU",
            GameAction.Left => "SINISTRA",
            GameAction.Right => "DESTRA",
            GameAction.MiniBomb => "BOMBA PICCOLA",
            GameAction.BigBomb => "BOMBA GRANDE",
            GameAction.Detonate => "DETONATORE",
            GameAction.Pause => "PAUSA",
            _ => action.ToString().ToUpperInvariant()
        };
    }

    public static string KeyName(Keys key)
    {
        return key switch
        {
            Keys.None => "-",
            Keys.Up => "FRECCIA SU",
            Keys.Down => "FRECCIA GIU",
            Keys.Left => "FRECCIA SX",
            Keys.Right => "FRECCIA DX",
            Keys.Space => "SPAZIO",
            Keys.Escape => "ESC",
            Keys.Enter => "ENTER",
            Keys.Back => "BACKSPACE",
            Keys.LeftShift => "SHIFT SX",
            Keys.RightShift => "SHIFT DX",
            Keys.LeftControl => "CTRL SX",
            Keys.RightControl => "CTRL DX",
            Keys.LeftAlt => "ALT SX",
            Keys.RightAlt => "ALT DX",
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => "NUM " + (key - Keys.NumPad0),
            _ => key.ToString().Replace("Oem", "").ToUpperInvariant()
        };
    }

    public static string ButtonName(Buttons button)
    {
        return button switch
        {
            Buttons.LeftShoulder => "LB",
            Buttons.RightShoulder => "RB",
            Buttons.LeftTrigger => "LT",
            Buttons.RightTrigger => "RT",
            Buttons.LeftStick => "L3",
            Buttons.RightStick => "R3",
            Buttons.DPadUp => "D-PAD SU",
            Buttons.DPadDown => "D-PAD GIU",
            Buttons.DPadLeft => "D-PAD SX",
            Buttons.DPadRight => "D-PAD DX",
            _ => button.ToString().ToUpperInvariant()
        };
    }

    /// <summary>Tasti tastiera di un'azione in forma leggibile (es. "FRECCIA SU / W").</summary>
    public static string KeysLabel(GameAction action)
    {
        var names = _keys[action].Where(k => k != Keys.None).Select(KeyName).ToArray();
        return names.Length == 0 ? "-" : string.Join(" / ", names);
    }
}
