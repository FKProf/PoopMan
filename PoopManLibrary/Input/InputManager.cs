namespace PoopManLibrary.Input;

public class InputManager
{
    public InputManager()
    {
        Keyboard = new KeyboardInfo();
        Mouse = new MouseInfo();
        GamePad = new GamePadInfo();
    }

    public KeyboardInfo Keyboard { get; }

    public MouseInfo Mouse { get; }

    public GamePadInfo GamePad { get; }

    public void Update()
    {
        Keyboard.Update();
        Mouse.Update();
        GamePad.Update();
    }
}