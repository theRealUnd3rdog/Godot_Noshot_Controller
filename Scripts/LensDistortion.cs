using Godot;
using System;

public partial class LensDistortion : ColorRect
{
	[Export] public float zoom;

	private ShaderMaterial _material;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_material = (ShaderMaterial)Material;

		zoom = (float)_material.GetShaderParameter("zoom");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_material.SetShaderParameter("zoom", zoom);
	}
}
