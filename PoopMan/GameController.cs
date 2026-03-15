using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan;

public class GameController
{
    private static KeyboardInfo p_keyboard => Core.Input.Keyboard;

    public static bool MoveUp()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Up) ||
               p_keyboard.WasKeyJustPressed(Keys.W);
    }

    public static bool MoveDown()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Down) ||
               p_keyboard.WasKeyJustPressed(Keys.S);
    }

    public static bool MoveLeft()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Left) ||
               p_keyboard.WasKeyJustPressed(Keys.A);
    }

    public static bool MoveRight()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Right) ||
               p_keyboard.WasKeyJustPressed(Keys.D);
    }

    public static bool Pause()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Escape);
    }

    public static bool Action()
    {
        return p_keyboard.WasKeyJustPressed(Keys.Enter);
    }
}
