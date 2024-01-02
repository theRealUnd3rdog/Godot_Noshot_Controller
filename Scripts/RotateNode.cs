using Godot;
using System;

public partial class RotateNode : DirectionalLight3D
{
    [Export] private float _rotationSpeed = 90f;
    
	public override void _Process(double delta)
	{
        Rotate(new Vector3(0f, 1f, 0f), Mathf.DegToRad(_rotationSpeed));
	}
}
