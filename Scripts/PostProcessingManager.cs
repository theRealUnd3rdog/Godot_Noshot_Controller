using Godot;
using System;

public partial class PostProcessingManager : CanvasGroup
{
	public static PostProcessingManager Instance;

	public DirectionalLight3D mainLight;
	public Camera3D mainCamera;
	[Export] public LensFlare lensFlare;
	[Export] public Vignette vignette;
	[Export] public LensDistortion lensDistortion;
	[Export] public ChromaticAbberation chromaticAbberation;
	[Export] public RadialBlur radialBlur;

	// Called when the node enters the scene tree for the first time.
	public override void _EnterTree()
	{
		Instance = this;

		mainLight = GetNode<DirectionalLight3D>("%MainLight");
		mainCamera = GetNode<Camera3D>("%Camera3D");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

	}
}
