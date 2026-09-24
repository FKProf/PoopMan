using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan;

/// <summary>
///     Astrazione dell'input di gioco: tastiera + gamepad (giocatore 1).
///     Gamepad: D-pad / levetta sinistra = movimento, A = bomba piccola / conferma,
///     B = bomba grande, Y = detonatore, Start = pausa, Back = indietro.
/// </summary>
public class GameController
{
    private static KeyboardInfo p_keyboard => Core.Input.Keyboard;
    private static GamePadInfo p_gamePad => Core.Input.GamePad;

    // === Pressione singola (tap) — per menu, azioni una-tantum ===
    public static bool MoveUp()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Up) || p_keyboard.WasKeyJustPressed(Keys.W) ||
               p_gamePad.WasUpJustPressed;
    }

    public static bool MoveDown()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Down) || p_keyboard.WasKeyJustPressed(Keys.S) ||
               p_gamePad.WasDownJustPressed;
    }

    public static bool MoveLeft()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Left) || p_keyboard.WasKeyJustPressed(Keys.A) ||
               p_gamePad.WasLeftJustPressed;
    }

    public static bool MoveRight()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Right) || p_keyboard.WasKeyJustPressed(Keys.D) ||
               p_gamePad.WasRightJustPressed;
    }

    // === Navigazione menu (alias leggibili) ===
    public static bool MenuUp() => MoveUp();
    public static bool MenuDown() => MoveDown();
    public static bool MenuLeft() => MoveLeft();
    public static bool MenuRight() => MoveRight();

    /// <summary>Conferma nei menu: ENTER oppure A / Start sul gamepad.</summary>
    public static bool Confirm()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Enter) ||
               p_gamePad.WasButtonJustPressed(Buttons.A) ||
               p_gamePad.WasButtonJustPressed(Buttons.Start);
    }

    // === Tasto tenuto premuto (hold) — per movimento continuo ===
    public static bool HoldUp()
    {
        return p_keyboard.IsKeyDown(Keys.Up) || p_keyboard.IsKeyDown(Keys.W) || p_gamePad.IsUpDown;
    }

    public static bool HoldDown()
    {
        return p_keyboard.IsKeyDown(Keys.Down) || p_keyboard.IsKeyDown(Keys.S) || p_gamePad.IsDownDown;
    }

    public static bool HoldLeft()
    {
        return p_keyboard.IsKeyDown(Keys.Left) || p_keyboard.IsKeyDown(Keys.A) || p_gamePad.IsLeftDown;
    }

    public static bool HoldRight()
    {
        return p_keyboard.IsKeyDown(Keys.Right) || p_keyboard.IsKeyDown(Keys.D) || p_gamePad.IsRightDown;
    }

    public static bool MiniBomb()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Space) || p_gamePad.WasButtonJustPressed(Buttons.A);
    }

    public static bool BigBomb()
    {
        return p_keyboard.WasKeyJustPressed(Keys.X) || p_gamePad.WasButtonJustPressed(Buttons.B);
    }

    /// <summary>Detonatore remoto (upgrade DETONATORE): C oppure Y sul gamepad.</summary>
    public static bool Detonate()
    {
        return p_keyboard.WasKeyJustPressed(Keys.C) || p_gamePad.WasButtonJustPressed(Buttons.Y);
    }

    public static bool Pause()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Escape) ||
               p_gamePad.WasButtonJustPressed(Buttons.Start) ||
               p_gamePad.WasButtonJustPressed(Buttons.Back);
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