using Godot;
using System;

public partial class ChromaticAbberation : ColorRect
{
	[Export] public float strength;

	private ShaderMaterial _material;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_material = (ShaderMaterial)Material;

		strength = (float)_material.GetShaderParameter("strength");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_material.SetShaderParameter("strength", strength);
	}
}
