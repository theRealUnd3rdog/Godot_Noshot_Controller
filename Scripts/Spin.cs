using Godot;
using System;

public partial class Spin : MeshInstance3D
{
	[Export]
	public float _spinSpeed = 1f;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.`
	public override void _Process(double delta)
	{
		Rotate(new Vector3(0f, 1f, 0f), Mathf.DegToRad(_spinSpeed));
	}
}
