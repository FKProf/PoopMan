using System;
using Microsoft.Xna.Framework.Input;

namespace PoopManLibrary.Input;

/// <summary>
///     Sostituisce PoopManLibrary/Input/KeyboardInfo.cs nell'auto-play: identica, ma lo
///     stato della tastiera può arrivare dal bot (<see cref="Provider" />) invece che
///     dalla tastiera fisica.
/// </summary>
public class KeyboardInfo
{
    public static Func<KeyboardState> Provider;

    public KeyboardInfo()
    {
        PreviousState = new KeyboardState();
        CurrentState = Get();
    }

    public KeyboardState PreviousState { get; private set; }
    public KeyboardState CurrentState { get; private set; }

    private static KeyboardState Get()
    {
        return Provider != null ? Provider() : Keyboard.GetState();
    }

    public void Update()
    {
        PreviousState = CurrentState;
        CurrentState = Get();
    }

    public bool IsKeyDown(Keys key)
    {
        return CurrentState.IsKeyDown(key);
    }

    public bool WasKeyJustPressed(Keys key)
    {
        return CurrentState.IsKeyDown(key) && PreviousState.IsKeyUp(key);
    }

    public bool WasKeyJustReleased(Keys key)
    {
        return CurrentState.IsKeyUp(key) && PreviousState.IsKeyDown(key);
    }
}
