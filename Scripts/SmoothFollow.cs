using Godot;
using System;

public partial class SmoothFollow : Node3D
{
	private Node3D _parentNode;
	[Export] private float _smoothSpeed = 10.0f;
	[Export] private float _maxYSmoothingDistance = 1.0f; // in meters	
	private float _currentYSmoothedDistance;
	[Export] private bool _enableYSmoothing = true;
	[Export] private bool _enableLocation = true;
	[Export] private bool _enableRotation = true;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TopLevel = true;

		_parentNode = GetParent<Node3D>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		Vector3 targetLoc = _parentNode.GlobalPosition;
		
		if (_enableLocation)
		{
			Vector3 currentPos = Position;

			if (!_enableYSmoothing)
			{
				// If Y smoothing is disabled, set the Y position directly
				currentPos.Y = targetLoc.Y;
			}

			Vector3 targetPos = new Vector3(targetLoc.X, currentPos.Y, targetLoc.Z);

			// Smooth X and Z
			Vector3 smoothed = currentPos.Lerp(targetPos, 1.0f - Mathf.Pow(0.5f, (float)delta * _smoothSpeed));

			if (_enableYSmoothing)
			{
				// Smooth Y only if within limit
				float yDistance = Mathf.Abs(targetLoc.Y - currentPos.Y);

				_currentYSmoothedDistance = Mathf.Lerp(currentPos.Y, targetLoc.Y, (float)delta * _smoothSpeed);
				smoothed.Y = Mathf.Clamp(_currentYSmoothedDistance, targetLoc.Y - _maxYSmoothingDistance, targetLoc.Y + _maxYSmoothingDistance);

				//GD.Print(yDistance);
			}
			

			Position = smoothed;
		}

		Transform3D newTransform = GlobalTransform;
		Quaternion curRot = newTransform.Basis.GetRotationQuaternion();
		Quaternion targetRot = _parentNode.GlobalTransform.Basis.GetRotationQuaternion();

		Quaternion newRot = curRot.Slerp(targetRot, 1.0f - Mathf.Pow(0.5f, (float)delta * _smoothSpeed));
		
		newTransform.Basis = new Basis(newRot);

		if (_enableRotation)
			GlobalBasis = newTransform.Basis;
	}
}