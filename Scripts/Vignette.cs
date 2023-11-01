using Godot;
using System;

public partial class Vignette : ColorRect
{
	[Export] public float vIntensity {set; get;}
	[Export] public float vOpacity {set; get;}
	[Export] public Color vColor {set; get;}

	private ShaderMaterial _material;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_material = (ShaderMaterial)Material;

		vIntensity = (float)_material.GetShaderParameter("vignette_intensity");
		vOpacity = (float)_material.GetShaderParameter("vignette_opacity");
		vColor = (Color)_material.GetShaderParameter("vignette_rgb");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_material.SetShaderParameter("vignette_intensity", vIntensity);
		_material.SetShaderParameter("vignette_opacity", vOpacity);
		_material.SetShaderParameter("vignette_rgb", vColor);

		//GD.Print($"Vignette {(float)_material.GetShaderParameter("vignette_opacity")}");
	}
}
