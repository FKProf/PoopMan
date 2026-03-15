using Microsoft.Xna.Framework.Input;

namespace PoopManLibrary.Input;

public class KeyboardInfo
{
    public  KeyboardState PreviousState { get; private set; }
    public KeyboardState CurrentState { get; private set; }

    public KeyboardInfo()
    {
        PreviousState = new KeyboardState();
        CurrentState = Keyboard.GetState();
    }

    public void Update()
    {
        PreviousState = CurrentState;
        CurrentState = Keyboard.GetState();
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
