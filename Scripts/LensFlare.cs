using Godot;
using System;

public partial class LensFlare : ColorRect
{
	public bool sunBlocked;
	private Vector3 _effSunDirection;
	private float _adjustTime = 0.25f;

	private ShaderMaterial _material;
	private DirectionalLight3D _directionalLight;
	private Camera3D _camera;

    public override void _Ready()
    {
        _material = (ShaderMaterial)Material;

		_directionalLight = PostProcessingManager.Instance.mainLight;
		_camera = PostProcessingManager.Instance.mainCamera;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_effSunDirection = (-_directionalLight.GlobalTransform.Basis.Z * Mathf.Max(_camera.Near, 1.0f)).Normalized();
		_effSunDirection += _camera.GlobalTransform.Origin;

		if (sunBlocked)
		{
			FadeOutLensFlare();
			return;
		}

		if (_camera.IsPositionBehind(_effSunDirection))
		{
			FadeOutLensFlare();
		}

		if (Visible)
		{
			FadeInLensFlare();
			UpdateLensFlareLocation();
		}
	}

	private void FadeInLensFlare()
	{
		Tween tween = CreateTween();
		tween.TweenProperty(_material, "shader_parameter/tint", new Vector3(1.8f, 1.8f, 1.2f), _adjustTime);
	}

	private void FadeOutLensFlare()
	{
		Tween tween = CreateTween();
		tween.TweenProperty(_material, "shader_parameter/tint", new Vector3(0f, 0f, 0f), _adjustTime);
	}

	private void UpdateLensFlareLocation()
	{
		Vector2 unprojectedSunPos = _camera.UnprojectPosition(_effSunDirection);
		_material.SetShaderParameter("sun_position", unprojectedSunPos);
	}
}
