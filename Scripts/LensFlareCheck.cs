using Godot;
using System;

public partial class LensFlareCheck : Marker3D
{
	private DirectionalLight3D _directionalLight;
	private Camera3D _camera;

    public override void _Ready()
    {
		_directionalLight = PostProcessingManager.Instance.mainLight;
		_camera = PostProcessingManager.Instance.mainCamera;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		CheckForSunVisible();
	}

	private bool ObjectIsInterectingSun()
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Vector3 effSunPosition = _directionalLight.GlobalTransform.Basis.Z * _camera.Far;
		effSunPosition += _camera.GlobalPosition;

		Vector3 rayOrigin = _camera.GlobalPosition;
		Vector3 rayEnd = effSunPosition;

		PhysicsRayQueryParameters3D parameters = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);

		var rayArray = spaceState.IntersectRay(parameters);

		if (rayArray.ContainsKey("collider"))
		{
			return true;
		}
			

		return false;
	}

	private void CheckForSunVisible()
	{
		PostProcessingManager.Instance.lensFlare.sunBlocked = ObjectIsInterectingSun();
	}
}
