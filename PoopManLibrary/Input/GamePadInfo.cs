using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PoopManLibrary.Input;

/// <summary>
///     Stato del gamepad (giocatore 1) con rilevamento "appena premuto",
///     sia per i pulsanti sia per la levetta sinistra usata come D-pad digitale.
/// </summary>
public class GamePadInfo
{
    /// <summary>Soglia oltre la quale la levetta analogica conta come direzione premuta.</summary>
    public const float StickThreshold = 0.5f;

    private readonly PlayerIndex _player;

    public GamePadInfo(PlayerIndex player = PlayerIndex.One)
    {
        _player = player;
        PreviousState = new GamePadState();
        CurrentState = SafeGetState();
    }

    public GamePadState PreviousState { get; private set; }
    public GamePadState CurrentState { get; private set; }

    public bool IsConnected => CurrentState.IsConnected;

    public void Update()
    {
        PreviousState = CurrentState;
        CurrentState = SafeGetState();
    }

    public bool IsButtonDown(Buttons button)
    {
        return CurrentState.IsConnected && CurrentState.IsButtonDown(button);
    }

    public bool WasButtonJustPressed(Buttons button)
    {
        return CurrentState.IsConnected && CurrentState.IsButtonDown(button) && PreviousState.IsButtonUp(button);
    }

    // ── Direzioni (D-pad oppure levetta sinistra) ─────────────────────────
    public bool IsUpDown => IsButtonDown(Buttons.DPadUp) || Stick(CurrentState).Y > StickThreshold;
    public bool IsDownDown => IsButtonDown(Buttons.DPadDown) || Stick(CurrentState).Y < -StickThreshold;
    public bool IsLeftDown => IsButtonDown(Buttons.DPadLeft) || Stick(CurrentState).X < -StickThreshold;
    public bool IsRightDown => IsButtonDown(Buttons.DPadRight) || Stick(CurrentState).X > StickThreshold;

    public bool WasUpJustPressed => WasButtonJustPressed(Buttons.DPadUp) ||
                                    (Stick(CurrentState).Y > StickThreshold && Stick(PreviousState).Y <= StickThreshold);

    public bool WasDownJustPressed => WasButtonJustPressed(Buttons.DPadDown) ||
                                      (Stick(CurrentState).Y < -StickThreshold && Stick(PreviousState).Y >= -StickThreshold);

    public bool WasLeftJustPressed => WasButtonJustPressed(Buttons.DPadLeft) ||
                                      (Stick(CurrentState).X < -StickThreshold && Stick(PreviousState).X >= -StickThreshold);

    public bool WasRightJustPressed => WasButtonJustPressed(Buttons.DPadRight) ||
                                       (Stick(CurrentState).X > StickThreshold && Stick(PreviousState).X <= StickThreshold);

    private static Vector2 Stick(GamePadState s)
    {
        return s.IsConnected ? s.ThumbSticks.Left : Vector2.Zero;
    }

    private GamePadState SafeGetState()
    {
        try
        {
            return GamePad.GetState(_player);
        }
        catch
        {
            // Alcuni driver/backend possono lanciare eccezioni: il gioco resta giocabile da tastiera
            return new GamePadState();
        }
    }
}
