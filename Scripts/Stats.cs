using Godot;
using System;

public partial class Stats : Control
{
	[Export] private Label _fpsLabel;
    
    public override void _Process(double delta)
    {
        double fps = Engine.GetFramesPerSecond();
		_fpsLabel.Text = $"FPS: {Mathf.RoundToInt(fps).ToString()}";
    }
}
