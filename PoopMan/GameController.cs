using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan;

public class GameController
{
    private static KeyboardInfo p_keyboard => Core.Input.Keyboard;

    // === Pressione singola (tap) — per menu, azioni una-tantum ===
    public static bool MoveUp()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Up) || p_keyboard.WasKeyJustPressed(Keys.W);
    }

    public static bool MoveDown()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Down) || p_keyboard.WasKeyJustPressed(Keys.S);
    }

    public static bool MoveLeft()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Left) || p_keyboard.WasKeyJustPressed(Keys.A);
    }

    public static bool MoveRight()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Right) || p_keyboard.WasKeyJustPressed(Keys.D);
    }

    // === Tasto tenuto premuto (hold) — per movimento continuo ===
    public static bool HoldUp()
    {
        return p_keyboard.IsKeyDown(Keys.Up) || p_keyboard.IsKeyDown(Keys.W);
    }

    public static bool HoldDown()
    {
        return p_keyboard.IsKeyDown(Keys.Down) || p_keyboard.IsKeyDown(Keys.S);
    }

    public static bool HoldLeft()
    {
        return p_keyboard.IsKeyDown(Keys.Left) || p_keyboard.IsKeyDown(Keys.A);
    }

    public static bool HoldRight()
    {
        return p_keyboard.IsKeyDown(Keys.Right) || p_keyboard.IsKeyDown(Keys.D);
    }

    public static bool MiniBomb()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Space);
    }

    public static bool BigBomb()
    {
        return p_keyboard.WasKeyJustPressed(Keys.X);
    }

    public static bool Pause()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Escape);
    }

    public static bool Action()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Enter);
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