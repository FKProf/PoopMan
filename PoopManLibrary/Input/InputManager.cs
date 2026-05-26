namespace PoopManLibrary.Input;

public class InputManager
{
    public InputManager()
    {
        Keyboard = new KeyboardInfo();
        Mouse = new MouseInfo();
    }

    public KeyboardInfo Keyboard { get; }

    public MouseInfo Mouse { get; }

    public void Update()
    {
        Keyboard.Update();
        Mouse.Update();
    }
}