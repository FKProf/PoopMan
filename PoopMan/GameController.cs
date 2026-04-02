using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan;

public class GameController
{
    private static KeyboardInfo p_keyboard => Core.Input.Keyboard;

    // === Pressione singola (tap) — per menu, azioni una-tantum ===
    public static bool MoveUp() => p_keyboard.WasKeyJustPressed(Keys.Up) || p_keyboard.WasKeyJustPressed(Keys.W);
    public static bool MoveDown() => p_keyboard.WasKeyJustPressed(Keys.Down) || p_keyboard.WasKeyJustPressed(Keys.S);
    public static bool MoveLeft() => p_keyboard.WasKeyJustPressed(Keys.Left) || p_keyboard.WasKeyJustPressed(Keys.A);
    public static bool MoveRight() => p_keyboard.WasKeyJustPressed(Keys.Right) || p_keyboard.WasKeyJustPressed(Keys.D);

    // === Tasto tenuto premuto (hold) — per movimento continuo ===
    public static bool HoldUp() => p_keyboard.IsKeyDown(Keys.Up) || p_keyboard.IsKeyDown(Keys.W);
    public static bool HoldDown() => p_keyboard.IsKeyDown(Keys.Down) || p_keyboard.IsKeyDown(Keys.S);
    public static bool HoldLeft() => p_keyboard.IsKeyDown(Keys.Left) || p_keyboard.IsKeyDown(Keys.A);
    public static bool HoldRight() => p_keyboard.IsKeyDown(Keys.Right) || p_keyboard.IsKeyDown(Keys.D);

    public static bool Pause() => p_keyboard.WasKeyJustPressed(Keys.Escape);
    public static bool Action() => p_keyboard.WasKeyJustPressed(Keys.Enter);
}