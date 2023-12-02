using Godot;
using System;

public partial class PlayerParticles : Node3D
{
	[ExportCategory("Particles")]
	[Export] private GpuParticles3D _landParticles;

	[ExportSubgroup("Sliding")]
	[Export] private GpuParticles3D _slideParticles;
	private float _minVelSlide;
	private float _maxVelSlide;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PlayerAir.LastVelocityChangeLanded += PlayLandParticles;
		PlayerSlide.SlideStartChange += PlaySlideParticles;
		PlayerSlide.SlideCurrentChange += ChangeSlideParticles;

		_minVelSlide = (float)_slideParticles.ProcessMaterial.Get("initial_velocity_min");
		_maxVelSlide = (float)_slideParticles.ProcessMaterial.Get("initial_velocity_max");
	}

    public override void _ExitTree()
    {
		PlayerAir.LastVelocityChangeLanded -= PlayLandParticles;
		PlayerSlide.SlideStartChange -= PlaySlideParticles;
		PlayerSlide.SlideCurrentChange -= ChangeSlideParticles;
    }

	private void PlayLandParticles(Vector3 velocity)
	{
		_landParticles.Restart();
		_landParticles.Emitting = true;
	}

	private void PlaySlideParticles()
	{
		_slideParticles.Emitting = true;
	}

	private void ChangeSlideParticles(float timer)
	{
		if (timer > 0.05f)
		{
			float minVel = _minVelSlide * Mathf.InverseLerp(0f, 1f, timer);
			float maxVel = _maxVelSlide * Mathf.InverseLerp(0f, 1f, timer);

			_slideParticles.ProcessMaterial.Set("initial_velocity_min", minVel);
			_slideParticles.ProcessMaterial.Set("initial_velocity_max", maxVel);
		}
		else if (timer < 0.5f)
		{
			_slideParticles.Emitting = false;
		}
	}
}
