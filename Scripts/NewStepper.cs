using Godot;
using System;

public partial class NewStepper : Node3D
{
	[ExportCategory("Stepper")]
	[Export] private bool _enableStepper = true;
	[Export] private RayCast3D _stepperRay;
	[Export] private CharacterBody3D _characterBody;
	[Export] private PlayerMovement _playerMovement;

	[ExportSubgroup("Stepper projection")]
	[Export(PropertyHint.Range, "0.05f, 2f")] private float _projectionMaxDistance;
	[Export(PropertyHint.Range, "0.1f, 3f")] private float _stepHeight;
	
	private float _stepRayDistance;

	[Export(PropertyHint.Range, "0.05f, 0.2f")] private float _maxElevation;

	[ExportSubgroup("Projection Smoother")]
	[Export] private float _smoothingSpeed = 5.0f;

	[ExportSubgroup("Gizmos")]
	[Export] private MeshInstance3D _pointGizmo;

	private Vector3 _currentProjection = Vector3.Zero;
	private Vector3 _stepPoint;

    public override void _Ready()
    {
		_stepRayDistance = _stepHeight * 4f;

		Vector3 rayPos = new Vector3(0f, Mathf.Abs(_stepHeight), 0f);
		_stepperRay.Position = rayPos;

		Vector3 rayDistanceVector = new Vector3(0f, -_stepRayDistance, 0f);
		_stepperRay.TargetPosition = rayDistanceVector;
    }

    public override void _ExitTree()
    {

    }

    public override void _PhysicsProcess(double delta)
	{
		if (!_enableStepper)
			return;

		// Get the raw input direction and smooth it out
		Vector3 rawProjectedXZ = new Vector3(_playerMovement.inputDirection.X, 0f, _playerMovement.inputDirection.Y);
		_currentProjection = _currentProjection.Lerp(rawProjectedXZ, _smoothingSpeed * (float)delta);

		// Get the projected point based on input
		Vector3 inputProjectionPoint = _projectionMaxDistance * _currentProjection;

		Position = new Vector3(inputProjectionPoint.X, _stepperRay.Position.Y, inputProjectionPoint.Z);
		
		// Perform the snapping to step here if step is found
		if (GetStep(out Vector3 projectionPoint))
		{
			_pointGizmo.GlobalPosition = projectionPoint;

			Vector3 stepPosition = _characterBody.GlobalPosition;

			if (stepPosition.Y > projectionPoint.Y)
			{
				stepPosition = projectionPoint;
			}
			else
			{
				stepPosition.Y = projectionPoint.Y;
			}

			_characterBody.GlobalPosition = stepPosition;
		}
		else
		{
			_pointGizmo.GlobalPosition = _characterBody.Position;
		}
	}

	private bool GetStep(out Vector3 projectionPoint)
	{
		projectionPoint = default(Vector3);

		// Check if is in floor
		if (!_characterBody.IsOnFloor())
		{
			return false;
		}

		// Check if it's colliding
		if (!_stepperRay.IsColliding())
		{
			return false;
		}

		Vector3 stepPoint = _stepperRay.GetCollisionPoint();
		Vector3 stepNormal = _stepperRay.GetCollisionNormal();

		// Check if the normal is approx 1
		if (stepNormal.Y < 0.99f)
		{
			return false;
		}

		// Check if there's an elevation
		float elevation = Mathf.Abs(_characterBody.GlobalPosition.Y - stepPoint.Y);

		if (elevation < _maxElevation)
		{
			return false;
		}

		// Check if the elevation is more than the step height, if it is, it's too high
		if (elevation > _stepHeight)
		{
			return false;
		}

		projectionPoint = stepPoint;

		// All of the conditions are true, return the step
		return true;
	}
}
