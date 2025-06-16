using Godot;
using System;

public partial class PauseMenu : Control
{
    public static Action OnReset;
    [Export] private Panel _pauseMenu;

    public override void _Ready()
    {
        _pauseMenu.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        _pauseMenu.Visible = !_pauseMenu.Visible;
        if (_pauseMenu.Visible)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetTree().Paused = true;
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetTree().Paused = false;
        }
    }

    private void OnResetPressed()
    {
        GD.Print("Reset Pressed");
        OnReset?.Invoke();

        TogglePauseMenu();
    }

    private void OnQuitPressed()
    {
        GD.Print("Quit Pressed");
        GetTree().Quit();
    }
}
