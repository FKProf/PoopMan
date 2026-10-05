using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan;

/// <summary>
///     Astrazione dell'input di gioco: tastiera + gamepad (giocatore 1).
///     I comandi di gioco seguono le assegnazioni personalizzabili di <see cref="InputBindings" />;
///     i comandi dei menu (frecce, ENTER, ESC, D-pad, A, B) restano fissi.
/// </summary>
public class GameController
{
    private static KeyboardInfo p_keyboard => Core.Input.Keyboard;
    private static GamePadInfo p_gamePad => Core.Input.GamePad;

    // === Comandi rimappabili (vedi InputBindings) ===
    /// <summary>True mentre l'azione è tenuta premuta (tastiera o gamepad).</summary>
    public static bool IsHeld(GameAction action)
    {
        for (var s = 0; s < InputBindings.KeySlots; s++)
        {
            var key = InputBindings.GetKey(action, s);
            if (key != Keys.None && p_keyboard.IsKeyDown(key)) return true;
        }

        return p_gamePad.IsButtonDown(InputBindings.GetButton(action));
    }

    /// <summary>True solo nel frame in cui l'azione viene premuta.</summary>
    public static bool WasPressed(GameAction action)
    {
        for (var s = 0; s < InputBindings.KeySlots; s++)
        {
            var key = InputBindings.GetKey(action, s);
            if (key != Keys.None && p_keyboard.WasKeyJustPressed(key)) return true;
        }

        return p_gamePad.WasButtonJustPressed(InputBindings.GetButton(action));
    }

    // === Pressione singola (tap) — per menu ===
    // Le frecce e il D-pad / levetta funzionano sempre nei menu, anche se rimappati,
    // così il giocatore non può restare bloccato con una configurazione sbagliata.
    public static bool MoveUp()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Up) || p_gamePad.WasUpJustPressed || WasPressed(GameAction.Up);
    }

    public static bool MoveDown()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Down) || p_gamePad.WasDownJustPressed || WasPressed(GameAction.Down);
    }

    public static bool MoveLeft()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Left) || p_gamePad.WasLeftJustPressed || WasPressed(GameAction.Left);
    }

    public static bool MoveRight()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Right) || p_gamePad.WasRightJustPressed ||
               WasPressed(GameAction.Right);
    }

    /// <summary>Sinistra tenuta nei menu (regolazione volume): frecce / D-pad sempre attivi.</summary>
    public static bool MenuHoldLeft()
    {
        return p_keyboard.IsKeyDown(Keys.Left) || p_gamePad.IsLeftDown || IsHeld(GameAction.Left);
    }

    public static bool MenuHoldRight()
    {
        return p_keyboard.IsKeyDown(Keys.Right) || p_gamePad.IsRightDown || IsHeld(GameAction.Right);
    }

    // === Navigazione menu (alias leggibili) ===
    public static bool MenuUp() => MoveUp();
    public static bool MenuDown() => MoveDown();
    public static bool MenuLeft() => MoveLeft();
    public static bool MenuRight() => MoveRight();

    /// <summary>
    ///     Indietro / chiudi nei menu: ESC oppure B / Back sul gamepad.
    ///     Da usare solo nelle schermate di menu (in gioco B è la bomba grande).
    /// </summary>
    public static bool MenuBack()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Escape) ||
               p_gamePad.WasButtonJustPressed(Buttons.B) ||
               p_gamePad.WasButtonJustPressed(Buttons.Back);
    }

    /// <summary>Sezione precedente (enciclopedia): Q oppure LB.</summary>
    public static bool PrevTab()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Q) || p_gamePad.WasButtonJustPressed(Buttons.LeftShoulder);
    }

    /// <summary>Sezione successiva (enciclopedia): TAB / E oppure RB.</summary>
    public static bool NextTab()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Tab) || p_keyboard.WasKeyJustPressed(Keys.E) ||
               p_gamePad.WasButtonJustPressed(Buttons.RightShoulder);
    }

    /// <summary>Attiva/disattiva il mute nel pannello audio: M oppure X sul gamepad.</summary>
    public static bool ToggleMute()
    {
        return p_keyboard.WasKeyJustPressed(Keys.M) || p_gamePad.WasButtonJustPressed(Buttons.X);
    }

    /// <summary>Conferma nei menu: ENTER oppure A / Start sul gamepad.</summary>
    public static bool Confirm()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Enter) ||
               p_gamePad.WasButtonJustPressed(Buttons.A) ||
               p_gamePad.WasButtonJustPressed(Buttons.Start);
    }

    // === Tasto tenuto premuto (hold) — movimento in gioco ===
    // La levetta sinistra muove sempre, in aggiunta ai comandi assegnati.
    public static bool HoldUp()
    {
        return IsHeld(GameAction.Up) || p_gamePad.IsStickUp;
    }

    public static bool HoldDown()
    {
        return IsHeld(GameAction.Down) || p_gamePad.IsStickDown;
    }

    public static bool HoldLeft()
    {
        return IsHeld(GameAction.Left) || p_gamePad.IsStickLeft;
    }

    public static bool HoldRight()
    {
        return IsHeld(GameAction.Right) || p_gamePad.IsStickRight;
    }

    public static bool MiniBomb()
    {
        return WasPressed(GameAction.MiniBomb);
    }

    public static bool BigBomb()
    {
        return WasPressed(GameAction.BigBomb);
    }

    /// <summary>Detonatore remoto (upgrade DETONATORE).</summary>
    public static bool Detonate()
    {
        return WasPressed(GameAction.Detonate);
    }

    public static bool Pause()
    {
        return WasPressed(GameAction.Pause);
    }

    public static bool Action()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Enter) || p_gamePad.WasButtonJustPressed(Buttons.A);
    }

    public static bool ToggleFullScreen()
    {
        return p_keyboard.WasKeyJustPressed(Keys.F11);
    }

    public static bool Restart()
    {
        return p_keyboard.WasKeyJustPressed(Keys.R);
    }
}