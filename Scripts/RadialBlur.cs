using Godot;
using System;

public partial class RadialBlur : ColorRect
{
	[Export] public float power;

	private ShaderMaterial _material;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_material = (ShaderMaterial)Material;

		power = (float)_material.GetShaderParameter("blur_power");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_material.SetShaderParameter("blur_power", power);
	}
}
